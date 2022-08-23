using System;

namespace Ryujinx.Graphics.GAL
{
    public interface IWindow
    {
        void Present(ITexture texture, ImageCrop crop, Action<object> swapBuffersCallback);

        void SetSize(int width, int height);

        void ChangeVSyncMode(bool vsyncEnabled);

        void ApplyEffect(EffectType effect);
        void ApplyScaler(PostProcessingScalerType scalerType);
        void SetUpscalerScale(float scale);
    }
}
