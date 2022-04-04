using System;

namespace Ryujinx.Graphics.GAL
{
    public interface IWindow
    {
        void Present(ITexture texture, ImageCrop crop, Func<object, bool> swapBuffersCallback);

        void SetSize(int width, int height);
    }
}
