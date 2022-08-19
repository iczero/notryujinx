using Ryujinx.Graphics.Vulkan;
using System;

namespace Ryujinx.Graphics.Vulkan.Effects
{
    internal interface IPostProcessingEffect :  IDisposable
    {
        protected void Initialize();
        internal TextureView Run(TextureView view, CommandBufferScoped cbs);
    }
}