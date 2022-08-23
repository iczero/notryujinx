using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Ryujinx.Common;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.OpenGL.Image;
using System;
using System.IO;

namespace Ryujinx.Graphics.OpenGL.Effects
{
    internal class FsrUpscaler : IScaler
    {
        private readonly OpenGLRenderer _renderer;
        private int _inputResolutionUniform;
        private int _outputResolutionUniform;
        private int _inputUniform;
        private int _outputUniform;
        private int _scalingShaderProgram;
        private TextureStorage _textureStorage;
        private int _sharpeningShaderProgram;
        private int _frameCount = 0;
        private float _scale = 1;

        public float Scale
        {
            get => _scale; set
            {
                _scale = MathF.Max(0.01f, value);
            }
        }

        public IPostProcessingEffect Effect { get; set; }

        public FsrUpscaler(OpenGLRenderer renderer, IPostProcessingEffect filter)
        {
            Initialize();
            _renderer = renderer;
            Effect = filter;
        }

        public void Dispose()
        {
            if (_scalingShaderProgram != 0)
            {
                GL.DeleteProgram(_scalingShaderProgram);
                GL.DeleteProgram(_sharpeningShaderProgram);
                _textureStorage?.Dispose();
            }
        }

        public void Initialize()
        {
            var scalingShader = EmbeddedResources.ReadAllText("Ryujinx.Graphics.OpenGL/Shaders/fsr_scaling.glsl");
            var sharpeningShader = EmbeddedResources.ReadAllText("Ryujinx.Graphics.OpenGL/Shaders/fsr_sharpening.glsl");
            var fsrA = EmbeddedResources.ReadAllText("Ryujinx.Graphics.OpenGL/Shaders/ffx_a.h");
            var fsr1 = EmbeddedResources.ReadAllText("Ryujinx.Graphics.OpenGL/Shaders/ffx_fsr1.h");

            scalingShader = scalingShader.Replace("#include \"ffx_a.h\"", fsrA);
            scalingShader = scalingShader.Replace("#include \"ffx_fsr1.h\"", fsr1);
            sharpeningShader = sharpeningShader.Replace("#include \"ffx_a.h\"", fsrA);
            sharpeningShader = sharpeningShader.Replace("#include \"ffx_fsr1.h\"", fsr1);

            var shader = GL.CreateShader(ShaderType.ComputeShader);
            GL.ShaderSource(shader, scalingShader);
            GL.CompileShader(shader);
            GL.GetShader(shader, ShaderParameter.CompileStatus, out var status);
            File.WriteAllText("/home/nhv3/personal/Ryujinx/Ryujinx.Ava/bin/Debug/shader.glsl", scalingShader);
            if (status == 0)
            {
                var log = GL.GetShaderInfoLog(shader);
                return;
            }

            _scalingShaderProgram = GL.CreateProgram();
            GL.AttachShader(_scalingShaderProgram, shader);
            GL.LinkProgram(_scalingShaderProgram);

            GL.GetProgram(_scalingShaderProgram, GetProgramParameterName.LinkStatus, out status);
            if (status == 0)
            {
                var log = GL.GetProgramInfoLog(_scalingShaderProgram);
                return;
            }
            GL.DetachShader(_scalingShaderProgram, shader);
            GL.DeleteShader(shader);

            shader = GL.CreateShader(ShaderType.ComputeShader);
            GL.ShaderSource(shader, sharpeningShader);
            GL.CompileShader(shader);
            GL.GetShader(shader, ShaderParameter.CompileStatus, out status);
            if (status == 0)
            {
                var log = GL.GetShaderInfoLog(shader);
                return;
            }

            _sharpeningShaderProgram = GL.CreateProgram();
            GL.AttachShader(_sharpeningShaderProgram, shader);
            GL.LinkProgram(_sharpeningShaderProgram);

            GL.GetProgram(_sharpeningShaderProgram, GetProgramParameterName.LinkStatus, out status);
            if (status == 0)
            {
                var log = GL.GetProgramInfoLog(_sharpeningShaderProgram);
                return;
            }
            GL.DetachShader(_sharpeningShaderProgram, shader);
            GL.DeleteShader(shader);

            _inputResolutionUniform = GL.GetUniformLocation(_scalingShaderProgram, "invResolution");
            _outputResolutionUniform = GL.GetUniformLocation(_scalingShaderProgram, "outvResolution");
            _inputUniform = GL.GetUniformLocation(_scalingShaderProgram, "Source");
            _outputUniform = GL.GetUniformLocation(_scalingShaderProgram, "imgOutput");
        }

        public TextureView Run(TextureView view, int width, int height)
        {
            _frameCount++;

            var input = view;

            if (Effect != null)
            {
                input = Effect.Run(input, width, height);
            }

            var upscaledWidth = (int)(input.Width * Scale);
            var upscaledHeight = (int)(input.Height * Scale);

            if (_textureStorage == null || _textureStorage.Info.Width != upscaledWidth || _textureStorage.Info.Height != upscaledHeight)
            {
                _textureStorage?.Dispose();
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

                _textureStorage = new TextureStorage(_renderer, info, input.ScaleFactor);
                _textureStorage.CreateDefaultView();
            }
            var textureView = _textureStorage.CreateView(_textureStorage.Info, 0, 0) as TextureView;

            int previousProgram = GL.GetInteger(GetPName.CurrentProgram);
            GL.BindImageTexture(0, textureView.Handle, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba8);

	        int threadGroupWorkRegionDim = 16;
            int dispatchX = (upscaledWidth + (threadGroupWorkRegionDim - 1)) / threadGroupWorkRegionDim;
            int dispatchY = (upscaledHeight + (threadGroupWorkRegionDim - 1)) / threadGroupWorkRegionDim;

            // Scaling pass
            GL.UseProgram(_scalingShaderProgram);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, input.Handle);
            GL.Uniform1(_inputUniform, 0);
            GL.Uniform1(_outputUniform, 0);
            GL.Uniform2(_inputResolutionUniform, (float)input.Width, input.Height);
            GL.Uniform2(_outputResolutionUniform, (float)upscaledWidth, upscaledHeight);
            GL.DispatchCompute(dispatchX, dispatchY, 1);

            GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);

            // Sharpening Pass
            GL.UseProgram(_sharpeningShaderProgram);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, textureView.Handle);
            GL.Uniform1(_inputUniform, 0);
            GL.Uniform1(_outputUniform, 0);
            GL.Uniform2(_inputResolutionUniform, (float)upscaledWidth, upscaledHeight);
            GL.Uniform2(_outputResolutionUniform, (float)upscaledWidth, upscaledHeight);
            GL.DispatchCompute(dispatchX, dispatchY, 1);

            GL.UseProgram(previousProgram);
            GL.BindImageTexture(0, 0, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba8);

            GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);

            return textureView;
        }
    }
}