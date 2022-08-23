namespace Ryujinx.Graphics.OpenGL.Effects
{
    internal interface IScaler : IPostProcessingEffect
    {
        float Level { get; set; }
    }
}