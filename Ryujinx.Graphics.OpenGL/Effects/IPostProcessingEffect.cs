using Ryujinx.Graphics.OpenGL.Image;
using System;

namespace Ryujinx.Graphics.OpenGL.Effects
{
    internal interface IPostProcessingEffect :  IDisposable
    {
        protected void Initialize();
        internal TextureView Run(TextureView view);
    }
}