namespace Kvm; 

using System;
using System.Runtime.InteropServices;

public class WrappedFD : IDisposable {
	readonly int FD;

	public WrappedFD(int fd) => FD = fd;

	public void Dispose() => close(FD);

	[DllImport("libc")]
	static extern void close(int fd);
	
	public static implicit operator int(WrappedFD fd) => fd.FD;
}
