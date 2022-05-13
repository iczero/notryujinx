using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Ryujinx.Ava.Vulkan
{
    internal class DefaultVulkanDeviceInitialization : IVulkanDeviceInitialization
    {
        /// <inheritdoc/>
        public unsafe Device CreateDevice(Vk api, VulkanInstance instance, VulkanPhysicalDevice physicalDevice, VulkanOptions options)
        {
            uint queueCount = options.MaxQueueCount == 0 ? physicalDevice.QueueCount : Math.Min(options.MaxQueueCount, physicalDevice.QueueCount);

            var queuePriorities = stackalloc float[(int)queueCount];

            for (var i = 0; i < queueCount; i++)
                queuePriorities[i] = 1f;

            var features = new PhysicalDeviceFeatures()
            {
                DepthBiasClamp = true,
                DepthClamp = true,
                DualSrcBlend = true,
                FragmentStoresAndAtomics = true,
                GeometryShader = true,
                ImageCubeArray = true,
                IndependentBlend = true,
                LogicOp = true,
                MultiViewport = true,
                PipelineStatisticsQuery = true,
                SamplerAnisotropy = true,
                ShaderClipDistance = true,
                ShaderImageGatherExtended = true,
                // ShaderStorageImageReadWithoutFormat = true,
                // ShaderStorageImageWriteWithoutFormat = true,
                TessellationShader = true,
                VertexPipelineStoresAndAtomics = true
            };

            var supportedExtensions = physicalDevice.GetSupportedExtensions();

            var featuresIndexU8 = new PhysicalDeviceIndexTypeUint8FeaturesEXT()
            {
                SType = StructureType.PhysicalDeviceIndexTypeUint8FeaturesExt,
                IndexTypeUint8 = true
            };

            var featuresTransformFeedback = new PhysicalDeviceTransformFeedbackFeaturesEXT()
            {
                SType = StructureType.PhysicalDeviceTransformFeedbackFeaturesExt,
                PNext = supportedExtensions.Contains("VK_EXT_index_type_uint8") ? &featuresIndexU8 : null,
                TransformFeedback = true
            };

            var featuresRobustness2 = new PhysicalDeviceRobustness2FeaturesEXT()
            {
                SType = StructureType.PhysicalDeviceRobustness2FeaturesExt,
                PNext = &featuresTransformFeedback,
                NullDescriptor = true
            };

            var featuresExtendedDynamicState = new PhysicalDeviceExtendedDynamicStateFeaturesEXT()
            {
                SType = StructureType.PhysicalDeviceExtendedDynamicStateFeaturesExt,
                PNext = &featuresRobustness2,
                ExtendedDynamicState = supportedExtensions.Contains(ExtExtendedDynamicState.ExtensionName)
            };

            var featuresVk11 = new PhysicalDeviceVulkan11Features()
            {
                SType = StructureType.PhysicalDeviceVulkan11Features,
                PNext = &featuresExtendedDynamicState,
                ShaderDrawParameters = true
            };

            var featuresVk12 = new PhysicalDeviceVulkan12Features()
            {
                SType = StructureType.PhysicalDeviceVulkan12Features,
                PNext = &featuresVk11,
                DrawIndirectCount = supportedExtensions.Contains(KhrDrawIndirectCount.ExtensionName)
            };

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
                PNext = &featuresVk12,
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
