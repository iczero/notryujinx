using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.Graphics.OpenGL.Effects
{
    internal static class ShaderHelper
    {
        public static int CompileProgram(string shaderCode, ShaderType shaderType)
        {
            var shader = GL.CreateShader(ShaderType.ComputeShader);
            GL.ShaderSource(shader, shaderCode);
            GL.CompileShader(shader);
            GL.GetShader(shader, ShaderParameter.CompileStatus, out _);

            var program = GL.CreateProgram();
            GL.AttachShader(program, shader);
            GL.LinkProgram(program);

            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out _);
            GL.DetachShader(program, shader);
            GL.DeleteShader(shader);

            return program;
        }
    }
}
