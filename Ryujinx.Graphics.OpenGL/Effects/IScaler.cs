namespace Ryujinx.Graphics.OpenGL.Effects
{
    internal interface IScaler : IPostProcessingEffect
    {
        IPostProcessingEffect Effect { get; set; }
        float Scale { get; set; }
    }
}