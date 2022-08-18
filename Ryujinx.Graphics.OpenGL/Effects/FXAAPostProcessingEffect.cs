using OpenTK.Graphics.OpenGL;
using Ryujinx.Common;
using Ryujinx.Graphics.OpenGL.Image;
using System;

namespace Ryujinx.Graphics.OpenGL.Effects
{
    internal class FXAAPostProcessingEffect : IPostProcessingEffect
    {
        private const int LocalGroupSize = 10;
        private readonly string _shader;
        private readonly OpenGLRenderer _renderer;
        private int _resolutionUniform;
        private int _textureUniform;
        private int _shaderProgram;
        private int _framebuffer;
        private int _width;
        private int _height;
        private TextureView _textureView;
        private SimpleVertexShader _vertexShader;
        private int _renderBuffer;

        public FXAAPostProcessingEffect(OpenGLRenderer renderer)
        {
            Initialize();
            _renderer = renderer;
            _framebuffer = GL.GenFramebuffer();
        }

        public void Dispose()
        {
            if (_shaderProgram != 0)
            {
                _vertexShader.Dispose();
                GL.DeleteProgram(_shaderProgram);
                GL.DeleteFramebuffer(_framebuffer);
                _textureView?.Dispose();
                GL.DeleteRenderbuffer(_renderBuffer);
            }
        }

        public void Initialize()
        {
            _vertexShader = new SimpleVertexShader();
            var shaderData = EmbeddedResources.ReadAllText("Ryujinx.Graphics.OpenGL/Shaders/fxaa.glsl");
            var shader = GL.CreateShader(ShaderType.FragmentShader);
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
            GL.AttachShader(_shaderProgram, _vertexShader.Handle);
            GL.LinkProgram(_shaderProgram);

            GL.GetProgram(_shaderProgram, GetProgramParameterName.LinkStatus, out status);
            if (status == 0)
            {
                var log = GL.GetProgramInfoLog(_shaderProgram);
                return;
            }
            GL.ValidateProgram(_shaderProgram);
            GL.DetachShader(_shaderProgram, _vertexShader.Handle);
            GL.DetachShader(_shaderProgram, shader);
            GL.DeleteShader(shader);
            _vertexShader.DeleteShader();
            _vertexShader.CreateVertexObjects(_shaderProgram);

            _resolutionUniform = GL.GetUniformLocation(_shaderProgram, "invResolution");
            _textureUniform = GL.GetUniformLocation(_shaderProgram, "textureUnit");
        }

        public TextureView Run(TextureView view)
        {
            if(_width != view.Width || _height != view.Height) 
            {
                _textureView?.Dispose();
                GL.DeleteRenderbuffer(_renderBuffer);

                GenerateRenderTarget(view.Info, view.ScaleFactor, _framebuffer, out _textureView, out _renderBuffer);
                _width = view.Width;
                _height = view.Height;
            }

            var _previousProgram = GL.GetInteger(GetPName.CurrentProgram);
            GL.UseProgram(_shaderProgram);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
            view.Bind(0);

            _vertexShader.Bind();
            GL.Uniform2(_resolutionUniform, (float)_width, (float)_height);
            GL.Uniform1(_textureUniform, 0);
            GL.DrawElements(PrimitiveType.Triangles, _vertexShader.ElementCount, DrawElementsType.UnsignedInt, 0);

            _vertexShader.Unbind();
            GL.UseProgram(_previousProgram);

            _textureView.CopyTo(view, 0, 0);

            return view;
        }

        public void GenerateRenderTarget(GAL.TextureCreateInfo info, float scaleFactor, int framebuffer, out TextureView texture, out int renderbuffer)
        {            
            texture = _renderer.CreateTexture(info, scaleFactor) as TextureView;

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
            GL.BindTexture(TextureTarget.Texture2D, texture.Handle);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture.Handle, 0);

            renderbuffer = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, renderbuffer);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, texture.Width, texture.Height);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, renderbuffer);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
    }
}