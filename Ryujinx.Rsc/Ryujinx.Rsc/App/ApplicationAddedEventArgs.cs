using System;

namespace Ryujinx.Rsc.Library.Common
{
    public class ApplicationAddedEventArgs : EventArgs
    {
        public ApplicationData AppData { get; set; }
    }
}