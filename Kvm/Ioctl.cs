using System.Runtime.InteropServices;

namespace Kvm;

public static unsafe class Ioctl {
	internal static readonly WrappedFD KvmFd = new(open("/dev/kvm", 2));

	static readonly ulong _KVM_GET_API_VERSION        = _IO(KVMIO, 0x00);
	static readonly ulong _KVM_CREATE_VM              = _IO(KVMIO, 0x01);
	static readonly ulong _KVM_GET_VCPU_MMAP_SIZE     = _IO(KVMIO, 0x04);
	static readonly ulong _KVM_CREATE_VCPU            = _IO(KVMIO, 0x41);
	static readonly ulong _KVM_SET_USER_MEMORY_REGION = _IOW<KvmUserspaceMemoryRegion>(KVMIO, 0x46);
	static readonly ulong _KVM_RUN                    = _IO(KVMIO, 0x80);
	static readonly ulong _KVM_GET_REGS               = _IOR<KvmRegs>(KVMIO, 0x81);
	static readonly ulong _KVM_SET_REGS               = _IOW<KvmRegs>(KVMIO, 0x82);
	static readonly ulong _KVM_GET_SREGS              = _IOR<KvmSregs>(KVMIO, 0x83);
	static readonly ulong _KVM_SET_SREGS              = _IOW<KvmSregs>(KVMIO, 0x84);
	static readonly ulong _KVM_TRANSLATE              = _IOWR<KvmTranslate>(KVMIO, 0x85);
	static readonly ulong _KVM_SET_GUEST_DEBUG        = _IOW<KvmDebug>(KVMIO, 0x9b);
	static readonly ulong _KVM_XEN_HVM_CONFIG         = _IOW<KvmXenHvmConfig>(KVMIO, 0x7a);
	static readonly ulong _KVM_GET_SUPPORTED_CPUID    = _IOWR(KVMIO, 5, 8);
	static readonly ulong _KVM_SET_CPUID2             = _IOW(KVMIO, 0x90, 8);
	static readonly ulong _KVM_XEN_HVM_SET_ATTR       = _IOW<KvmXenHvmAttr>(KVMIO, 0xc9);
	static readonly ulong _KVM_XEN_VCPU_SET_ATTR      = _IOW<KvmXenVcpuAttr>(KVMIO, 0xcb);

	const ulong KVMIO = 0xAE;
	
	const int _IOC_NRBITS = 8;
	const int _IOC_TYPEBITS = 8;
	const int _IOC_SIZEBITS = 14;
	const int _IOC_NRSHIFT = 0;
	const int _IOC_TYPESHIFT = _IOC_NRSHIFT + _IOC_NRBITS;
	const int _IOC_SIZESHIFT = _IOC_TYPESHIFT + _IOC_TYPEBITS;
	const int _IOC_DIRSHIFT = _IOC_SIZESHIFT + _IOC_SIZEBITS;

	const ulong _IOC_NONE = 0;
	const ulong _IOC_WRITE = 1;
	const ulong _IOC_READ = 2;
	
	static ulong _IOC(ulong dir, ulong type, ulong nr, ulong size) =>
		(dir  << _IOC_DIRSHIFT) |
		(type << _IOC_TYPESHIFT) |
		(nr   << _IOC_NRSHIFT) |
		(size << _IOC_SIZESHIFT);
	
	static ulong _IO(ulong type, ulong nr) => _IOC(_IOC_NONE, type, nr, 0);
	static ulong _IOW<T>(ulong type, ulong nr) => _IOW(type, nr, (ulong) Marshal.SizeOf<T>());
	static ulong _IOW(ulong type, ulong nr, ulong size) => _IOC(_IOC_WRITE, type, nr, size);
	static ulong _IOR<T>(ulong type, ulong nr) => _IOR(type, nr, (ulong) Marshal.SizeOf<T>());
	static ulong _IOR(ulong type, ulong nr, ulong size) => _IOC(_IOC_READ, type, nr, size);
	static ulong _IOWR<T>(ulong type, ulong nr) => _IOWR(type, nr, (ulong) Marshal.SizeOf<T>());
	static ulong _IOWR(ulong type, ulong nr, ulong size) => _IOC(_IOC_READ | _IOC_WRITE, type, nr, size);
	
