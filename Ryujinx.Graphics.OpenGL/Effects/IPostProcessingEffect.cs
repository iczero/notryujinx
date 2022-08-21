using Ryujinx.Graphics.OpenGL.Image;
using System;

namespace Ryujinx.Graphics.OpenGL.Effects
{
    internal interface IPostProcessingEffect :  IDisposable
    {
        protected const int LocalGroupSize = 10;
        protected void Initialize();
        internal TextureView Run(TextureView view, int width, int height);
    }
}