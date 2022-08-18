using OpenTK.Graphics.OpenGL;
using Ryujinx.Common;
using System;

namespace Ryujinx.Graphics.OpenGL.Effects
{
    public class SimpleVertexShader : IDisposable
    {
        private readonly float[] _vertices =
        {
             1f,  1f, 0.0f,
             1f, -1f, 0.0f,
            -1f, -1f, 0.0f,
            -1f, 1f, 0.0f, 
        };

        private int _vertexArray;
        private int _vertexBuffer;
        private int _previousVertexArray;
        private int _previousVertexBuffer;

        public int Handle { get; }
        public SimpleVertexShader()
        {
            string shaderData = EmbeddedResources.ReadAllText("Ryujinx.Graphics.OpenGL/Shaders/simple.vert.glsl");
            Handle = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(Handle, shaderData);
            GL.CompileShader(Handle);
            GL.GetShader(Handle, ShaderParameter.CompileStatus, out var status);
            if (status == 0)
            {
                var log = GL.GetShaderInfoLog(Handle);
                return;
            }
            
        }

        public void CreateVertexObjects(int program)
        {
            _vertexArray = GL.GenVertexArray();
            _vertexBuffer = GL.GenBuffer();
            _previousVertexBuffer = GL.GetInteger(GetPName.ArrayBufferBinding);
            Bind();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

            var vertexLocation = GL.GetAttribLocation(program, "aPosition");
            GL.EnableVertexAttribArray(vertexLocation);
            GL.VertexAttribPointer(vertexLocation, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            Unbind();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _previousVertexBuffer);
        }

        public void DeleteShader()
        {
            GL.DeleteShader(Handle);
        }

        public void Dispose()
        {
            if (Handle != 0)
            {
                GL.DeleteBuffer(_vertexBuffer);
                GL.DeleteVertexArray(_vertexArray);
            }
        }

        public void Bind()
        {
            _previousVertexArray = GL.GetInteger(GetPName.VertexArrayBinding);
            GL.BindVertexArray(_vertexArray);
        }

        public void Unbind()
        {
            GL.BindVertexArray(_previousVertexArray);
        }
    }
}