	[DllImport("libc", CharSet = CharSet.Ansi)]
	static extern int open(string filename, int flags);

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_GET_API_VERSION(int fd, ulong req, ulong _ = 0);
	internal static int KVM_GET_API_VERSION() => TrapPass(ioctl_KVM_GET_API_VERSION(KvmFd, _KVM_GET_API_VERSION));

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_CREATE_VM(int fd, ulong req, ulong machineType);
	internal static int KVM_CREATE_VM(ulong machineType = 0) => TrapPass(ioctl_KVM_CREATE_VM(KvmFd, _KVM_CREATE_VM, machineType));

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_SET_USER_MEMORY_REGION(int fd, ulong req, in KvmUserspaceMemoryRegion region);
	internal static void KVM_SET_USER_MEMORY_REGION(int fd, in KvmUserspaceMemoryRegion region) => Trap(ioctl_KVM_SET_USER_MEMORY_REGION(fd, _KVM_SET_USER_MEMORY_REGION, region));

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_CREATE_VCPU(int fd, ulong req, ulong id);
	internal static int KVM_CREATE_VCPU(int fd, ulong id) => TrapPass(ioctl_KVM_CREATE_VCPU(fd, _KVM_CREATE_VCPU, id));

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_GET_VCPU_MMAP_SIZE(int fd, ulong req, ulong _ = 0);
	internal static int KVM_GET_VCPU_MMAP_SIZE() => TrapPass(ioctl_KVM_GET_VCPU_MMAP_SIZE(KvmFd, _KVM_GET_VCPU_MMAP_SIZE));

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_GET_REGS(int fd, ulong req, out KvmRegs regs);
	internal static void KVM_GET_REGS(int fd, out KvmRegs regs) => Trap(ioctl_KVM_GET_REGS(fd, _KVM_GET_REGS, out regs));

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_SET_SREGS(int fd, ulong req, in KvmSregs regs);
	internal static void KVM_SET_SREGS(int fd, in KvmSregs regs) => Trap(ioctl_KVM_SET_SREGS(fd, _KVM_SET_SREGS, regs));
	
	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_GET_SREGS(int fd, ulong req, out KvmSregs regs);
	internal static void KVM_GET_SREGS(int fd, out KvmSregs regs) => Trap(ioctl_KVM_GET_SREGS(fd, _KVM_GET_SREGS, out regs));
	internal static KvmSregs KVM_GET_SREGS(int fd) {
		KVM_GET_SREGS(fd, out var sregs);
		return sregs;
	}

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_SET_REGS(int fd, ulong req, in KvmRegs regs);
	internal static void KVM_SET_REGS(int fd, in KvmRegs regs) => Trap(ioctl_KVM_SET_REGS(fd, _KVM_SET_REGS, regs));
	
	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_RUN(int fd, ulong req, ulong _ = 0);
	internal static void KVM_RUN(int fd) => Trap(ioctl_KVM_RUN(fd, _KVM_RUN));

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_TRANSLATE(int fd, ulong req, ref KvmTranslate translate);
	internal static void KVM_TRANSLATE(int fd, ref KvmTranslate translate) => Trap(ioctl_KVM_TRANSLATE(fd, _KVM_TRANSLATE, ref translate));
	
	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_SET_GUEST_DEBUG(int fd, ulong req, in KvmDebug debug);
	internal static void KVM_SET_GUEST_DEBUG(int fd, in KvmDebug debug) => Trap(ioctl_KVM_SET_GUEST_DEBUG(fd, _KVM_SET_GUEST_DEBUG, debug));
	
	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_XEN_HVM_CONFIG(int fd, ulong req, in KvmXenHvmConfig debug);
	internal static void KVM_XEN_HVM_CONFIG(int fd, in KvmXenHvmConfig config) => Trap(ioctl_KVM_XEN_HVM_CONFIG(fd, _KVM_XEN_HVM_CONFIG, config));
	
	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_GET_SUPPORTED_CPUID(int fd, ulong req, void* str);
	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_SET_CPUID2(int fd, ulong req, void* str);

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_XEN_HVM_SET_ATTR(int fd, ulong req, in KvmXenHvmAttr attr);
	internal static void KVM_XEN_HVM_SET_ATTR(int fd, in KvmXenHvmAttr attr) => Trap(ioctl_KVM_XEN_HVM_SET_ATTR(fd, _KVM_XEN_HVM_SET_ATTR, attr));

	[DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ioctl", SetLastError = true)]
	static extern int ioctl_KVM_XEN_VCPU_SET_ATTR(int fd, ulong req, in KvmXenVcpuAttr attr);
	internal static void KVM_XEN_VCPU_SET_ATTR(int fd, in KvmXenVcpuAttr attr) => Trap(ioctl_KVM_XEN_VCPU_SET_ATTR(fd, _KVM_XEN_VCPU_SET_ATTR, attr));
	
	internal static void SetCpuid(int fd) {
		fixed(uint* buf = new uint[2 + 10 * 100]) {
			buf[0] = 100;
			Trap(ioctl_KVM_GET_SUPPORTED_CPUID(KvmFd, _KVM_GET_SUPPORTED_CPUID, buf));
			Trap(ioctl_KVM_GET_SUPPORTED_CPUID(KvmFd, _KVM_GET_SUPPORTED_CPUID, buf));
			Trap(ioctl_KVM_SET_CPUID2(fd, _KVM_SET_CPUID2, buf));
		}
	}
	
	[DllImport("libc")]
	static extern IntPtr strerror(int errno);

	static T TrapPass<T>(T value) {
		var errno = Marshal.GetLastPInvokeError();
		if(errno != 0)
			throw new Exception($"Got errno {errno}: {Marshal.PtrToStringAnsi(strerror(errno))}");
		return value;
	}

	static void Trap(int ret) {
		var errno = Marshal.GetLastPInvokeError();
		if(errno != 0)
			throw new Exception($"Got errno {errno}: {Marshal.PtrToStringAnsi(strerror(errno))}");
		if(ret != 0) throw new Exception($"Call failed with return value {ret}");
	}
}