using OpenTK.Graphics.OpenGL;
using System;

namespace Ryujinx.Ava.Ui.Controls;

public class PresentImage : IDisposable
{
    public PresentImage(int texture)
    {
        Texture = texture;
    }

    public int Texture { get; set; }
    public bool Presented { get; set; }
    public IntPtr Fence { get; set; } = IntPtr.Zero;
    
    public void Dispose()
    {
        GL.DeleteTexture(Texture);

        if (Fence != IntPtr.Zero)
        {
            GL.DeleteSync(Fence);
        }
    }
}