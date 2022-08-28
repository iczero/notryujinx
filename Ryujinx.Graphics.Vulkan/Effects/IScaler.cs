using System;

namespace Ryujinx.Graphics.Vulkan.Effects
{
    internal interface IScaler : IDisposable
    {
        float Level { get; set; }
        protected void Initialize();
        internal void Run(TextureView view, CommandBufferScoped cbs, Auto<DisposableImageView> destinationTexture, int width, int height, int srcX0, int srcX1, int srcY0, int srcY1, int dstX0, int dstX1, int dstY0, int dstY1);
    }
}