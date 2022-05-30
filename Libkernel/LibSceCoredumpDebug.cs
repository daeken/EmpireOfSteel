namespace Libkernel;
using Common;

[Library("libSceCoredump_debug")]
public static class LibSceCoredumpDebug {
	[Export("1Pw5n31Ayxc")]
	public static void SceCoredumpDebugForceCoredumpOnAppClose() => throw new NotImplementedException();
	[Export("G420P25pN5Y")]
	public static void SceCoredumpDebugTriggerCoredump() => throw new NotImplementedException();
}
