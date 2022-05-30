namespace Libkernel;
using Common;

[Library("libkernel_module_load_check")]
public static class LibkernelModuleLoadCheck {
	[Export("21+rb7xOlJk")]
	public static void SceKernelIsModuleLoaded() => throw new NotImplementedException();
}
