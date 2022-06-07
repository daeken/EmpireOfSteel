namespace Kvm; 

using System;
using System.Runtime.InteropServices;

[Flags]
public enum MemoryFlags {
	Read = 1, 
	Write = 2, 
	Exec = 4, 
	RW = Read | Write, 
	RX = Read | Exec, 
	WX = Write | Exec, 
	RWX = Read | Write | Exec, 
}

[StructLayout(LayoutKind.Sequential)]
struct KvmUserspaceMemoryRegion {
	public uint Slot, Flags;
	public ulong GuestPhysAddr, MemorySize;
	public IntPtr UserspaceAddr;
}

[StructLayout(LayoutKind.Explicit)]
struct KvmXenHvmConfig {
	[FieldOffset(0)] public uint Flags;
	[FieldOffset(4)] public uint Msr;
	[FieldOffset(8)] public ulong BlobAddr32;
	[FieldOffset(16)] public ulong BlobAddr64;
	[FieldOffset(24)] public byte BlobSize32;
	[FieldOffset(25)] public byte BlobSize64;
	[FieldOffset(55)] byte Pad;
}

enum KvmXenHvmAttrType : ushort {
	LongMode,
	SharedInfo,
	UpcallVector,
}

[StructLayout(LayoutKind.Explicit, Size = 72)]
struct KvmXenHvmAttr {
	[FieldOffset(0)] public KvmXenHvmAttrType Type;
	[FieldOffset(8)] public byte LongMode;
	[FieldOffset(8)] public byte Vector;
	[FieldOffset(8)] public ulong SharedInfoGfn;
}

[StructLayout(LayoutKind.Sequential)]
struct KvmIrqRoutingXenEvtChn {
	public uint Port, Vcpu, Priority;
}

public unsafe class KvmVm : IDisposable {
	public ISystem System;
	internal readonly WrappedFD VmFd;
	readonly Dictionary<ulong, uint> MemorySlots = new();
	readonly Stack<uint> FreeSlots = new();
	readonly List<KvmVcpu> Vcpus = new();

	public KvmVm() {
		var version = Ioctl.KVM_GET_API_VERSION();
		if(version != 12)
			throw new Exception($"Unsupported KVM API version {version}!");
		VmFd = new(Ioctl.KVM_CREATE_VM());
		
		Ioctl.KVM_XEN_HVM_CONFIG(VmFd, new() {
			Flags = (1 << 1), // KVM_XEN_HVM_CONFIG_INTERCEPT_HCALL
			Msr = 0xDEADBEEF, 
		});
	}

	public bool XenLongMode {
		set => Ioctl.KVM_XEN_HVM_SET_ATTR(VmFd, new() { Type = KvmXenHvmAttrType.LongMode, LongMode = (byte) (value ? 1 : 0) });
	}

	public ulong XenSharedInfo {
		set => Ioctl.KVM_XEN_HVM_SET_ATTR(VmFd, new() { Type = KvmXenHvmAttrType.SharedInfo, SharedInfoGfn = value });
	}

	~KvmVm() => Dispose();

	public void Dispose() {
		Vcpus.ForEach(x => x.Dispose());
		VmFd?.Dispose();
	}

	public BoundMemory Map(ulong guestPhysAddr, ulong size) {
		var bm = new BoundMemory(size, () => Unmap(guestPhysAddr));
		Map(bm.Pointer, guestPhysAddr, size);
		return bm;
	}

	public void Map(IntPtr hostAddress, ulong guestPhysAddr, ulong size) {
		var slot = FreeSlots.TryPop(out var _slot) ? _slot : (uint) MemorySlots.Count;
		MemorySlots[guestPhysAddr] = slot;
		var region = new KvmUserspaceMemoryRegion {
			Slot = slot, 
			Flags = 0,
			GuestPhysAddr = guestPhysAddr, 
			MemorySize = size, 
			UserspaceAddr = hostAddress
		};
		Ioctl.KVM_SET_USER_MEMORY_REGION(VmFd, region);
	}

	public void Unmap(ulong guestPhysAddr) {
		if(!MemorySlots.TryGetValue(guestPhysAddr, out var slot)) return;
		var region = new KvmUserspaceMemoryRegion {
			Slot = slot, 
			Flags = 0, 
			GuestPhysAddr = 0, 
			MemorySize = 0, 
			UserspaceAddr = IntPtr.Zero
		};
		Ioctl.KVM_SET_USER_MEMORY_REGION(VmFd, region);
	}

	public KvmVcpu CreateVcpu() {
		var fd = Ioctl.KVM_CREATE_VCPU(VmFd, (ulong) Vcpus.Count);
		if(fd == -1) return null;
		var vcpu = new KvmVcpu(this, fd, Vcpus.Count);
		Vcpus.Add(vcpu);
		return vcpu;
	}
}