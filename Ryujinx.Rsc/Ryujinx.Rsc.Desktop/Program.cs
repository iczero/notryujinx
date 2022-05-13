using System;
using Avalonia;
using Ryujinx.Common.Configuration;
using Ryujinx.Rsc.Backend;
using Ryujinx.Rsc.Common.Configuration;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using System.Collections.Generic;

namespace Ryujinx.Rsc.Desktop
{
    class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            App.PreviewerDetached = true;
            App.LoadConfiguration();
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .UseSkia()
                .With(new Vulkan.VulkanOptions()
                {
                    ApplicationName = "Ryujinx.Graphics.Vulkan",
                    VulkanVersion = new Version(1, 2),
                    DeviceExtensions = new List<string>
                    {
                        ExtConditionalRendering.ExtensionName,
                        ExtExtendedDynamicState.ExtensionName,
                        KhrDrawIndirectCount.ExtensionName,
                        "VK_EXT_custom_border_color",
                        "VK_EXT_fragment_shader_interlock",
                        "VK_EXT_index_type_uint8",
                        "VK_EXT_robustness2",
                        "VK_EXT_shader_subgroup_ballot",
                        "VK_EXT_subgroup_size_control",
                        "VK_NV_geometry_shader_passthrough"
                    },
                    MaxQueueCount = 2,
                    PreferDiscreteGpu = true,
                    UseDebug = ConfigurationState.Instance.Logger.GraphicsDebugLevel.Value > GraphicsDebugLevel.None,
                })
                .With(new SkiaOptions()
                {
                    CustomGpuFactory = SkiaGpuFactory.CreateVulkanGpu
                })
                .LogToTrace();
    }
}
