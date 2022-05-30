namespace Libkernel;
using Common;

[Library("libkernel_pre250mmap")]
public static class LibkernelPre250mmap {
	[Export("2SKEx6bSq-4")]
	public static void SceKernelBatchMap() => throw new NotImplementedException();
	[Export("L-Q3LEjIbgA")]
	public static void SceKernelMapDirectMemory() => throw new NotImplementedException();
	[Export("NcaWUxfMNIQ")]
	public static void SceKernelMapNamedDirectMemory() => throw new NotImplementedException();
}
