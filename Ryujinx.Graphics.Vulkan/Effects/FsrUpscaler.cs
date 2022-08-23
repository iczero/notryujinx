using Ryujinx.Common;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using Silk.NET.Vulkan;
using System;
using System.IO;

namespace Ryujinx.Graphics.Vulkan.Effects
{
    internal partial class FsrUpscaler : IScaler
    {
        private readonly VulkanRenderer _renderer;
        private TextureView _outputTexture;
        private PipelineHelperShader _scalingPipeline;
        private PipelineHelperShader _sharpeningPipeline;
        private ISampler _samplerLinear;
        private ShaderCollection _scalingProgram;
        private ShaderCollection _sharpeningProgram;
        private float _scale = 1;
        private Device _device;
        private CommandBufferScoped _currentCommandBuffer;

        public float Scale
        {
            get => _scale; set
            {
                _scale = MathF.Max(0.01f, value);
            }
        }

        public IPostProcessingEffect Effect { get; set; }

        public FsrUpscaler(VulkanRenderer renderer, Device device, IPostProcessingEffect filter)
        {
            _device = device;
            _renderer = renderer;
            Initialize();
            Effect = filter;
        }

        public void Dispose()
        {
            _scalingPipeline?.Dispose();
            _scalingProgram?.Dispose();
            _sharpeningPipeline?.Dispose();
            _sharpeningProgram?.Dispose();
            _samplerLinear?.Dispose();
            _outputTexture?.Dispose();
        }

        public void Initialize()
        {
            _scalingPipeline = new PipelineHelperShader(_renderer, _device);
            _sharpeningPipeline = new PipelineHelperShader(_renderer, _device);

            _scalingPipeline?.Initialize();
            _sharpeningPipeline?.Initialize();

            var computeBindings = new ShaderBindings(
                new[] { 2, 3 },
                Array.Empty<int>(),
                new[] { 1 },
                new[] { 0 }
            );

            _samplerLinear = _renderer.CreateSampler(GAL.SamplerCreateInfo.Create(MinFilter.Linear, MagFilter.Linear));

            _scalingProgram = _renderer.CreateProgramWithMinimalLayout(new[]{
                new ShaderSource(this.ScalingShader, computeBindings, ShaderStage.Compute, TargetLanguage.Spirv)
            });

            _sharpeningProgram = _renderer.CreateProgramWithMinimalLayout(new[]{
                new ShaderSource(this.SharpeningShader, computeBindings, ShaderStage.Compute, TargetLanguage.Spirv)
            });
        }

