using System.Diagnostics;
using Kvm;

namespace CoreEmulator; 

public static class Timer {
	static readonly SortedList<ulong, KvmVcpu> Timeouts = new();
	static readonly AutoResetEvent WakeUp = new(false);
	static readonly Stopwatch Stopwatch = new();
	static ulong NextStop = ulong.MaxValue;
	static ulong CurrentTime => (ulong) Stopwatch.ElapsedMilliseconds;

	public static void Run() => Task.Factory.StartNew(Loop);

	public static void AddTimeout(ulong delta, KvmVcpu cpu) {
		lock(Timeouts) {
			var time = CurrentTime + delta;
			Timeouts.Add(time, cpu);
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
						var (next, cpu) = Timeouts.First();
						if(next > cur) break;
						Timeouts.RemoveAt(0);
						Console.WriteLine($"Vcpu {cpu} needs wakeup");
					}
					NextStop = Timeouts.Count == 0 ? ulong.MaxValue : Timeouts.First().Key;
				}
			else
				Thread.Sleep(10);
		}
	}
}