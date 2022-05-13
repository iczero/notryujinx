using System;

namespace Ryujinx.Rsc.Library
{
    public class ApplicationCountUpdatedEventArgs : EventArgs
    {
        public int NumAppsFound  { get; set; }
        public int NumAppsLoaded { get; set; }
    }
}