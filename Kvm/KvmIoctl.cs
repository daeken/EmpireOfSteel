using System.Runtime.InteropServices;

namespace Kvm;

public static class KvmIoctl {
	public static readonly ulong KVM_GET_API_VERSION        = _IO(KVMIO, 0x00);
	public static readonly ulong KVM_CREATE_VM              = _IO(KVMIO, 0x01);
	public static readonly ulong KVM_SET_USER_MEMORY_REGION = _IOW<KvmUserspaceMemoryRegion>(KVMIO, 0x46);

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
	static ulong _IOW<T>(ulong type, ulong nr) => _IOC(_IOC_WRITE, type, nr, (ulong) Marshal.SizeOf<T>());
}