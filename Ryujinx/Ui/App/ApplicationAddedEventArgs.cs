using System;

namespace Ryujinx.Rsc.Library
{
    public class ApplicationAddedEventArgs : EventArgs
    {
        public ApplicationData AppData { get; set; }
    }
}