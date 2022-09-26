using Ryujinx.Common;
using System;

namespace Ryujinx.Graphics.GAL
{
    public interface IWindow
    {
        void Present(ITexture texture, ImageCrop crop, Action swapBuffersCallback);

        Osd Osd { get; }

        void SetSize(int width, int height);

        void ChangeVSyncMode(bool vsyncEnabled);

        void SetAntiAliasing(AntiAliasing antialiasing);

        void SetUpscaler(UpscaleType scalerType);

        void SetUpscalerLevel(float level);
    }
}
