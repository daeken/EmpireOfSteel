using System.Runtime.InteropServices;

namespace CoreEmulator;

enum XenHypercall {
	SetTrapTable = 0,
	MmuUpdate = 1,
	SetGdt = 2,
	StackSwitch = 3,
	SetCallbacks = 4,
	FpuTaskswitch = 5,
	SchedOpCompat = 6,
	PlatformOp = 7,
	SetDebugReg = 8,
	GetDebugReg = 9,
	UpdateDescriptor = 10,
	MemoryOp = 12,
	Multicall = 13,
	UpdateVaMapping = 14,
	SetTimerOp = 15,
	EventChannelOpCompat = 16,
	XenVersion = 17,
	ConsoleIo = 18,
	PhysdevOpCompat = 19,
	GrantTableOp = 20,
	VmAssist = 21,
	UpdateVaMappingOtherDomain = 22,
	Iret = 23,
	VcpuOp = 24,
	SetSegmentBase = 25,
	MmuExtOp = 26,
	XsmOp = 27,
	NmiOp = 28,
	SchedOp = 29,
	CallbackOp = 30,
	XenoprofOp = 31,
	EventChannelOp = 32,
	PhysdevOp = 33,
	HvmOp = 34,
	Sysctl = 35,
	Domctl = 36,
	KexecOp = 37,
	TmemOp = 38,
	ArgoOp = 39,
	XenPmuOp = 40,
	DmOp = 41,
	HypfsOp = 42,
}

[Flags]
enum XenSif : uint {
	Privileged = 1 << 0,
	InitDomain = 1 << 1, 
	MultibootMod = 1 << 2, 
	ModStartPfn = 1 << 3, 
	VirtP2M4Tools = 1 << 4,
}

[StructLayout(LayoutKind.Explicit)]
unsafe struct StartInfo {
	[FieldOffset(0)] public fixed byte Magic[32];
	[FieldOffset(32)] public ulong NumPages;
	[FieldOffset(40)] public ulong SharedInfo;
	[FieldOffset(48)] public XenSif Flags;
	[FieldOffset(56)] public ulong StoreMfn;
	[FieldOffset(64)] public uint StoreEvtChn;
	[FieldOffset(72)] public ulong ConsoleMfn;
	[FieldOffset(80)] public uint ConsoleEvtChn;
	[FieldOffset(88)] public ulong PtBase;
	[FieldOffset(96)] public ulong NumPtFrames;
	[FieldOffset(104)] public ulong MfnList;
	[FieldOffset(112)] public ulong ModStart;
	[FieldOffset(120)] public ulong ModLen;
	[FieldOffset(128)] public fixed byte CmdLine[1024];
	[FieldOffset(128 + 1024)] public ulong FirstP2MPfn;
	[FieldOffset(136 + 1024)] public ulong NumP2MFrames;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct XenStoreDomainInterface {
	public fixed byte Req[1024];
	public fixed byte Rsp[1024];
	public uint ReqCons, ReqProd, RspCons, RspProd;
}

[StructLayout(LayoutKind.Sequential)]
public struct XenEventChnAllocUnbound {
	public ushort Dom, RemoteDom;
	public uint OutPort;
}

public enum XenVcpuOp {
	Initialize,
	Up,
	Down,
	IsUp,
	GetRunstateInfo,
	RegisterRunstateMemoryArea,
	SetPeriodicTimer,
	StopPeriodicTimer,
	SetSingleshotTimer,
	StopSingleshotTimer,
	RegisterVcpuInfo,
}
