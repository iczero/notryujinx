using OpenTK.Graphics.OpenGL;
using Ryujinx.Common;
using Ryujinx.Graphics.OpenGL.Image;
using System;

namespace Ryujinx.Graphics.OpenGL
{
    internal class PostProcessingEffect : IDisposable
    {
        private const int LocalGroupSize = 10;
        private readonly string _shader;
        private int _shaderProgram;

        public PostProcessingEffect(string shader)
        {
            _shader = shader;

            Initialize();
        }

        public void Dispose()
        {
            if (_shaderProgram != 0)
            {
                GL.DeleteProgram(_shaderProgram);
            }
        }

        private void Initialize()
        {
            var shaderData = EmbeddedResources.ReadAllText(_shader);
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
        }

        public void Run(TextureView view)
        {
            int previousProgram = GL.GetInteger(GetPName.CurrentProgram);
            GL.BindImageTexture(0, view.Handle, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba8);
            GL.UseProgram(_shaderProgram);
            GL.DispatchCompute(view.Width / LocalGroupSize, view.Height / LocalGroupSize, 1);
            GL.UseProgram(previousProgram);
            GL.BindImageTexture(0, 0, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba8);

            GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);
        }
    }
}