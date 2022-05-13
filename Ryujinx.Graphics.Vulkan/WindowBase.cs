using Ryujinx.Graphics.GAL;
using System;

namespace Ryujinx.Graphics.Vulkan
{
    internal abstract class WindowBase: IWindow
    {

        internal bool ScreenCaptureRequested { get; set; }

        public abstract void Dispose();
        public abstract void Present(ITexture texture, ImageCrop crop, Action<object> swapBuffersCallback);
        public abstract void SetSize(int width, int height);
    }
}