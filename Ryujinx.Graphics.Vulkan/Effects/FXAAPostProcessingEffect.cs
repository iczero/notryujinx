using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using Silk.NET.Vulkan;
using System;

namespace Ryujinx.Graphics.Vulkan.Effects
{
    internal partial class FXAAPostProcessingEffect : IPostProcessingEffect
    {
        private const int LocalGroupSize = 10;
        private readonly VulkanRenderer _renderer;
        private int _resolutionUniform;
        private int _inputUniform;
        private ISampler _samplerLinear;
        private ShaderCollection _shaderProgram;

        private PipelineHelperShader _pipeline;
        private TextureView _texture;

        public FXAAPostProcessingEffect(VulkanRenderer renderer, Device device)
        {
            _renderer = renderer;
            _pipeline = new PipelineHelperShader(renderer, device);
            Initialize();
        }

        public void Dispose()
        {
            _shaderProgram?.Dispose();
            _pipeline?.Dispose();
            _samplerLinear?.Dispose();
            _texture?.Dispose();
        }

        public void Initialize()
        {
            _pipeline.Initialize();

            var computeBindings = new ShaderBindings(
                new[] { 2 },
                Array.Empty<int>(),
                new[] { 1 },
                new[] { 0 }
            );

            _samplerLinear = _renderer.CreateSampler(GAL.SamplerCreateInfo.Create(MinFilter.Linear, MagFilter.Linear));

            _shaderProgram = _renderer.CreateProgramWithMinimalLayout(new[]{
                new ShaderSource(this.Shader, computeBindings, ShaderStage.Compute, TargetLanguage.Spirv)
            });
        }

        public TextureView Run(TextureView view, CommandBufferScoped cbs)
        {
            if (_texture == null || _texture.Width != view.Width || _texture.Height != view.Height)
            {
                _texture?.Dispose();
                _texture = _renderer.CreateTexture(view.Info, view.ScaleFactor) as TextureView;
            }
            Transition(cbs.CommandBuffer,
                       view.GetImage().GetUnsafe().Value,
                       AccessFlags.AccessNoneKhr,
                       AccessFlags.AccessNoneKhr,
                       ImageLayout.General,
                       ImageLayout.TransferDstOptimal);

            _pipeline.SetCommandBuffer(cbs);
            _pipeline.SetProgram(_shaderProgram);
            _pipeline.SetTextureAndSampler(ShaderStage.Compute, 1, view, _samplerLinear);

            var resolutionBuffer = new ReadOnlySpan<float>(new float[] { view.Width, view.Height });
            int rangeSize = resolutionBuffer.Length * sizeof(float);
            var bufferHandle = _renderer.BufferManager.CreateWithHandle(_renderer, rangeSize, false);

            _renderer.BufferManager.SetData(bufferHandle, 0, resolutionBuffer);

            Span<BufferRange> bufferRanges = stackalloc BufferRange[1];

            bufferRanges[0] = new BufferRange(bufferHandle, 0, rangeSize);
            _pipeline.SetUniformBuffers(2, bufferRanges);

            Span<GAL.Viewport> viewports = stackalloc GAL.Viewport[1];

            viewports[0] = new GAL.Viewport(
                new Rectangle<float>(0, 0, view.Width, view.Height),
                ViewportSwizzle.PositiveX,
                ViewportSwizzle.PositiveY,
                ViewportSwizzle.PositiveZ,
                ViewportSwizzle.PositiveW,
                0f,
                1f);

            Span<Rectangle<int>> scissors = stackalloc Rectangle<int>[1];

            scissors[0] = new Rectangle<int>(0, 0, view.Width, view.Height);
            _pipeline.SetScissors(scissors);
            _pipeline.SetViewports(viewports, false);

            _pipeline.SetImage(0, _texture, GAL.Format.R8G8B8A8Unorm);
            _pipeline.DispatchCompute(view.Width / LocalGroupSize, view.Height / LocalGroupSize, 1);

            _renderer.BufferManager.Delete(bufferHandle);

            var memoryBarrier = new MemoryBarrier()
            {
                SType = StructureType.MemoryBarrier,
                SrcAccessMask = AccessFlags.AccessShaderWriteBit,
                DstAccessMask = AccessFlags.AccessNoneKhr,
            };

            _pipeline.ComputeBarrier();

            Transition(cbs.CommandBuffer,
                       _texture.GetImage().GetUnsafe().Value,
                       AccessFlags.AccessNoneKhr,
                       AccessFlags.AccessNoneKhr,
                       ImageLayout.TransferDstOptimal,
                       ImageLayout.General);

            return _texture;
        }

        private unsafe void Transition(
            CommandBuffer commandBuffer,
            Image image,
            AccessFlags srcAccess,
            AccessFlags dstAccess,
            ImageLayout srcLayout,
            ImageLayout dstLayout)
        {
            var subresourceRange = new ImageSubresourceRange(ImageAspectFlags.ImageAspectColorBit, 0, 1, 0, 1);

            var barrier = new ImageMemoryBarrier()
            {
                SType = StructureType.ImageMemoryBarrier,
                SrcAccessMask = srcAccess,
                DstAccessMask = dstAccess,
                OldLayout = srcLayout,
                NewLayout = dstLayout,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = image,
                SubresourceRange = subresourceRange
            };

            _renderer.Api.CmdPipelineBarrier(
                commandBuffer,
                PipelineStageFlags.PipelineStageTopOfPipeBit,
                PipelineStageFlags.PipelineStageAllCommandsBit,
                0,
                0,
                null,
                0,
                null,
                1,
                barrier);
        }
    }
}