using OpenTK.Graphics.OpenGL;
using System;
using System.Threading;

namespace Ryujinx.Ava.Ui.Controls;

public class PresentImage : IDisposable
{
    public int Texture { get; set; }
    public bool Presented { get; set; }
    public IntPtr Fence { get; set; } = IntPtr.Zero;
    
    private ManualResetEventSlim _resetEvent;
    private CancellationTokenSource _cancellationTokenSource;
    
    public PresentImage(int texture)
    {
        Texture = texture;
        _resetEvent = new ManualResetEventSlim(false);
        _cancellationTokenSource = new CancellationTokenSource();
    }
    
    public void Dispose()
    {
        GL.DeleteTexture(Texture);

        if (Fence != IntPtr.Zero)
        {
            GL.DeleteSync(Fence);
        }
        
        _resetEvent.Set();
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _resetEvent.Dispose();
    }

    public void WaitTillReady()
    {
        _resetEvent.Wait(_cancellationTokenSource.Token);
    }

    public void SetReady()
    {
        _resetEvent.Set();
    }

    public void Reset()
    {
        _resetEvent.Reset();
    }
}