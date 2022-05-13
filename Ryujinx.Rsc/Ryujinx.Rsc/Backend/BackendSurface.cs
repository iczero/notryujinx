using Avalonia;
using Avalonia.Platform;
using System;
using System.Reflection.Metadata;

namespace Ryujinx.Rsc.Backend
{
    public abstract class BackendSurface : IDisposable
    {
        public IPlatformNativeSurfaceHandle Handle { get; }

        public bool IsDisposed { get; private set; }

        public BackendSurface(IPlatformNativeSurfaceHandle handle)
        {
            Handle = handle;
        }

        public PixelSize Size => Handle.Size;

        public virtual void Dispose()
        {
            IsDisposed = true;
        }
    }
}