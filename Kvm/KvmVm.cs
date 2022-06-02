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
	readonly WrappedFD KvmFd = new(open("/dev/kvm", 2));
	readonly WrappedFD VmFd;
	readonly Dictionary<ulong, uint> MemorySlots = new();
	readonly Stack<uint> FreeSlots = new();

	public KvmVm() {
		var version = TrapErrno(() => ioctl_KVM_GET_API_VERSION(KvmFd, KvmIoctl.KVM_GET_API_VERSION));
		if(version != 12)
			throw new Exception($"Unsupported KVM API version {version}!");
		VmFd = new(TrapErrno(() => ioctl_KVM_CREATE_VM(KvmFd, KvmIoctl.KVM_CREATE_VM, 0)));
	}

	~KvmVm() => Dispose();

	public void Dispose() {
		KvmFd?.Dispose();
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
		if(TrapErrno(() => ioctl_KVM_SET_USER_MEMORY_REGION(VmFd, KvmIoctl.KVM_SET_USER_MEMORY_REGION, ref region)) != 0)
			throw new Exception($"Could not map memory at guest 0x{guestPhysAddr:X}");
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
		if(TrapErrno(() => ioctl_KVM_SET_USER_MEMORY_REGION(VmFd, KvmIoctl.KVM_SET_USER_MEMORY_REGION, ref region)) != 0)
			throw new Exception($"Could not unmap memory at guest 0x{guestPhysAddr:X}");
	}

	public KvmVcpu CreateVcpu() {
		throw new NotImplementedException();
	}
	
	[DllImport("libc", CharSet = CharSet.Ansi)]
	static extern int open(string filename, int flags);

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_GET_API_VERSION(int fd, ulong req, ulong _ = 0);

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_CREATE_VM(int fd, ulong req, ulong machineType);

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_SET_USER_MEMORY_REGION(int fd, ulong req, ref KvmUserspaceMemoryRegion region);

	[DllImport("libc")]
	static extern IntPtr strerror(int errno);

	static int TrapErrno(Func<int> func) {
		var ret = func();
		var errno = Marshal.GetLastPInvokeError();
		if(errno != 0)
			throw new Exception($"Got errno {errno}: {Marshal.PtrToStringAnsi(strerror(errno))}");
		return ret;
	}
}