using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Ryujinx.Ava.Ui.Vulkan
{
    internal class DefaultVulkanDeviceInitialization : IVulkanDeviceInitialization
    {
        /// <inheritdoc/>
        public unsafe Device CreateDevice(Vk api, VulkanInstance instance, VulkanPhysicalDevice physicalDevice, VulkanOptions options)
        {
            uint queueCount = options.MaxQueueCount == 0 ? physicalDevice.QueueCount : Math.Min(options.MaxQueueCount, physicalDevice.QueueCount);

            var queuePriorities = stackalloc float[(int)queueCount];

            var supportedFeatures = api.GetPhysicalDeviceFeature(physicalDevice.InternalHandle);

            for (var i = 0; i < queueCount; i++)
                queuePriorities[i] = 1f;

            var features = new PhysicalDeviceFeatures()
            {
                DepthBiasClamp = supportedFeatures.DepthBiasClamp,
                DepthClamp = supportedFeatures.DepthClamp,
                DualSrcBlend = supportedFeatures.DualSrcBlend,
                FragmentStoresAndAtomics = supportedFeatures.FragmentStoresAndAtomics,
                GeometryShader = supportedFeatures.GeometryShader,
                ImageCubeArray = supportedFeatures.ImageCubeArray,
                IndependentBlend = supportedFeatures.IndependentBlend,
                LogicOp = supportedFeatures.LogicOp,
                MultiViewport = supportedFeatures.MultiViewport,
                PipelineStatisticsQuery = supportedFeatures.PipelineStatisticsQuery,
                SamplerAnisotropy = supportedFeatures.SamplerAnisotropy,
                ShaderClipDistance = supportedFeatures.ShaderClipDistance,
                ShaderFloat64 = supportedFeatures.ShaderFloat64,
                ShaderImageGatherExtended = supportedFeatures.ShaderImageGatherExtended,
                // ShaderStorageImageReadWithoutFormat = true,
                // ShaderStorageImageWriteWithoutFormat = true,
                TessellationShader = supportedFeatures.TessellationShader,
                VertexPipelineStoresAndAtomics = supportedFeatures.VertexPipelineStoresAndAtomics
            };

            var supportedExtensions = physicalDevice.GetSupportedExtensions();

            void* pExtendedFeatures = null;

            PhysicalDeviceTransformFeedbackFeaturesEXT featuresTransformFeedback;

            if (supportedExtensions.Contains("VK_EXT_transform_feedback"))
            {
                featuresTransformFeedback = new PhysicalDeviceTransformFeedbackFeaturesEXT()
                {
                    SType = StructureType.PhysicalDeviceTransformFeedbackFeaturesExt,
                    PNext = pExtendedFeatures,
                    TransformFeedback = true
                };

                pExtendedFeatures = &featuresTransformFeedback;
            }

            PhysicalDeviceRobustness2FeaturesEXT featuresRobustness2;

            if (supportedExtensions.Contains("VK_EXT_robustness2"))
            {
                featuresRobustness2 = new PhysicalDeviceRobustness2FeaturesEXT()
                {
                    SType = StructureType.PhysicalDeviceRobustness2FeaturesExt,
                    PNext = pExtendedFeatures,
                    NullDescriptor = true
                };

                pExtendedFeatures = &featuresRobustness2;
            }

            var featuresExtendedDynamicState = new PhysicalDeviceExtendedDynamicStateFeaturesEXT()
            {
                SType = StructureType.PhysicalDeviceExtendedDynamicStateFeaturesExt,
                PNext = pExtendedFeatures,
                ExtendedDynamicState = supportedExtensions.Contains(ExtExtendedDynamicState.ExtensionName)
            };

            pExtendedFeatures = &featuresExtendedDynamicState;

            var featuresVk11 = new PhysicalDeviceVulkan11Features()
            {
                SType = StructureType.PhysicalDeviceVulkan11Features,
                PNext = pExtendedFeatures,
                ShaderDrawParameters = true
            };

            pExtendedFeatures = &featuresVk11;

            var featuresVk12 = new PhysicalDeviceVulkan12Features()
            {
                SType = StructureType.PhysicalDeviceVulkan12Features,
                PNext = pExtendedFeatures,
                DrawIndirectCount = supportedExtensions.Contains(KhrDrawIndirectCount.ExtensionName)
            };

            pExtendedFeatures = &featuresVk12;

            PhysicalDeviceIndexTypeUint8FeaturesEXT featuresIndexU8;

            if (supportedExtensions.Contains("VK_EXT_index_type_uint8"))
            {
                featuresIndexU8 = new PhysicalDeviceIndexTypeUint8FeaturesEXT()
                {
                    SType = StructureType.PhysicalDeviceIndexTypeUint8FeaturesExt,
                    PNext = pExtendedFeatures,
                    IndexTypeUint8 = true
                };

                pExtendedFeatures = &featuresIndexU8;
            }

            PhysicalDeviceFragmentShaderInterlockFeaturesEXT featuresFragmentShaderInterlock;

            if (supportedExtensions.Contains("VK_EXT_fragment_shader_interlock"))
            {
                featuresFragmentShaderInterlock = new PhysicalDeviceFragmentShaderInterlockFeaturesEXT()
                {
                    SType = StructureType.PhysicalDeviceFragmentShaderInterlockFeaturesExt,
                    PNext = pExtendedFeatures,
                    FragmentShaderPixelInterlock = true
                };

                pExtendedFeatures = &featuresFragmentShaderInterlock;
            }

            PhysicalDeviceSubgroupSizeControlFeaturesEXT featuresSubgroupSizeControl;

            if (supportedExtensions.Contains("VK_EXT_subgroup_size_control"))
            {
                featuresSubgroupSizeControl = new PhysicalDeviceSubgroupSizeControlFeaturesEXT()
                {
                    SType = StructureType.PhysicalDeviceSubgroupSizeControlFeaturesExt,
                    PNext = pExtendedFeatures,
                    SubgroupSizeControl = true
                };

                pExtendedFeatures = &featuresSubgroupSizeControl;
            }

            var queueCreateInfo = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = physicalDevice.QueueFamilyIndex,
                QueueCount = queueCount,
                PQueuePriorities = queuePriorities
            };

            var enabledExtensions = VulkanDevice.RequiredDeviceExtensions.Union(
                options.DeviceExtensions.Intersect(supportedExtensions)).ToArray();

            var ppEnabledExtensions = stackalloc IntPtr[enabledExtensions.Length];

            for (var i = 0; i < enabledExtensions.Length; i++)
                ppEnabledExtensions[i] = Marshal.StringToHGlobalAnsi(enabledExtensions[i]);

            var deviceCreateInfo = new DeviceCreateInfo
            {
                PNext = pExtendedFeatures,
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queueCreateInfo,
                PpEnabledExtensionNames = (byte**)ppEnabledExtensions,
                EnabledExtensionCount = (uint)enabledExtensions.Length,
                PEnabledFeatures = &features
            };

            api.CreateDevice(physicalDevice.InternalHandle, in deviceCreateInfo, null, out var device)
                .ThrowOnError();

            for (var i = 0; i < enabledExtensions.Length; i++)
                Marshal.FreeHGlobal(ppEnabledExtensions[i]);

            return device;
        }
    }
}
