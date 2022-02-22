using Avalonia.OpenGL;
using OpenTK.Graphics.OpenGL;
using Silk.NET.Vulkan.Extensions.KHR;
using SPB.Graphics;
using SPB.Graphics.OpenGL;
using SPB.Platform;
using SPB.Windowing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.Ava.Ui.Controls
{
    internal class VulkanRenderer : RendererControl
    {
        private SwappableNativeWindowBase _window;
        
        public int ReadySemaphore { get; set; }
        public int CompleteSemaphore { get; set; }

        private ConcurrentQueue<PresentSubmission> _presentationQueue = new ConcurrentQueue<PresentSubmission>();
        private Dictionary<int, PresentImage> _images = new Dictionary<int, PresentImage>();

        public Action WaitAction { get; set; }
        
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();

        protected unsafe override void OnRender(GlInterface gl, int fb)
        {
            if (_presentationQueue.IsEmpty)
            {
                return;
            }

            if (_presentationQueue.TryDequeue(out var submission))
            {
                submission.Present(this.Framebuffer, fb);
                if (_images.TryGetValue(submission.Index, out var image))
                {
                    image.Fence = submission.Fence;
                    image.Presented = true;
                    image.SetReady();
                }
            }

            if (!_presentationQueue.IsEmpty)
            {
                QueueRender();
            }
        }

        protected override  void CreateGlContext(OpenGLContextBase mainContext)
        {
            _window = PlatformHelper.CreateOpenGLWindow(FramebufferFormat.Default, 0, 0, (int)Bounds.Width, (int)Bounds.Height);
            _window.Hide();
            Context = PlatformHelper.CreateOpenGLContext(FramebufferFormat.Default, 4, 5, OpenGLContextFlags.Default, shareContext: mainContext);
            Context.Initialize(_window);
        }

        public async override Task DestroyBackgroundContext()
        {
            _tokenSource.Cancel();
            _tokenSource.Dispose();
            await Task.Delay(1000);

            foreach (KeyValuePair<int, PresentImage> image in _images)
            {
                image.Value?.Dispose();
            }

            GL.Ext.DeleteSemaphore(CompleteSemaphore);
            GL.Ext.DeleteSemaphore(ReadySemaphore);
            GL.DeleteFramebuffer(Framebuffer);
            // WGL hangs here when disposing context
            //Context?.Dispose();
            _window?.Dispose();
        }

        protected override void OnOpenGlDeinit(GlInterface gl, int fb)
        {
            base.OnOpenGlDeinit(gl, fb);

            if (GuiFence != IntPtr.Zero)
            {
                GL.DeleteSync(GuiFence);
            }
        }

        internal override void MakeCurrent()
        {
            Context.MakeCurrent(_window);
        }

        internal override void MakeCurrent(SwappableNativeWindowBase window)
        {
            Context.MakeCurrent(window);
        }

        public static string[] GetRequiredInstanceExtensions()
        {
            return new string[] { KhrGetPhysicalDeviceProperties2.ExtensionName,
            KhrExternalMemoryCapabilities.ExtensionName,
            KhrExternalSemaphoreCapabilities.ExtensionName,
            KhrSurface.ExtensionName
            };
        }

        public void AddImage(int texture, int index)
        {
            if (_images.TryGetValue(index, out var oldImage))
            {
                oldImage.Dispose();
            }
            var image = new PresentImage(texture);
            if (!_images.TryAdd(index, image))
            {
                _images[index] = image;
            }
        }

        internal override bool Present(int index)
        {
            MakeCurrent();

            if (_images.TryGetValue(index, out var image))
            {
                if (_presentationQueue.FirstOrDefault(x => x.Index == index) != null)
                {
                    image.WaitTillReady();
                }

                if (image.Fence != IntPtr.Zero)
                {
                    GL.ClientWaitSync(image.Fence, ClientWaitSyncFlags.SyncFlushCommandsBit, Int64.MaxValue);
                    GL.DeleteSync(image.Fence);
                    image.Fence = IntPtr.Zero;
                    image.Presented = false;
                }

                _presentationQueue.Enqueue(new PresentSubmission(image.Texture, ReadySemaphore,
                    new Vector2((float)RenderSize.Width, (float)RenderSize.Height), index));
                image.Reset();

                GL.Ext.WaitSemaphore(CompleteSemaphore, 0, null, 1, new[] { image.Texture },
                    new[] { PresentSubmission.Layout });

                QueueRender();

                MakeCurrent(null);
            }

            return true;
        }
    }
}
