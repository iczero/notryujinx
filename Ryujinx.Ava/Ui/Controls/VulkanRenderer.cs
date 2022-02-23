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

        private ConcurrentQueue<PresentSubmission> _presentationQueue = new ConcurrentQueue<PresentSubmission>();
        private Dictionary<int, PresentImage> _images = new Dictionary<int, PresentImage>();

        public Action WaitAction { get; set; }
        
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();

        private ManualResetEventSlim _resetEvent = new ManualResetEventSlim(false); 

        protected unsafe override void OnRender(GlInterface gl, int fb)
        {
            if (_presentationQueue.IsEmpty)
            {
                return;
            }

            if (_presentationQueue.TryDequeue(out var submission))
            {
                submission.Present(this.Framebuffer, fb);

                if(_images.TryGetValue(submission.Index, out var image))
                {
                    image.WaitFence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None);
                }
            }

            _resetEvent.Set();
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
                image.Value?.Dispose(true);
            }
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

        public void AddImage(int texture, int index, int readySemaphore, int completeSemaphore)
        {
            if (_images.TryGetValue(index, out var oldImage))
            {
                oldImage.Dispose(false);
                readySemaphore = readySemaphore == 0 ? oldImage.ReadySemaphore : readySemaphore;
                completeSemaphore = readySemaphore == 0 ? oldImage.CompletedSemaphore : completeSemaphore;
            }

            var image = new PresentImage(texture, readySemaphore, completeSemaphore);
            if (!_images.TryAdd(index, image))
            {
                _images[index] = image;
            }
        }

        internal override bool Present(int index)
        {
            MakeCurrent();

            bool presented = false;

            if (_images.TryGetValue(index, out var image))
            {
                if (image.WaitFence != IntPtr.Zero)
                {
                    GL.ClientWaitSync(image.WaitFence, ClientWaitSyncFlags.None, long.MaxValue);
                    GL.DeleteSync(image.WaitFence);
                    image.WaitFence = IntPtr.Zero;

                    presented = true;
                }

                if (presented || _presentationQueue.IsEmpty)
                {
                    GL.Ext.WaitSemaphore(image.CompletedSemaphore, 0, null, 1, new[] { image.Texture },
                        new[] { PresentSubmission.Layout });

                    _presentationQueue.Enqueue(new PresentSubmission(image.Texture, image.ReadySemaphore,
                        new Vector2((float)RenderSize.Width, (float)RenderSize.Height), index));

                    QueueRender();

                    presented = true;
                }
            }

            MakeCurrent(null);

            if(_presentationQueue.Count > 1)
            {
                _resetEvent.Reset();
                _resetEvent.Wait();
            }

            return presented;
        }
    }
}
