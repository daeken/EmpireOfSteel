using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Kvm;

namespace CoreEmulator;

public unsafe class Core : ISystem {
	public static readonly KvmVm Vm = new();
	public static readonly BoundMemory PhysMem;
	public static readonly ulong RamSize = 8UL * 1024 * 1024 * 1024;
	public static readonly KvmVcpu Vcpu;
	public static readonly Socket ConsoleSocket;
	public static readonly Dictionary<uint, IEventChannel> EventChannels = new();

	public XenStore XenStore;

	string XenConsoleBuf = "";

	static Core() {
		PhysMem = Vm.Map(0, RamSize);
		Vcpu = Vm.CreateVcpu();
		try {
			ConsoleSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
			ConsoleSocket.Connect(new UnixDomainSocketEndPoint("./console.sock"));
			ConsoleSocket.ReceiveTimeout = 10;
		} catch(Exception) {
			ConsoleSocket = null;
		}
		Timer.Run();
	}
	
	public void Run() {
		Vm.System = this;
		PhysMem.AsSpan<byte>()[0] = 0xF4;
		PhysMem.AsSpan<byte>()[1] = 0xCC;

		var pml4Base = 0x2000UL;
		var pdpBase = 0x3000UL;
		var pdBase = 0x4000UL;

		var pml4 = PhysMem.AsSpan<ulong>(pml4Base);
		pml4[0] = pdpBase | 0b11;
		pml4[0b111111111] = pdpBase | 0b11;

		var pdp = PhysMem.AsSpan<ulong>(pdpBase);
		var physAddr = 0UL;
		pdp[0] = pdBase | 0b11;
		pdp[0b111111110] = pdBase | 0b11;
		var pd = PhysMem.AsSpan<ulong>(pdBase);
		for(var j = 0; j < 512; ++j, physAddr += 0x200000)
			pd[j] = physAddr | 0b10000011; // 2MB page size!

		var gdtBase = 0x1000UL;
		var gdt = PhysMem.AsSpan<ulong>(gdtBase);
		gdt[0] = 0;
		gdt[1] = 0x00209A0000000000;
		gdt[2] = 0x0000920000000000;

		var sregs = Vcpu.Sregs;
		sregs.Gdt.Base = gdtBase;
		sregs.Gdt.Limit = 24;

		// Zero-length IDT so any NMI triple-faults
		sregs.Idt.Base = 0;
		sregs.Idt.Limit = 0;

		sregs.Efer |= 0x0500; // LMA | LME
		sregs.Cr0 |= 0x80000001; // Paging and protection
		sregs.Cr3 = pml4Base;
		sregs.Cr4 = 0b10100000; // PAE enabled

		var seg = new KvmSegment {
			Base = 0, 
			Limit = 0xFFFFFFFF, 
			Selector = 1 << 3, 
			Present = 1, 
			Type = 11, 
			Dpl = 0, 
			Db = 0, 
			S = 1, 
			L = 1, 
			G = 1
		};
		sregs.Cs = seg;
		seg.Type = 3;
		seg.Selector = 1 << 4;
		sregs.Ds = sregs.Es = sregs.Fs = sregs.Gs = sregs.Ss = seg; 

		Vcpu.Sregs = sregs;

		var kernSpace = PhysMem.AsSpan<byte>(0x200000); // Add another 2MB because ... reasons?
		var kernData = File.ReadAllBytes("kernel")[..0x006ef2e8]; // TODO: HACK
		kernData.CopyTo(kernSpace);

		Vcpu.Rip = 0xffffffff806e6000UL; // TODO: Actually read xen_start (XEN_ELFNOTE_ENTRY)

		var (valid, _, addr) = Vcpu.Translate(Vcpu.Rip);
		Console.WriteLine($"0x{Vcpu.Rip:X} at physical 0x{addr:X} (valid {valid})");

		Console.WriteLine($"Starting at 0x{Vcpu.Rip:X}");

		var hypercallPage = 0xffffffff806e5000UL; // TODO: Actually read XEN_ELFNOTE_HYPERCALL_PAGE
		var hp = PhysMem.AsSpan<byte>(Vcpu.SafeTranslate(hypercallPage));
		for(var i = 0; i < 128; ++i) {
			var r = i * 32;
			// mov eax, i
			hp[r++] = 0xb8;
			hp[r++] = (byte) i;
			hp[r++] = 0;
			hp[r++] = 0;
			hp[r++] = 0;
			// Just use vmcall; kvm handles translation
			// But it'll be faster on AMD if we use vmmcall there
			hp[r++] = 0x0F; // vmcall
			hp[r++] = 0x01;
			hp[r++] = 0xC1;
			hp[r++] = 0xC3; // ret
		}

		var startInfoBase = 0x1000UL;
		var _startInfo = PhysMem.AsSpan<StartInfo>(startInfoBase);
		ref var startInfo = ref _startInfo[0];
		unsafe {
			var magic = Encoding.ASCII.GetBytes("{Empire Of Steel}");
			fixed(byte* magicptr = startInfo.Magic)
				magic.CopyTo(new Span<byte>(magicptr, 32));
			var cmdline = Encoding.ASCII.GetBytes("boot_serial=YES,boot_verbose=YES,console=xc0");
			fixed(byte* cmdptr = startInfo.CmdLine)
				cmdline.CopyTo(new Span<byte>(cmdptr, 1024));
		}
		startInfo.NumPages = 8UL * 1024 * 1024 * 1024 / 0x1000;
		Vm.XenLongMode = true;
		startInfo.SharedInfo = 0x8000UL;
		Vm.XenSharedInfo = startInfo.SharedInfo >> 12; // PFN rather than GPA... Fucking stupid.
		var storeAddr = 0x9000UL;
		XenStore = new(PhysMem.AsPointer<XenStoreDomainInterface>(Vcpu.SafeTranslate(storeAddr)));
		startInfo.StoreMfn = storeAddr >> 12;
		startInfo.StoreEvtChn = Register(XenStore);

		Vcpu.XenInfo = startInfo.SharedInfo; // It's really sharedinfo + vcpu_id * 64

		Vcpu.Rsi = 0xffffffff80000000 + startInfoBase;
		Vcpu.Rsp = 0xffffffff80000000 + 0x2000000UL;

		// HACK: Why does this call exist?!
		var temp = PhysMem.AsSpan<byte>(Vcpu.SafeTranslate(0xffffffff8072b5d4));
		for(var i = 0; i < 5; ++i)
			temp[i] = 0x90;
		//PhysMem.AsSpan<byte>(Vcpu.SafeTranslate(0xffffffff81080010))[0] = 0xF4;
		//PhysMem.AsSpan<byte>(Vcpu.SafeTranslate(0xffffffff80c1b950))[0] = 0xF4; // panic hook

		Console.CancelKeyPress += (sender, eventArgs) => eventArgs.Cancel = true;
		
		try {
			Vcpu.Run();
		} catch(Exception e) {
			Console.WriteLine(e);
			Console.WriteLine();
	
			Console.WriteLine("Registers:");
			Console.WriteLine($"RAX 0x{Vcpu.Rax:X}    RBX 0x{Vcpu.Rbx:X}");
			Console.WriteLine($"RCX 0x{Vcpu.Rcx:X}    RDX 0x{Vcpu.Rdx:X}");
			Console.WriteLine($"RBP 0x{Vcpu.Rbp:X}    RSP 0x{Vcpu.Rsp:X}");
			Console.WriteLine($"RDI 0x{Vcpu.Rax:X}    RSI 0x{Vcpu.Rsi:X}");
			Console.WriteLine($"R8  0x{Vcpu.R8:X}    R9  0x{Vcpu.R9:X}");
			Console.WriteLine($"R10 0x{Vcpu.R10:X}    R11 0x{Vcpu.R11:X}");
			Console.WriteLine($"R12 0x{Vcpu.R12:X}    R13 0x{Vcpu.R13:X}");
			Console.WriteLine($"R14 0x{Vcpu.R14:X}    R15 0x{Vcpu.R15:X}");
			Console.WriteLine($"RIP 0x{Vcpu.Rip:X}");

			Console.WriteLine("Stack contents:");
			var stack = PhysMem.AsSpan<ulong>(Vcpu.SafeTranslate(Vcpu.Rsp));
			for(var i = 0; i < 16; ++i)
				Console.WriteLine($"0x{stack[i]:X}");
		}
	}
	
