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
        private float _sharpeningLevel = 1;
        private Device _device;
        private CommandBufferScoped _currentCommandBuffer;

        public float Level
        {
            get => _sharpeningLevel; set
            {
                _sharpeningLevel = MathF.Max(0.01f, value);
            }
        }

        public FsrUpscaler(VulkanRenderer renderer, Device device)
        {
            _device = device;
            _renderer = renderer;
            Initialize();
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

            var sharpeningBindings = new ShaderBindings(
                new[] { 2, 3, 4 },
                Array.Empty<int>(),
                new[] { 1 },
                new[] { 0 }
            );

            _samplerLinear = _renderer.CreateSampler(GAL.SamplerCreateInfo.Create(MinFilter.Linear, MagFilter.Linear));

            _scalingProgram = _renderer.CreateProgramWithMinimalLayout(new[]{
                new ShaderSource(this.ScalingShader, computeBindings, ShaderStage.Compute, TargetLanguage.Spirv)
            });

            _sharpeningProgram = _renderer.CreateProgramWithMinimalLayout(new[]{
                new ShaderSource(this.SharpeningShader, sharpeningBindings, ShaderStage.Compute, TargetLanguage.Spirv)
            });
        }

        public TextureView Run(TextureView view, CommandBufferScoped cbs, int width, int height)
        {
            _currentCommandBuffer = cbs;

            if (_outputTexture == null || _outputTexture.Info.Width != width || _outputTexture.Info.Height != height)
            {
                var originalInfo = view.Info;
                var info = new TextureCreateInfo(width,
                    height,
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
                _outputTexture = _renderer.CreateTexture(info, view.ScaleFactor) as TextureView;
            }

            Span<GAL.Viewport> viewports = stackalloc GAL.Viewport[1];
            Span<Rectangle<int>> scissors = stackalloc Rectangle<int>[1];

            viewports[0] = new GAL.Viewport(
                new Rectangle<float>(0, 0, view.Width, view.Height),
                ViewportSwizzle.PositiveX,
                ViewportSwizzle.PositiveY,
                ViewportSwizzle.PositiveZ,
                ViewportSwizzle.PositiveW,
                0f,
                1f);

            scissors[0] = new Rectangle<int>(0, 0, view.Width, view.Height);

            _scalingPipeline.SetCommandBuffer(cbs);
            _scalingPipeline.SetProgram(_scalingProgram);
            _scalingPipeline.SetTextureAndSampler(ShaderStage.Compute, 1, view, _samplerLinear);

            var inputResolutionBuffer = new ReadOnlySpan<float>(new float[] { view.Width, view.Height });
            int rangeSize = inputResolutionBuffer.Length * sizeof(float);
            var bufferHandle = _renderer.BufferManager.CreateWithHandle(_renderer, rangeSize, false);
            _renderer.BufferManager.SetData(bufferHandle, 0, inputResolutionBuffer);

            var outputResolutionBuffer = new ReadOnlySpan<float>(new float[] { _outputTexture.Width, _outputTexture.Height });
            var outputBufferHandle = _renderer.BufferManager.CreateWithHandle(_renderer, rangeSize, false);
            _renderer.BufferManager.SetData(outputBufferHandle, 0, outputResolutionBuffer);

            var sharpeningBuffer = new ReadOnlySpan<float>(new float[] { Level });
            var sharpeningBufferHandle = _renderer.BufferManager.CreateWithHandle(_renderer, sizeof(float), false);
            _renderer.BufferManager.SetData(sharpeningBufferHandle, 0, sharpeningBuffer);

            int threadGroupWorkRegionDim = 16;
            int dispatchX = (width + (threadGroupWorkRegionDim - 1)) / threadGroupWorkRegionDim;
            int dispatchY = (height + (threadGroupWorkRegionDim - 1)) / threadGroupWorkRegionDim;

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
            bufferRanges[0] = new BufferRange(sharpeningBufferHandle, 0, sizeof(float));
            _sharpeningPipeline.SetUniformBuffers(4, bufferRanges);
            _sharpeningPipeline.SetScissors(scissors);
            _sharpeningPipeline.SetViewports(viewports, false);
            _sharpeningPipeline.SetImage(0, _outputTexture, GAL.Format.R8G8B8A8Unorm);
            _sharpeningPipeline.DispatchCompute(dispatchX, dispatchY, 1);
            _sharpeningPipeline.ComputeBarrier();

            _renderer.BufferManager.Delete(bufferHandle);
            _renderer.BufferManager.Delete(outputBufferHandle);
            _renderer.BufferManager.Delete(sharpeningBufferHandle);

            return _outputTexture;
        }
    }
}