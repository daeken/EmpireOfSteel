using System.Runtime.InteropServices;
using System.Text;
using Kvm;
// ReSharper disable VariableHidesOuterVariable

using var vm = new KvmVm();
using var bm = vm.Map(0, 8UL * 1024 * 1024 * 1024);
var vcpu = vm.CreateVcpu();

bm.AsSpan<byte>()[0] = 0xF4;
bm.AsSpan<byte>()[1] = 0xCC;

var pml4Base = 0x2000UL;
var pdpBase = 0x3000UL;
var pdBase = 0x4000UL;

var pml4 = bm.AsSpan<ulong>(pml4Base);
pml4[0] = pdpBase | 0b11;
pml4[0b111111111] = pdpBase | 0b11;

var pdp = bm.AsSpan<ulong>(pdpBase);
var physAddr = 0UL;
pdp[0] = pdBase | 0b11;
pdp[0b111111110] = pdBase | 0b11;
var pd = bm.AsSpan<ulong>(pdBase);
for(var j = 0; j < 512; ++j, physAddr += 0x200000)
	pd[j] = physAddr | 0b10000011; // 2MB page size!

var gdtBase = 0x1000UL;
var gdt = bm.AsSpan<ulong>(gdtBase);
gdt[0] = 0;
gdt[1] = 0x00209A0000000000;
gdt[2] = 0x0000920000000000;

var sregs = vcpu.Sregs;
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

vcpu.Sregs = sregs;

var kernSpace = bm.AsSpan<byte>(0x200000); // Add another 2MB because ... reasons?
var kernData = File.ReadAllBytes("kernel");
kernData.CopyTo(kernSpace);

vcpu.Rip = 0xffffffff8108f000; // TODO: Actually read xen_start (XEN_ELFNOTE_ENTRY)

var (valid, _, addr) = vcpu.Translate(vcpu.Rip);
Console.WriteLine($"0x{vcpu.Rip:X} at physical 0x{addr:X} (valid {valid})");

Console.WriteLine($"Starting at 0x{vcpu.Rip:X}");

var hypercallPage = 0xffffffff8108e000UL; // TODO: Actually read XEN_ELFNOTE_HYPERCALL_PAGE
var hp = bm.AsSpan<byte>(vcpu.SafeTranslate(hypercallPage));
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
var _startInfo = bm.AsSpan<StartInfo>(startInfoBase);
ref var startInfo = ref _startInfo[0];
unsafe {
	var magic = Encoding.ASCII.GetBytes("{Empire Of Steel}");
	fixed(byte* magicptr = startInfo.Magic)
		magic.CopyTo(new Span<byte>(magicptr, 32));
}
startInfo.Version = 1;
startInfo.Flags = 0;
startInfo.NumModules = 0;
startInfo.ModlistAddr = 0;
startInfo.CmdlineAddr = 0x2000UL;
startInfo.RsdpAddr = 0;
startInfo.MemMapAddr = 0;
startInfo.MemMapEntries = 0;

vcpu.Rsi = startInfoBase;
vcpu.Rsp = 0x100000UL;

vcpu.Hypercall = Hypercall;

vcpu.Run();

ulong Hypercall(KvmVcpu cpu, ulong num, ulong[] args) {
	switch((XenHypercall) num) {
		case XenHypercall.ConsoleIo: {
			if(args[0] == 0) { // Write!
				var (size, addr) = (args[1], args[2]);
				var buf = bm.AsSpan<byte>(cpu.SafeTranslate(addr))[..(int) size];
				var str = Encoding.ASCII.GetString(buf).TrimEnd();
				Console.WriteLine($"Message from kernel via Xen ConsoleIO: '{str}'");
			}
			break;
		}
		case {} unk:
			throw new Exception($"Unhandled Xen hypercall: {unk}");
	}
	return 0;
}

enum XenHypercall {
	SetTrapTable = 0,
	MmuUpdate = 1,
	SetGdt = 2,
	StackSwitch = 3,
	SetCallbacks = 4,
	FpuTaskswitch = 5,
	SchedOpCompat = 6,
	PlatformOp = 7,
	SetDebugReg = 8,
	GetDebugReg = 9,
	UpdateDescriptor = 10,
	MemoryOp = 12,
	Multicall = 13,
	UpdateVaMapping = 14,
	SetTimerOp = 15,
	EventChannelOpCompat = 16,
	XenVersion = 17,
	ConsoleIo = 18,
	PhysdevOpCompat = 19,
	GrantTableOp = 20,
	VmAssist = 21,
	UpdateVaMappingOtherDomain = 22,
	Iret = 23,
	VcpuOp = 24,
	SetSegmentBase = 25,
	MmuExtOp = 26,
	XsmOp = 27,
	NmiOp = 28,
	SchedOp = 29,
	CallbackOp = 30,
	XenoprofOp = 31,
	EventChannelOp = 32,
	PhysdevOp = 33,
	HvmOp = 34,
	Sysctl = 35,
	Domctl = 36,
	KexecOp = 37,
	TmemOp = 38,
	ArgoOp = 39,
	XenPmuOp = 40,
	DmOp = 41,
	HypfsOp = 42,
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
unsafe struct StartInfo {
	public fixed byte Magic[32];
	public uint Version, Flags, NumModules;
	public ulong ModlistAddr, CmdlineAddr, RsdpAddr, MemMapAddr;
	public uint MemMapEntries;
	uint Reserved;
}