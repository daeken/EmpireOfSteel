using Kvm;

namespace CoreEmulator; 

public unsafe class XenStore : IEventChannel {
	readonly XenStoreDomainInterface* Queue;

	public XenStore(XenStoreDomainInterface* queue) => Queue = queue;
	
	internal override void OnTrigger(KvmVcpu cpu) {
		throw new NotImplementedException();
	}
}