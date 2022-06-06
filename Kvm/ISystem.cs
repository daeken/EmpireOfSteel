namespace Kvm; 

public interface ISystem {
	ulong Hypercall(KvmVcpu cpu, ulong num, ulong[] args);
	void PortIo(KvmVcpu cpu, int port, bool write, Span<byte> data);
}