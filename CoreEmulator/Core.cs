using System.Text;
using Kvm;

namespace CoreEmulator;

public class Core : ISystem {
	public static readonly KvmVm Vm = new();
	public static readonly BoundMemory PhysMem;
	public static readonly ulong RamSize = 8UL * 1024 * 1024 * 1024;
	public static readonly KvmVcpu Vcpu;

	string XenConsoleBuf = "";

	static Core() {
		PhysMem = Vm.Map(0, RamSize);
		Vcpu = Vm.CreateVcpu();
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
		var kernData = File.ReadAllBytes("kernel")[..0x017bf3b8]; // TODO: HACK
		kernData.CopyTo(kernSpace);

		Vcpu.Rip = 0xffffffff8108f000; // TODO: Actually read xen_start (XEN_ELFNOTE_ENTRY)

		var (valid, _, addr) = Vcpu.Translate(Vcpu.Rip);
		Console.WriteLine($"0x{Vcpu.Rip:X} at physical 0x{addr:X} (valid {valid})");

		Console.WriteLine($"Starting at 0x{Vcpu.Rip:X}");

		var hypercallPage = 0xffffffff8108e000UL; // TODO: Actually read XEN_ELFNOTE_HYPERCALL_PAGE
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
		startInfo.SharedInfo = 0x8000L;
		Vm.XenSharedInfo = startInfo.SharedInfo >> 12; // PFN rather than GPA... Fucking stupid.

		Vcpu.XenInfo = startInfo.SharedInfo; // It's really sharedinfo + vcpu_id * 64

		Vcpu.Rsi = 0xffffffff80000000 + startInfoBase;
		Vcpu.Rsp = 0xffffffff80000000 + 0x2000000UL;

		// HACK: Why does this call exist?!
		var temp = PhysMem.AsSpan<byte>(Vcpu.SafeTranslate(0xffffffff8117ae44));
		for(var i = 0; i < 5; ++i)
			temp[i] = 0x90;
		//PhysMem.AsSpan<byte>(Vcpu.SafeTranslate(0xffffffff80a8b3f0))[0] = 0xF4;
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
					var str = Encoding.ASCII.GetString(buf);
					foreach(var c in str) {
						if(c == '\n') {
							Console.WriteLine($"Xen console: {XenConsoleBuf}");
							XenConsoleBuf = "";
						} else
							XenConsoleBuf += c;
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
				if(args[0] == 3) { // VCPUOP_is_up
					var cpunum = args[1];
					if(cpunum == 1)
						return 1;
					return ~0UL;
				}
				throw new Exception($"Unhandled VCPU op from xen interface: {args[0]}");
			}
			case {} unk:
				throw new Exception($"Unhandled Xen hypercall: {unk}");
		}
		return 0;
	}
	
	public void PortIo(KvmVcpu cpu, int port, bool write, Span<byte> data) {
	}
}