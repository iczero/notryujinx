using Avalonia;
using Avalonia.Platform;
using System;
using static Ryujinx.Ava.Ui.Backend.Interop;

namespace Ryujinx.Ava.Ui.Backend
{
    public abstract class BackendSurface : IDisposable
    {
        protected IntPtr Display { get; set; } = IntPtr.Zero;
        private PixelSize _currentSize;
        public IntPtr Handle { get; protected set; }

        public bool IsDisposed { get; private set; }

        public BackendSurface(IntPtr handle)
        {
            Handle = handle;
            if(OperatingSystem.IsLinux())
            {
                Display = XOpenDisplay(IntPtr.Zero);
            }
        }

        public PixelSize Size
        {
            get
            {
                PixelSize size = new PixelSize();
                if (OperatingSystem.IsWindows())
                {
                    GetClientRect(Handle, out var rect);
                    size = new PixelSize(rect.right, rect.bottom);
                }
                else if (OperatingSystem.IsLinux())
                {
                    XWindowAttributes attributes = new XWindowAttributes();
                    XGetWindowAttributes(Display, Handle, ref attributes);

                    size = new PixelSize(attributes.width, attributes.height);
                }

                _currentSize = size;

                return size;
            }
        }

        public PixelSize CurrentSize => _currentSize;


        public virtual void Dispose()
        {
            IsDisposed = true;
            if (Display != IntPtr.Zero)
            {
                XCloseDisplay(Display);
                Display = IntPtr.Zero;
            }
        }
    }
}