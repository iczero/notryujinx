using OpenTK.Graphics.OpenGL;
using System;
using System.Numerics;

namespace Ryujinx.Ava.Ui.Controls;

public class PresentSubmission
{
    public int Texture { get; set; }
    public IntPtr Fence { get; private set; }
    public int Semaphore { get; set; }
    public Vector2 Size { get; private set; }
    public int Index { get; }

    public static TextureLayout Layout = TextureLayout.LayoutColorAttachmentExt;

    public PresentSubmission(int texture, int semaphore, Vector2 size, int index)
    {
        Semaphore = semaphore;
        Size = size;
        Index = index;
        Texture = texture;

        Fence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None);
    }

    public void Present(int readFb, int drawFb)
    {
        GL.ClientWaitSync(Fence, ClientWaitSyncFlags.None, ulong.MaxValue);
        GL.DeleteSync(Fence);

        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, readFb);
        GL.FramebufferTexture2D(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, Texture, 0);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, drawFb);
        GL.BlitFramebuffer(0,
            0,
            (int)Size.X,
            (int)Size.Y,
            0,
            0,
            (int)Size.X,
            (int)Size.Y,
            ClearBufferMask.ColorBufferBit,
            BlitFramebufferFilter.Linear);

        GL.FramebufferTexture2D(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, 0, 0);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, drawFb);

        GL.Ext.SignalSemaphore(Semaphore, 0, null, 1, new[] { Texture }, new []{Layout});
    }
}