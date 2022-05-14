using Android.App;
using Android.Content.PM;
using Avalonia.Android;
using Avalonia;
using Ryujinx.Common.Configuration;
using Ryujinx.Rsc.Backend;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using System.Collections.Generic;
using System;
using Ryujinx.Rsc.Common.Configuration;

namespace Ryujinx.Rsc.Android
{
    [Activity(Label = "Ryujinx.Rsc.Android", Theme = "@style/MyTheme.NoActionBar", Icon = "@drawable/ryujinx", LaunchMode = LaunchMode.SingleInstance, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
    public class MainActivity : AvaloniaActivity<App>
    {
        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            builder.UseSkia()
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
                });
            return base.CustomizeAppBuilder(builder);
        }
    }
}
