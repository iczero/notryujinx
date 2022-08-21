using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using Silk.NET.Vulkan;
using System;
using Format = Ryujinx.Graphics.GAL.Format;

namespace Ryujinx.Graphics.Vulkan.Effects
{
    internal partial class SmaaPostProcessingEffect : IPostProcessingEffect
    {
        private const int LocalGroupSize = 10;
        private readonly VulkanRenderer _renderer;
        private int _resolutionUniform;
        private int _inputUniform;
        private ISampler _samplerLinear;
        private ShaderCollection _edgeProgram;
        private ShaderCollection _blendProgram;
        private ShaderCollection _neighbourProgram;

        private PipelineHelperShader _edgePipeline;
        private PipelineHelperShader _blendPipeline;
        private PipelineHelperShader _neighbourPipleline;
        private TextureView _outputTexture;
        private TextureView _edgeOutputTexture;
        private TextureView _blendOutputTexture;
        private TextureView _areaTexture;
        private TextureView _searchTexture;
        private Device _device;

        public SmaaPostProcessingEffect(VulkanRenderer renderer, Device device)
        {
            _device = device;
            _renderer = renderer;
            _edgePipeline = new PipelineHelperShader(renderer, device);
            _blendPipeline = new PipelineHelperShader(renderer, device);
            _neighbourPipleline = new PipelineHelperShader(renderer, device);
            Initialize();
        }

        public int Quality { get; internal set; }

        public void Dispose()
        {
            _edgeProgram?.Dispose();
            _blendProgram?.Dispose();
            _neighbourProgram?.Dispose();
            _edgePipeline?.Dispose();
            _blendPipeline?.Dispose();
            _neighbourPipleline?.Dispose();
            _samplerLinear?.Dispose();
            _outputTexture?.Dispose();
            _edgeOutputTexture?.Dispose();
            _blendOutputTexture?.Dispose();
            _areaTexture?.Dispose();
            _searchTexture?.Dispose();
        }

        public void Initialize()
        {
            _edgePipeline.Initialize();
            _blendPipeline.Initialize();
            _neighbourPipleline.Initialize();

            var edgeBindings = new ShaderBindings(
                new[] { 2 },
                Array.Empty<int>(),
                new[] { 1 },
                new[] { 0 }
            );

            var blendBindings = new ShaderBindings(
                new[] { 2 },
                Array.Empty<int>(),
                new[] { 1, 3 , 4 },
                new[] { 0 }
            );

            var neighbourBindings = new ShaderBindings(
                new[] { 2 },
                Array.Empty<int>(),
                new[] { 1, 3 },
                new[] { 0 }
            );

            _samplerLinear = _renderer.CreateSampler(GAL.SamplerCreateInfo.Create(MinFilter.Linear, MagFilter.Linear));

            _edgeProgram = _renderer.CreateProgramWithMinimalLayout(new[]{
                new ShaderSource(this.SmaaEdgeShader, edgeBindings, ShaderStage.Compute, TargetLanguage.Spirv)
            });

            _blendProgram = _renderer.CreateProgramWithMinimalLayout(new[]{
                new ShaderSource(this.SmaaBlendShader, blendBindings, ShaderStage.Compute, TargetLanguage.Spirv)
            });

            _neighbourProgram = _renderer.CreateProgramWithMinimalLayout(new[]{
                new ShaderSource(this.SmaaNeighbourShader, neighbourBindings, ShaderStage.Compute, TargetLanguage.Spirv)
            });

            var areaInfo = new TextureCreateInfo(AreaWidth,
                AreaHeight,
                1,
                1,
                1,
                1,
                1,
                1,
                Format.R8G8Unorm,
                DepthStencilMode.Depth,
                Target.Texture2D,
                SwizzleComponent.Red,
                SwizzleComponent.Green,
                SwizzleComponent.Blue,
                SwizzleComponent.Alpha);

            var searchInfo = new TextureCreateInfo(SearchWidth,
                SearchHeight,
                1,
                1,
                1,
                1,
                1,
                1,
                Format.R8Unorm,
                DepthStencilMode.Depth,
                Target.Texture2D,
                SwizzleComponent.Red,
                SwizzleComponent.Green,
                SwizzleComponent.Blue,
                SwizzleComponent.Alpha);

            _areaTexture = _renderer.CreateTexture(areaInfo, 1) as TextureView;
            _searchTexture = _renderer.CreateTexture(searchInfo, 1) as TextureView;

            _areaTexture.SetData(AreaTexture);
            _searchTexture.SetData(SearchTexBytes);
        }

