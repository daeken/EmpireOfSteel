using Kvm;

using var vm = new KvmVm();
using var bm = vm.Map(0, 8UL * 1024 * 1024 * 1024);
var bytes = bm.AsSpan<byte>();
var uints = bm.AsSpan<uint>();
uints[0] = 0xDEADBEEF;
for(var i = 0; i < 4; ++i)
	Console.WriteLine($"{bytes[i]:X}");