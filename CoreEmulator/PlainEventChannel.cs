using Kvm;

namespace CoreEmulator; 

public class PlainEventChannel : IEventChannel {
	internal override void OnTrigger(KvmVcpu vcpu) {
		throw new NotImplementedException();
	}
}