        public TextureView Run(TextureView view, CommandBufferScoped cbs)
        {
            if (_outputTexture == null || _outputTexture.Info.Width != view.Width || _outputTexture.Info.Height != view.Height)
            {
                _outputTexture?.Dispose();
                _edgeOutputTexture?.Dispose();
                _blendOutputTexture?.Dispose();
                _outputTexture = _renderer.CreateTexture(view.Info, view.ScaleFactor) as TextureView;
                _edgeOutputTexture = _renderer.CreateTexture(view.Info, view.ScaleFactor) as TextureView;
                _blendOutputTexture = _renderer.CreateTexture(view.Info, view.ScaleFactor) as TextureView;
            }

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

            _renderer.HelperShader.Clear(_renderer,
                _edgeOutputTexture.GetImageView(),
                new float[] { 0, 0, 0, 1 },
                (uint)(ColorComponentFlags.ColorComponentRBit | ColorComponentFlags.ColorComponentGBit | ColorComponentFlags.ColorComponentBBit | ColorComponentFlags.ColorComponentABit),
                view.Width,
                view.Height,
                _edgeOutputTexture.VkFormat,
                scissors[0]
                );

            _renderer.HelperShader.Clear(_renderer,
                _blendOutputTexture.GetImageView(),
                new float[] { 0, 0, 0, 1 },
                (uint)(ColorComponentFlags.ColorComponentRBit | ColorComponentFlags.ColorComponentGBit | ColorComponentFlags.ColorComponentBBit | ColorComponentFlags.ColorComponentABit),
                view.Width,
                view.Height,
                _blendOutputTexture.VkFormat,
                scissors[0]
                );

            _edgePipeline.SetCommandBuffer(cbs);
            _edgePipeline.SetProgram(_edgeProgram);
            _edgePipeline.SetTextureAndSampler(ShaderStage.Compute, 1, view, _samplerLinear);

            var resolutionBuffer = new ReadOnlySpan<float>(new float[] { view.Width, view.Height });
            int rangeSize = resolutionBuffer.Length * sizeof(float);
            var bufferHandle = _renderer.BufferManager.CreateWithHandle(_renderer, rangeSize, false);

            _renderer.BufferManager.SetData(bufferHandle, 0, resolutionBuffer);

            Span<BufferRange> bufferRanges = stackalloc BufferRange[1];

            bufferRanges[0] = new BufferRange(bufferHandle, 0, rangeSize);
            _edgePipeline.SetUniformBuffers(2, bufferRanges);

            _edgePipeline.SetScissors(scissors);
            _edgePipeline.SetViewports(viewports, false);

            _edgePipeline.SetImage(0, _edgeOutputTexture, GAL.Format.R8G8B8A8Unorm);
            _edgePipeline.DispatchCompute(view.Width / LocalGroupSize, view.Height / LocalGroupSize, 1);


            var memoryBarrier = new MemoryBarrier()
            {
                SType = StructureType.MemoryBarrier,
                SrcAccessMask = AccessFlags.AccessShaderWriteBit,
                DstAccessMask = AccessFlags.AccessNoneKhr,
            };

            _edgePipeline.ComputeBarrier();

            Transition(cbs.CommandBuffer,
                       _outputTexture.GetImage().GetUnsafe().Value,
                       AccessFlags.AccessNoneKhr,
                       AccessFlags.AccessNoneKhr,
                       ImageLayout.TransferDstOptimal,
                       ImageLayout.General);

            _renderer.BufferManager.Delete(bufferHandle);

            return _outputTexture;
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