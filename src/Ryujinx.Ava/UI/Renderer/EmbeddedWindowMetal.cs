using SharpMetal.QuartzCore;
using System.Runtime.Versioning;

namespace Ryujinx.Ava.UI.Renderer
{
    [SupportedOSPlatform("macos")]
    public class EmbeddedWindowMetal : EmbeddedWindow
    {
        public CAMetalLayer GetMetalLayer()
        {
            return new(MetalLayer);
        }
    }
}
