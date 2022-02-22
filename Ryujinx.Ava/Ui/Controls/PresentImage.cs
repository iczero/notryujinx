using OpenTK.Graphics.OpenGL;
using System;
using System.Threading;

namespace Ryujinx.Ava.Ui.Controls;

public class PresentImage : IDisposable
{
    public int Texture { get; set; }

    public int ReadySemaphore { get; set; }
    public int CompletedSemaphore { get; set; }

    public IntPtr WaitFence { get; set; } = IntPtr.Zero;

    public PresentImage(int texture, int readySemaphore, int completedSemaphore)
    {
        Texture = texture;
        ReadySemaphore = readySemaphore;
        CompletedSemaphore = completedSemaphore;
    }

    public void Dispose()
    {
        Dispose(false);
    }

    public void Dispose(bool disposeSemaphores = false)
    {
        GL.DeleteTexture(Texture);

        if (disposeSemaphores && ReadySemaphore != 0)
        {
            GL.Ext.DeleteSemaphore(ReadySemaphore);
            GL.Ext.DeleteSemaphore(CompletedSemaphore);
        }
    }
}