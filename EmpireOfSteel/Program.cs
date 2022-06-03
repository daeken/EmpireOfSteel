using Kvm;

using var vm = new KvmVm();
using var bm = vm.Map(0, 8UL * 1024 * 1024 * 1024);
var vcpu = vm.CreateVcpu();

bm.AsSpan<byte>()[0] = 0xF4;
bm.AsSpan<byte>()[1] = 0xCC;

var gdtBase = 0x1000UL;
var gdt = bm.AsSpan<ulong>(gdtBase);
gdt[0] = 0;
gdt[1] = 0x00209A0000000000;
gdt[2] = 0x0000920000000000;

var pml4Base = 0x2000UL;
var pdpBase = 0x3000UL;
var pdBase = 0x4000UL;

var pml4 = bm.AsSpan<ulong>(pml4Base);
pml4[0] = pdpBase | 0b11;

var pdp = bm.AsSpan<ulong>(pdpBase);
for(var i = 0UL; i < 4; ++i) {
	var physAddr = 0x4000_0000UL * i;
	var pdAddr = pdBase + 0x1000UL * i;
	pdp[(int) i] = pdAddr | 0b11;
	var pd = bm.AsSpan<ulong>(pdAddr);
	for(var j = 0; j < 512; ++j, physAddr += 0x200000) {
		pd[j] = physAddr | 0b10000011; // 2MB page size!
	}
}

var sregs = vcpu.Sregs;
sregs.Gdt.Base = gdtBase;
sregs.Gdt.Limit = 24;

// Zero-length IDT so any NMI triple-faults
sregs.Idt.Base = 0;
sregs.Idt.Limit = 0;

sregs.Efer |= 0x0500; // LMA | LME
sregs.Cr0 |= 0x80000001; // Paging and protection
sregs.Cr3 = pml4Base;
sregs.Cr4 = 0b10100000; // PAE and PGE enabled

sregs.Cs.Base = sregs.Ds.Base = sregs.Es.Base = sregs.Fs.Base = sregs.Gs.Base = sregs.Ss.Base = 0;
sregs.Cs.Limit = sregs.Ds.Limit = sregs.Es.Limit = sregs.Fs.Limit = sregs.Gs.Limit = sregs.Ss.Limit = 0xFFFFFFFF;
sregs.Cs.G = sregs.Ds.G = sregs.Es.G = sregs.Fs.G = sregs.Gs.G = sregs.Ss.G = 1;
sregs.Cs.Db = sregs.Ss.Db = 1;

vcpu.Sregs = sregs;

var kernBase = 2UL * 1024 * 1024;
var kernSpace = bm.AsSpan<byte>(kernBase);
var kernData = File.ReadAllBytes("kernel");
kernData.CopyTo(kernSpace);

vcpu.Rip = kernBase + 0x185000; // TODO: Actually read entrypoint from kernel (symbol: btext)

bm.AsSpan<byte>(kernBase + 0xe8fd37)[0] = 0xF4;
bm.AsSpan<byte>(kernBase + 0xe8f0f0)[0] = 0xF4;

Console.WriteLine($"Starting at 0x{vcpu.Rip:X}");

var tstackPointer = 0x100000UL - 12;
var tstack = bm.AsSpan<uint>(tstackPointer);

tstack[1] = (uint) kernBase;
tstack[2] = (uint) kernBase + (uint) kernData.Length;

vcpu.Rsp = tstackPointer;

vcpu.Run();