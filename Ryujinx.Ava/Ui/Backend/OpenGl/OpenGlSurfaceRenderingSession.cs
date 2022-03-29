using Avalonia;
using OpenTK.Graphics.OpenGL;
using System;
using System.Threading;

namespace Ryujinx.Ava.Ui.Backend.OpenGl
{
    public class OpenGlSurfaceRenderingSession : IDisposable
    {
        private readonly OpenGlSurface _window;

        public bool IsValid { get; set; }

        public OpenGlSurfaceRenderingSession(OpenGlSurface window, float scaling)
        {
            _window = window;
            Scaling = scaling;
            _window.MakeCurrent();
        }

        public int Framebuffer => _window.Framebuffer;

        public PixelSize Size => _window.Size;

        public PixelSize CurrentSize => _window.CurrentSize;

        public float Scaling { get; }

        public bool IsYFlipped { get; } = false;

        public void Dispose()
        {
            if (IsValid)
            {
                var size = _window.CurrentSize;

                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, Framebuffer);
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
                GL.BlitFramebuffer(0,
                    0,
                    size.Width,
                    size.Height,
                    0,
                    0,
                    size.Width,
                    size.Height,
                    ClearBufferMask.ColorBufferBit,
                    BlitFramebufferFilter.Linear);

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                _window.SwapBuffers();
            }
            _window.UnsetCurrent();
        }
    }
}