        public TextureView Run(TextureView view, CommandBufferScoped cbs)
        {
            _currentCommandBuffer = cbs;
            var input = view;

            if (Effect != null)
            {
                input = Effect.Run(input, cbs);
            }

            var upscaledWidth = (int)(input.Width * Scale);
            var upscaledHeight = (int)(input.Height * Scale);

            if (_outputTexture == null || _outputTexture.Info.Width != upscaledWidth || _outputTexture.Info.Height != upscaledHeight)
            {
                var originalInfo = input.Info;
                var info = new TextureCreateInfo(upscaledWidth,
                    upscaledHeight,
                    originalInfo.Depth,
                    originalInfo.Levels,
                    originalInfo.Samples,
                    originalInfo.BlockWidth,
                    originalInfo.BlockHeight,
                    originalInfo.BytesPerPixel,
                    originalInfo.Format,
                    originalInfo.DepthStencilMode,
                    originalInfo.Target,
                    originalInfo.SwizzleR,
                    originalInfo.SwizzleG,
                    originalInfo.SwizzleB,
                    originalInfo.SwizzleA);
                _outputTexture?.Dispose();
                _outputTexture = _renderer.CreateTexture(info, input.ScaleFactor) as TextureView;
            }

            Span<GAL.Viewport> viewports = stackalloc GAL.Viewport[1];
            Span<Rectangle<int>> scissors = stackalloc Rectangle<int>[1];

            viewports[0] = new GAL.Viewport(
                new Rectangle<float>(0, 0, input.Width, input.Height),
                ViewportSwizzle.PositiveX,
                ViewportSwizzle.PositiveY,
                ViewportSwizzle.PositiveZ,
                ViewportSwizzle.PositiveW,
                0f,
                1f);

            scissors[0] = new Rectangle<int>(0, 0, input.Width, input.Height);

            _scalingPipeline.SetCommandBuffer(cbs);
            _scalingPipeline.SetProgram(_scalingProgram);
            _scalingPipeline.SetTextureAndSampler(ShaderStage.Compute, 1, input, _samplerLinear);

            var inputResolutionBuffer = new ReadOnlySpan<float>(new float[] { input.Width, input.Height });
            int rangeSize = inputResolutionBuffer.Length * sizeof(float);
            var bufferHandle = _renderer.BufferManager.CreateWithHandle(_renderer, rangeSize, false);
            _renderer.BufferManager.SetData(bufferHandle, 0, inputResolutionBuffer);

            var outputResolutionBuffer = new ReadOnlySpan<float>(new float[] { _outputTexture.Width, _outputTexture.Height });
            var outputBufferHandle = _renderer.BufferManager.CreateWithHandle(_renderer, rangeSize, false);
            _renderer.BufferManager.SetData(outputBufferHandle, 0, outputResolutionBuffer);

            int threadGroupWorkRegionDim = 16;
            int dispatchX = (upscaledWidth + (threadGroupWorkRegionDim - 1)) / threadGroupWorkRegionDim;
            int dispatchY = (upscaledHeight + (threadGroupWorkRegionDim - 1)) / threadGroupWorkRegionDim;

            Span<BufferRange> bufferRanges = stackalloc BufferRange[1];
            bufferRanges[0] = new BufferRange(bufferHandle, 0, rangeSize);
            _scalingPipeline.SetUniformBuffers(2, bufferRanges);
            bufferRanges[0] = new BufferRange(outputBufferHandle, 0, rangeSize);
            _scalingPipeline.SetUniformBuffers(3, bufferRanges);
            _scalingPipeline.SetScissors(scissors);
            _scalingPipeline.SetViewports(viewports, false);
            _scalingPipeline.SetImage(0, _outputTexture, GAL.Format.R8G8B8A8Unorm);
            _scalingPipeline.DispatchCompute(dispatchX, dispatchY, 1);
            _scalingPipeline.ComputeBarrier();

            viewports[0] = new GAL.Viewport(
                new Rectangle<float>(0, 0, _outputTexture.Width, _outputTexture.Height),
                ViewportSwizzle.PositiveX,
                ViewportSwizzle.PositiveY,
                ViewportSwizzle.PositiveZ,
                ViewportSwizzle.PositiveW,
                0f,
                1f);

            scissors[0] = new Rectangle<int>(0, 0, _outputTexture.Width, _outputTexture.Height);

            // Sharpening pass
            _sharpeningPipeline.SetCommandBuffer(cbs);
            _sharpeningPipeline.SetProgram(_sharpeningProgram);
            _sharpeningPipeline.SetTextureAndSampler(ShaderStage.Compute, 1, _outputTexture, _samplerLinear);
            _sharpeningPipeline.SetUniformBuffers(2, bufferRanges);
            _sharpeningPipeline.SetScissors(scissors);
            _sharpeningPipeline.SetViewports(viewports, false);
            _sharpeningPipeline.SetImage(0, _outputTexture, GAL.Format.R8G8B8A8Unorm);
            _sharpeningPipeline.DispatchCompute(dispatchX, dispatchY, 1);
            _sharpeningPipeline.ComputeBarrier();

            _renderer.BufferManager.Delete(bufferHandle);
            _renderer.BufferManager.Delete(outputBufferHandle);

            return _outputTexture;
        }
    }
}