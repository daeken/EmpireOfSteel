using Kvm;

namespace CoreEmulator; 

public abstract class IEventChannel {
	internal uint Port { get; set; }
	
	internal abstract void OnTrigger(KvmVcpu vcpu);
	
	void Trigger(KvmVcpu vcpu) => vcpu.TriggerEventChannel(Port, 0);
}