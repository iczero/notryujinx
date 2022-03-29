using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Skia;
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

        public static ISkiaGpu CreateGpu()
        {
            var skiaOptions = AvaloniaLocator.Current.GetService<SkiaOptions>() ?? new SkiaOptions();
            var gpu = new OpenGlSkiaGpu(skiaOptions.MaxGpuResourceSizeBytes);
            AvaloniaLocator.CurrentMutable.Bind<OpenGlSkiaGpu>().ToConstant(gpu);

            return gpu;
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
                if (surface is IPlatformHandle handle)
                {
                    var window = new OpenGlSurface(handle);

                    var OpenGlRenderTarget = new OpenGlRenderTarget(window);

                    window.MakeCurrent();
                    Initialize();
                    window.UnsetCurrent();

                    OpenGlRenderTarget.GrContext = _grContext;

                    return OpenGlRenderTarget;
                }
            }

            return null;
        }

        public ISkiaSurface TryCreateSurface(PixelSize size, ISkiaGpuRenderSession session)
        {
            return null;
        }

    }
}
