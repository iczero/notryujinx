namespace Ryujinx.Graphics.OpenGL
{
    public class PostProcessingEffect
    {
        private readonly string _shader;

        public PostProcessingEffect(string shader)
        {
            _shader = shader;
        }
    }
}