	public ulong Hypercall(KvmVcpu cpu, ulong num, ulong[] args) {
		switch((XenHypercall) num) {
			case XenHypercall.ConsoleIo: {
				if(args[0] == 0) { // Write!
					var (size, addr) = (args[1], args[2]);
					var buf = PhysMem.AsSpan<byte>(cpu.SafeTranslate(addr))[..(int) size];
					if(ConsoleSocket != null)
						ConsoleSocket.Send(buf);
					else {
						var str = Encoding.ASCII.GetString(buf);
						foreach(var c in str) {
							if(c == '\n') {
								Console.WriteLine($"Xen console: {XenConsoleBuf}");
								XenConsoleBuf = "";
							} else
								XenConsoleBuf += c;
						}
					}
				} else {
					if(ConsoleSocket == null) break;
					var (size, addr) = (args[1], args[2]);
					var buf = PhysMem.AsSpan<byte>(cpu.SafeTranslate(addr))[..(int) size];
					try {
						return (ulong) ConsoleSocket.Receive(buf);
					} catch(Exception) {
					}
				}
				break;
			}
			case XenHypercall.MemoryOp: {
				if(args[0] == 9) { // XENMEM_memory_map
					var addr = cpu.SafeTranslate(args[1]);
					var memmap = PhysMem.AsSpan<uint>(addr);
					memmap[0] = 1;
					var bufaddr = cpu.SafeTranslate(PhysMem.AsSpan<ulong>(addr)[1]);
					var entries = PhysMem.AsSpan<ulong>(bufaddr);
					entries[0] = 0; // Base of physical memory
					entries[1] = RamSize;
					entries[2] = 1; // Normal ram
				}
				break;
			}
			case XenHypercall.VcpuOp: {
				var cpunum = args[1];
				Console.WriteLine($"Vcpu op for cpunum 0x{cpunum:X} (GS 0x{cpu.Sregs.Gs.Base:X} -- vcpu_id? 0x{PhysMem.AsSpan<uint>(cpu.SafeTranslate(cpu.Sregs.Gs.Base + 0x30c))[0]:X})");
				switch((XenVcpuOp) args[0]) {
					case XenVcpuOp.IsUp:
						return cpunum == 0 ? 1 : ~0UL;
					case XenVcpuOp.StopPeriodicTimer:
						break;
					case XenVcpuOp.RegisterVcpuInfo: {
						ref var str = ref PhysMem.AsSpan<VcpuRegisterVcpuInfo>(cpu.SafeTranslate(args[2]))[0];
						cpu.XenInfo = (str.Mfn << 12) + str.Offset;
						break;
					}
					case XenVcpuOp.SetSingleshotTimer:
						Console.WriteLine($"Ignoring SetSingleshotTimer for vcpu {cpunum}");
						break;
					case {} x:
						throw new Exception($"Unhandled VCPU op from xen interface: {x}");
				}
				break;
			}
			case XenHypercall.PhysdevOp: {
				switch(args[0]) {
					case 28: // PHYSDEVOP_pirq_eoi_gmfn_v2
						break;
					default:
						throw new Exception($"Unhandled Physdev op from xen interface: {args[0]}");
				}
				break;
			}
			case XenHypercall.GrantTableOp: {
				Console.WriteLine($"Grant table op {args[0]}");
				break;
			}
			case XenHypercall.EventChannelOp: {
				switch(args[0]) {
					case 6: { // EVTCHNOP_alloc_unbound
						ref var str = ref PhysMem.AsRef<XenEventChnAllocUnbound>(cpu.SafeTranslate(args[1]));
						Console.WriteLine($"Allocating unbound event channel from {str.Dom} for {str.RemoteDom}");
						str.OutPort = Register(new PlainEventChannel());
						break;
					}
					default:
						Console.WriteLine($"Unhandled event channel op: {args[0]}");
						break;
				}
				break;
			}
			case {} unk:
				throw new Exception($"Unhandled Xen hypercall: {unk}");
		}
		return 0;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct VcpuRegisterVcpuInfo {
		public ulong Mfn;
		public uint Offset;
		uint Reserved;
	}
	
	public void PortIo(KvmVcpu cpu, int port, bool write, Span<byte> data) {
	}

	public uint Register(IEventChannel channel) {
		channel.Port = (uint) EventChannels.Count + 1;
		EventChannels[channel.Port] = channel;
		return channel.Port;
	}
}