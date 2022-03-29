using System;

namespace Ryujinx.Ava.Ui.Backend.OpenGl
{
    public class OpenGlSurfaceRenderingSession : IDisposable
    {
        private readonly OpenGlSurface _window;

        public OpenGlSurfaceRenderingSession(OpenGlSurface window, float scaling)
        {
            _window = window;
            Scaling = scaling;
            Begin();
        }

        public PixelSize Size => _window.Size;

        public float Scaling { get; }

        public bool IsYFlipped { get; } = true;

        public void Dispose()
        {
            _window.SwapBuffers();
            _window.UnsetCurrent();
        }

        private void Begin()
        {
            _window.MakeCurrent();
        }
    }
}
