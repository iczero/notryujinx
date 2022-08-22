using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Ryujinx.Common;
using Ryujinx.Graphics.OpenGL.Image;
using System;

namespace Ryujinx.Graphics.OpenGL.Effects
{
    internal class FsrUpscaler : IPostProcessingEffect
    {
        private readonly OpenGLRenderer _renderer;
        private int _resolutionUniform;
        private int _inputUniform;
        private int _outputUniform;
        private int _shaderProgram;
        private TextureStorage _textureStorage;

        public FsrUpscaler(OpenGLRenderer renderer)
        {
            Initialize();
            _renderer = renderer;
        }

        public void Dispose()
        {
            if (_shaderProgram != 0)
            {
                GL.DeleteProgram(_shaderProgram);
                _textureStorage?.Dispose();
            }
        }

        public void Initialize()
        {
            var shaderData = EmbeddedResources.ReadAllText("Ryujinx.Graphics.OpenGL/Shaders/fsr_scaling.glsl");
            var fsrA = EmbeddedResources.ReadAllText("Ryujinx.Graphics.OpenGL/Shaders/ffx_a.h");
            var fsr1 = EmbeddedResources.ReadAllText("Ryujinx.Graphics.OpenGL/Shaders/ffx_fsr1.h");

            shaderData = shaderData.Replace("#include \"ffx_a.h\"", fsrA);
            shaderData = shaderData.Replace("#include \"ffx_fsr1.h\"", fsr1);

            var shader = GL.CreateShader(ShaderType.ComputeShader);
            GL.ShaderSource(shader, shaderData);
            GL.CompileShader(shader);
            GL.GetShader(shader, ShaderParameter.CompileStatus, out var status);
            if (status == 0)
            {
                var log = GL.GetShaderInfoLog(shader);
                return;
            }

            _shaderProgram = GL.CreateProgram();
            GL.AttachShader(_shaderProgram, shader);
            GL.LinkProgram(_shaderProgram);

            GL.GetProgram(_shaderProgram, GetProgramParameterName.LinkStatus, out status);
            if (status == 0)
            {
                var log = GL.GetProgramInfoLog(_shaderProgram);
                return;
            }
            GL.DetachShader(_shaderProgram, shader);
            GL.DeleteShader(shader);

            _resolutionUniform = GL.GetUniformLocation(_shaderProgram, "invResolution");
            _inputUniform = GL.GetUniformLocation(_shaderProgram, "input");
            _outputUniform = GL.GetUniformLocation(_shaderProgram, "imgOutput");
        }

        public TextureView Run(TextureView view, int width, int height)
        {
            if(_textureStorage == null || _textureStorage.Info.Width != view.Width || _textureStorage.Info.Height != view.Height)
            {
                _textureStorage?.Dispose();
                _textureStorage = new TextureStorage(_renderer, view.Info, view.ScaleFactor);
                _textureStorage.CreateDefaultView();
            }
            var textureView = _textureStorage.CreateView(view.Info, 0, 0) as TextureView;

            int previousProgram = GL.GetInteger(GetPName.CurrentProgram);
            GL.BindImageTexture(0, textureView.Handle, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba8);
            GL.UseProgram(_shaderProgram);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, view.Handle);
            GL.Uniform1(_inputUniform, 0);
            GL.Uniform1(_outputUniform, 0);
            GL.Uniform2(_resolutionUniform, (float)view.Width, (float)view.Height);
            GL.DispatchCompute(view.Width / IPostProcessingEffect.LocalGroupSize, view.Height / IPostProcessingEffect.LocalGroupSize, 1);
            GL.UseProgram(previousProgram);
            GL.BindImageTexture(0, 0, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba8);

            GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);

            return textureView;
        }
    }
}