using System.Runtime.InteropServices;

namespace Kvm;

enum KvmExitReason : uint {
	Unknown,
	Exception,
	Io,
	Hypercall,
	Debug,
	Hlt,
	Mmio, 
	IrqWindowOpen, 
	Shutdown, 
	FailEntry, 
	Intr, 
	SetTpr, 
	TprAccess, 
	Dcr = 15, 
	Nmi, 
	InternalError, 
	Osi, 
	PaprHCall, 
	Xen = 34
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
unsafe struct KvmXenExit {
	internal uint Type;
	uint _;
	
	// Only hypercalls exist right now, so no need for union
	internal uint LongMode;
	internal uint Cpl;
	internal ulong Input;
	internal ulong Result;
	internal fixed ulong Params[6];
}

[StructLayout(LayoutKind.Explicit)]
struct KvmCpuRun {
	[FieldOffset(0)] internal byte RequestInterruptWindow;
	[FieldOffset(8)] internal KvmExitReason ExitReason;
	[FieldOffset(12)] internal byte ReadyForInterruptInjection;
	[FieldOffset(13)] internal byte IfFlag;
	[FieldOffset(16)] internal ulong Cr8;
	[FieldOffset(24)] internal ulong ApicBase;

	[FieldOffset(32)] internal KvmXenExit XenExit;
}

[StructLayout(LayoutKind.Sequential)]
struct KvmRegs {
	internal ulong Rax, Rbx, Rcx, Rdx,
		Rsi, Rdi, Rsp, Rbp,
		R8, R9, R10, R11, 
		R12, R13, R14, R15, 
		Rip, Rflags;
}

[StructLayout(LayoutKind.Explicit)]
struct KvmTranslate {
	[FieldOffset(0)] internal ulong LinearAddress;
	[FieldOffset(8)] internal ulong PhysicalAddress;
	[FieldOffset(16)] internal byte Valid;
	[FieldOffset(17)] internal byte Writable;
	[FieldOffset(18)] internal byte UserMode;
	[FieldOffset(20)] readonly uint Pad;
}

[StructLayout(LayoutKind.Explicit)]
public struct KvmSegment {
	[FieldOffset(0)] public ulong Base;
	[FieldOffset(8)] public uint Limit;
	[FieldOffset(12)] public ushort Selector;
	[FieldOffset(14)] public byte Type;
	[FieldOffset(15)] public byte Present;
	[FieldOffset(16)] public byte Dpl;
	[FieldOffset(17)] public byte Db;
	[FieldOffset(18)] public byte S;
	[FieldOffset(19)] public byte L;
	[FieldOffset(20)] public byte G;
	[FieldOffset(21)] public byte Avl;
	[FieldOffset(23)] byte Padding;
}

[StructLayout(LayoutKind.Explicit)]
public struct KvmDTable {
	[FieldOffset(0)] public ulong Base;
	[FieldOffset(8)] public ushort Limit;
	[FieldOffset(15)] byte Padding;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct KvmSregs {
	public KvmSegment Cs, Ds, Es, Fs, Gs, Ss, Tr, Ldt;
	public KvmDTable Gdt, Idt;
	public ulong Cr0, Cr2, Cr3, Cr4, Cr8,
		Efer, ApicBase;
	public fixed ulong InterruptBitmap[4];
}

[StructLayout(LayoutKind.Sequential)]
unsafe struct KvmDebug {
	public ulong Control;
	public fixed ulong DebugReg[8];
}

public unsafe class KvmVcpu {
	public Func<KvmVcpu, ulong, ulong[], ulong> Hypercall;
	
	readonly WrappedFD Fd;
	volatile KvmCpuRun* CpuRun;
	KvmRegs Regs;
	bool DirtyRegs;
	void MakeDirty<T>(T _) => DirtyRegs = true;
	
