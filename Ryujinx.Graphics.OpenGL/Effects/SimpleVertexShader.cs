using OpenTK.Graphics.OpenGL;
using Ryujinx.Common;
using System;

namespace Ryujinx.Graphics.OpenGL.Effects
{
    public class SimpleVertexShader : IDisposable
    {
        private readonly float[] _vertices =
        {
             1f,  1f, 0.0f, 1.0f, 1.0f,
             1f, -1f, 0.0f, 1.0f, 0.0f,
            -1f, -1f, 0.0f, 0.0f, 0.0f,
            -1f, 1f, 0.0f, 0.0f, 1.0f
        };

        private readonly uint[] _indices =
         {
            0, 1, 3,
            1, 2, 3
        };

        public int ElementCount => _indices.Length;

        private int _vertexArray;
        private int _vertexBuffer;
        private int _previousVertexArray;
        private int _previousVertexBuffer;
        private int _previousElementBuffer;
        private int _elementBuffer;

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
            _previousElementBuffer = GL.GetInteger(GetPName.ElementArrayBufferBinding);
            Bind();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

            _elementBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _elementBuffer);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint), _indices, BufferUsageHint.StaticDraw);

            var vertexLocation = GL.GetAttribLocation(program, "aPosition");
            GL.EnableVertexAttribArray(vertexLocation);
            GL.VertexAttribPointer(vertexLocation, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);

            var texCoordLocation = GL.GetAttribLocation(program, "aTexCoord");
            GL.EnableVertexAttribArray(texCoordLocation);
            GL.VertexAttribPointer(texCoordLocation, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
            Unbind();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _previousVertexBuffer);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _previousElementBuffer);
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
                GL.DeleteBuffer(_elementBuffer);
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