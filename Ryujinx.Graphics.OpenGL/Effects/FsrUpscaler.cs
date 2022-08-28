using OpenTK.Graphics.OpenGL;
using Ryujinx.Common;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.OpenGL.Image;
using System;

namespace Ryujinx.Graphics.OpenGL.Effects
{
    internal class FsrUpscaler : IScaler
    {
        private readonly OpenGLRenderer _renderer;
        private int _inputUniform;
        private int _outputUniform;
        private int _sharpeningUniform;
        private int _srcX0Uniform;
        private int _srcX1Uniform;
        private int _srcY0Uniform;
        private int _scalingShaderProgram;
        private int _sharpeningShaderProgram;
        private int _frameCount = 0;
        private float _scale = 1;
        private int _srcY1Uniform;
        private int _dstX0Uniform;
        private int _dstX1Uniform;
        private int _dstY0Uniform;
        private int _dstY1Uniform;

        public float Level
        {
            get => _scale; set
            {
                _scale = MathF.Max(0.01f, value);
            }
        }

        public FsrUpscaler(OpenGLRenderer renderer, IPostProcessingEffect filter)
        {
            Initialize();
            _renderer = renderer;
        }

        public void Dispose()
        {
            if (_scalingShaderProgram != 0)
            {
                GL.DeleteProgram(_scalingShaderProgram);
                GL.DeleteProgram(_sharpeningShaderProgram);
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
            _inputUniform = GL.GetUniformLocation(_scalingShaderProgram, "Source");
            _outputUniform = GL.GetUniformLocation(_scalingShaderProgram, "imgOutput");
            _sharpeningUniform = GL.GetUniformLocation(_sharpeningShaderProgram, "sharpening");

            _srcX0Uniform = GL.GetUniformLocation(_scalingShaderProgram, "srcX0");
            _srcX1Uniform = GL.GetUniformLocation(_scalingShaderProgram, "srcX1");
            _srcY0Uniform = GL.GetUniformLocation(_scalingShaderProgram, "srcY0");
            _srcY1Uniform = GL.GetUniformLocation(_scalingShaderProgram, "srcY1");
            _dstX0Uniform = GL.GetUniformLocation(_scalingShaderProgram, "dstX0");
            _dstX1Uniform = GL.GetUniformLocation(_scalingShaderProgram, "dstX1");
            _dstY0Uniform = GL.GetUniformLocation(_scalingShaderProgram, "dstY0");
            _dstY1Uniform = GL.GetUniformLocation(_scalingShaderProgram, "dstY1");
        }

        public void Run(TextureView view, TextureView destinationTexture, int width, int height, int srcX0, int srcX1, int srcY0, int srcY1, int dstX0, int dstX1, int dstY0, int dstY1)
        {
            _frameCount++;

            int previousProgram = GL.GetInteger(GetPName.CurrentProgram);
            GL.BindImageTexture(0, destinationTexture.Handle, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba8);

	        int threadGroupWorkRegionDim = 16;
            int dispatchX = (width + (threadGroupWorkRegionDim - 1)) / threadGroupWorkRegionDim;
            int dispatchY = (height + (threadGroupWorkRegionDim - 1)) / threadGroupWorkRegionDim;

            // Scaling pass
            GL.UseProgram(_scalingShaderProgram);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, view.Handle);
            GL.Uniform1(_inputUniform, 0);
            GL.Uniform1(_outputUniform, 0);
            GL.Uniform1(_srcX0Uniform, (float)srcX0);
            GL.Uniform1(_srcX1Uniform, (float)srcX1);
            GL.Uniform1(_srcY0Uniform, (float)srcY0);
            GL.Uniform1(_srcY1Uniform, (float)srcY1);
            GL.Uniform1(_dstX0Uniform, (float)dstX0);
            GL.Uniform1(_dstX1Uniform, (float)dstX1);
            GL.Uniform1(_dstY0Uniform, (float)dstY0);
            GL.Uniform1(_dstY1Uniform, (float)dstY1);
            GL.DispatchCompute(dispatchX, dispatchY, 1);

            GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);

            // Sharpening Pass
            GL.UseProgram(_sharpeningShaderProgram);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, destinationTexture.Handle);
            GL.Uniform1(_inputUniform, 0);
            GL.Uniform1(_outputUniform, 0);
            GL.Uniform1(_sharpeningUniform, Level);
            GL.DispatchCompute(dispatchX, dispatchY, 1);

            GL.UseProgram(previousProgram);
            GL.BindImageTexture(0, 0, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba8);

            GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);
        }
    }
}