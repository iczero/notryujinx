namespace Ryujinx.Graphics.Vulkan.Effects
{
    internal interface IScaler : IPostProcessingEffect
    {
        IPostProcessingEffect Effect { get; set; }
        float Scale { get; set; }
    }
}