	public ulong Rax { get => Regs.Rax; set => MakeDirty(Regs.Rax = value); }
	public ulong Rbx { get => Regs.Rbx; set => MakeDirty(Regs.Rbx = value); }
	public ulong Rcx { get => Regs.Rcx; set => MakeDirty(Regs.Rcx = value); }
	public ulong Rdx { get => Regs.Rdx; set => MakeDirty(Regs.Rdx = value); }
	public ulong Rsi { get => Regs.Rsi; set => MakeDirty(Regs.Rsi = value); }
	public ulong Rdi { get => Regs.Rdi; set => MakeDirty(Regs.Rdi = value); }
	public ulong Rsp { get => Regs.Rsp; set => MakeDirty(Regs.Rsp = value); }
	public ulong Rbp { get => Regs.Rbp; set => MakeDirty(Regs.Rbp = value); }
	public ulong R8 { get => Regs.R8; set => MakeDirty(Regs.R8 = value); }
	public ulong R9 { get => Regs.R9; set => MakeDirty(Regs.R9 = value); }
	public ulong R10 { get => Regs.R10; set => MakeDirty(Regs.R10 = value); }
	public ulong R11 { get => Regs.R11; set => MakeDirty(Regs.R11 = value); }
	public ulong R12 { get => Regs.R12; set => MakeDirty(Regs.R12 = value); }
	public ulong R13 { get => Regs.R13; set => MakeDirty(Regs.R13 = value); }
	public ulong R14 { get => Regs.R14; set => MakeDirty(Regs.R14 = value); }
	public ulong R15 { get => Regs.R15; set => MakeDirty(Regs.R15 = value); }
	public ulong Rip { get => Regs.Rip; set => MakeDirty(Regs.Rip = value); }
	public ulong Rflags { get => Regs.Rflags; set => MakeDirty(Regs.Rflags = value); }

	bool SingleStepping;
	public bool SingleStep {
		get => SingleStepping;
		set {
			if(SingleStepping == value) return;
			SingleStepping = value;
			var dbg = new KvmDebug { Control = value ? 0x00000003U : 0 };
			if(value)
				dbg.DebugReg[7] = 0x00000400;
			Ioctl.KVM_SET_GUEST_DEBUG(Fd, dbg);
		}
	}
	
	internal KvmVcpu(int fd) {
		Fd = new(fd);
		var mmapSize = Ioctl.KVM_GET_VCPU_MMAP_SIZE();
		var ptr = Mmap(null, (ulong) mmapSize, 4 | 2, 1, fd, 0);
		if(ptr == null) throw new Exception("Failed to mmap kvm_run!");
		CpuRun = (KvmCpuRun*) ptr;
		Ioctl.KVM_GET_REGS(fd, out Regs);
	}

	internal void Dispose() {
		Fd.Dispose();
		Munmap(CpuRun, (ulong) Ioctl.KVM_GET_VCPU_MMAP_SIZE());
	}

	public (bool Valid, bool UserMode, ulong Physical) Translate(ulong virtaddr) {
		var trans = new KvmTranslate { LinearAddress = virtaddr };
		Ioctl.KVM_TRANSLATE(Fd, ref trans);
		return (trans.Valid != 0, trans.UserMode != 0, trans.PhysicalAddress);
	}

	public ulong SafeTranslate(ulong virtaddr) {
		var (valid, _, phys) = Translate(virtaddr);
		if(!valid) throw new Exception();
		return phys;
	}

	public KvmSregs Sregs {
		get => Ioctl.KVM_GET_SREGS(Fd);
		set => Ioctl.KVM_SET_SREGS(Fd, value);
	}

	public void Run() {
		while(true) {
			if(DirtyRegs) {
				Ioctl.KVM_SET_REGS(Fd, Regs);
				DirtyRegs = false;
			}

			Console.WriteLine($"Exit reason before entry: {CpuRun->ExitReason}");
			Ioctl.KVM_RUN(Fd);
			Ioctl.KVM_GET_REGS(Fd, out Regs);
			Console.WriteLine($"Exited at 0x{Rip:X}");
			switch(CpuRun->ExitReason) {
				case KvmExitReason.Xen:
					ref var xe = ref CpuRun->XenExit;
					Console.WriteLine($"Xen hypercall! {xe.Type} {xe.LongMode} {xe.Input:X}");
					xe.Result = Hypercall?.Invoke(this, xe.Input, Enumerable.Range(0, 6).Select(i => CpuRun->XenExit.Params[i]).ToArray()) ?? 1;
					break;
				default:
					throw new Exception($"Unknown exit reason: {CpuRun->ExitReason}");
			}
		}
	}
	
	[DllImport("libc", EntryPoint = "mmap")]
	static extern void* Mmap(void* addr, ulong length, int prot, int flags, int fd, ulong offset);
	[DllImport("libc", EntryPoint = "munmap")]
	static extern void Munmap(void* addr, ulong length);
}