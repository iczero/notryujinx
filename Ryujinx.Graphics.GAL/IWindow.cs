using System;

namespace Ryujinx.Graphics.GAL
{
    public interface IWindow
    {
        event EventHandler<ExternalMemoryObjectCreatedEvent> ExternalImageCreated;
        event EventHandler<int> ExternalImageDestroyed;

        void Present(ITexture texture, ImageCrop crop, Func<int, bool> swapBuffersCallback);

        void SetSize(int width, int height);
    }
}
