using System.Diagnostics;
using Kvm;

namespace CoreEmulator; 

public static class Timer {
	static readonly SortedList<ulong, HashSet<KvmVcpu>> Timeouts = new();
	static readonly AutoResetEvent WakeUp = new(false);
	static readonly Stopwatch Stopwatch = Stopwatch.StartNew();
	static ulong NextStop = ulong.MaxValue;
	static ulong CurrentTime => (ulong) Stopwatch.ElapsedMilliseconds;

	public static void Run() => Task.Factory.StartNew(Loop);

	public static void AddTimeout(ulong delta, KvmVcpu cpu) => AddAbsoluteTimeout(CurrentTime + delta, cpu);

	public static void AddAbsoluteTimeout(ulong time, KvmVcpu cpu) {
		lock(Timeouts) {
			if(Timeouts.TryGetValue(time, out var set))
				set.Add(cpu);
			else
				Timeouts.Add(time, new() { cpu });
			var wake = NextStop == ulong.MaxValue;
			if(time < NextStop) NextStop = time;
			if(wake) WakeUp.Set();
		}
	}

	static void Loop() {
		while(true) {
			if(NextStop == ulong.MaxValue) {
				WakeUp.WaitOne();
				continue;
			}
			var cur = CurrentTime;
			if(NextStop <= cur)
				lock(Timeouts) {
					while(Timeouts.Count != 0) {
						var (next, set) = Timeouts.First();
						if(next > cur) break;
						Timeouts.RemoveAt(0);
						//foreach(var cpu in set)
						//	cpu.TriggerTimer();
					}
					NextStop = Timeouts.Count == 0 ? ulong.MaxValue : Timeouts.First().Key;
				}
			else
				Thread.Sleep(10);
		}
	}
}