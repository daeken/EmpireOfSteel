namespace Kvm; 

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public unsafe class BoundMemory : IDisposable {
	public readonly void* Memory;
	internal readonly IntPtr Pointer, OrigPointer;
	public readonly ulong Size;
	readonly Action OnDispose;
		
	internal unsafe BoundMemory(ulong size, Action onDispose) {
		Size = size;
		if((size & 0x3FFF) != 0) throw new ArgumentException("BoundMemory size must be a multiple of page size");
		OrigPointer = Pointer = Marshal.AllocHGlobal((IntPtr) size + 0x3FFF);
		var tptr = (ulong) Pointer;
		if((tptr & 0x3FFF) != 0)
			tptr = (tptr & ~0x3FFFUL) + 0x4000;
		Pointer = (IntPtr) tptr;
		Memory = (void*) Pointer;
		OnDispose = onDispose;
	}

	~BoundMemory() => Dispose(false);

	public Span<T> AsSpan<T>(ulong offset = 0) where T : struct => new((void*) ((ulong) Memory + offset), (int) Math.Min(Size - offset, int.MaxValue));

	void Dispose(bool disposing) {
		OnDispose();
		if(disposing) Marshal.FreeHGlobal(OrigPointer);
	}

	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}
}
