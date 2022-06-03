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

public unsafe class KvmVm : IDisposable {
	readonly WrappedFD VmFd;
	readonly Dictionary<ulong, uint> MemorySlots = new();
	readonly Stack<uint> FreeSlots = new();
	readonly List<KvmVcpu> Vcpus = new();

	public KvmVm() {
		var version = Ioctl.KVM_GET_API_VERSION();
		if(version != 12)
			throw new Exception($"Unsupported KVM API version {version}!");
		VmFd = new(Ioctl.KVM_CREATE_VM());
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
		var vcpu = new KvmVcpu(fd);
		Vcpus.Add(vcpu);
		return vcpu;
	}
}