using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Am.Controllers;
using Ryujinx.Horizon.Sdk.Sf;

namespace Ryujinx.Horizon.Sdk.Am.Proxies
{
    interface ISystemAppletProxy : IServiceObject
    {
        Result GetCommonStateGetter(out ICommonStateGetter commonStateGetter);
        Result GetSelfController(out ISelfController selfController);
        Result GetWindowController(out IWindowController windowController);
        Result GetAudioController(out IAudioController audioController);
        Result GetDisplayController(out IDisplayController displayController);
        Result GetProcessWindingController(out IProcessWindingController processWindingController);
        Result GetLibraryAppletCreator(out ILibraryAppletCreator libraryAppletCreator);
        Result GetHomeMenuFunctions();
        Result GetGlobalStateController();
        Result GetApplicationCreator();
        Result GetAppletCommonFunctions(out IAppletCommonFunctions commonFunctions);
        Result GetDebugFunctions(out IDebugFunctions debugFunctions);
    }
}
