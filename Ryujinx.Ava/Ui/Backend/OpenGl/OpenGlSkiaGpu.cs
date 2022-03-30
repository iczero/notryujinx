using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Skia;
using Avalonia.X11;
using SkiaSharp;

namespace Ryujinx.Ava.Ui.Backend.OpenGl
{
    public class OpenGlSkiaGpu : ISkiaGpu
    {
        private readonly long? _maxResourceBytes;
        private GRContext _grContext;
        private bool _initialized;
        private GRGlInterface _interface;

        public GRContext GrContext { get => _grContext; set => _grContext = value; }

        public OpenGlSkiaGpu(long? maxResourceBytes)
        {
            _maxResourceBytes = maxResourceBytes;
        }

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            _interface = GRGlInterface.Create();
            _grContext = GRContext.CreateGl(_interface);
            if (_maxResourceBytes.HasValue)
            {
                _grContext.SetResourceCacheLimit(_maxResourceBytes.Value);
            }
        }

        public ISkiaGpuRenderTarget TryCreateRenderTarget(IEnumerable<object> surfaces)
        {
            foreach (var surface in surfaces)
            {
                OpenGlSurface window = null;

                if (surface is IPlatformHandle handle)
                {
                    window = new OpenGlSurface(handle.Handle);
                }
                else if (surface is X11FramebufferSurface x11FramebufferSurface)
                {
                    var xId = (IntPtr)x11FramebufferSurface.GetType().GetField("_xid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(x11FramebufferSurface);

                    window = new OpenGlSurface(xId);
                }

                if(window == null){
                    return null;
                }

                var OpenGlRenderTarget = new OpenGlRenderTarget(window);

                window.MakeCurrent();
                Initialize();
                window.UnsetCurrent();

                OpenGlRenderTarget.GrContext = _grContext;

                return OpenGlRenderTarget;
            }

            return null;
        }

        public ISkiaSurface TryCreateSurface(PixelSize size, ISkiaGpuRenderSession session)
        {
            return null;
        }

    }
}
