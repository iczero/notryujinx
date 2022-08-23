namespace Ryujinx.Graphics.Vulkan.Effects
{
    internal interface IScaler : IPostProcessingEffect
    {
        float Level { get; set; }
    }
}