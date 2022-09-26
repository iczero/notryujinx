using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Ryujinx.Common;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.OpenGL.Effects;
using Ryujinx.Graphics.OpenGL.Image;
using System;

namespace Ryujinx.Graphics.OpenGL
{
    internal class Osd
    {
        private const int OuterPadding = 23;
        private const int InnerPadding = 7;
        private TextureStorage _fontAtlas;
        private readonly Common.Osd _osd;
        private readonly OpenGLRenderer _renderer;
        private byte[] _dataUniformBuffer;
        private BufferHandle _dataBuffer;
        private int _shaderProgram;
        private int _colorUniform;
        private int _paddingUniform;
        private int _lineHeightUniform;
        private int _fontAtlasUniform;
        private int _edgeUniform;
        private int _sizeUniform;
        private int _outputUniform;
        private int _drawUniform;
        private int _scaleUniform;
        private bool _isUpdated;

        public Osd(OpenGLRenderer renderer, Common.Osd osd)
        {
            _osd = osd;
            _osd.ContentUpdated += ContentUpdated;
            _renderer = renderer;
            Initialize();
        }

        private void ContentUpdated(object sender, EventArgs e)
        {
            _isUpdated = true;
        }

        public void Dispose()
        {
            _osd.ContentUpdated -= ContentUpdated;
            _isUpdated = false;
            _fontAtlas?.Dispose();
            Buffer.Delete(_dataBuffer);
        }

        public void Initialize()
        {
            var shaderData = EmbeddedResources.ReadAllText("Ryujinx.Graphics.OpenGL/Shaders/osd.glsl");
            _shaderProgram = ShaderHelper.CompileProgram(shaderData, ShaderType.ComputeShader);

            _colorUniform = GL.GetUniformLocation(_shaderProgram, "color");
            _paddingUniform = GL.GetUniformLocation(_shaderProgram, "padding");
            _lineHeightUniform = GL.GetUniformLocation(_shaderProgram, "lineHeight");
            _fontAtlasUniform = GL.GetUniformLocation(_shaderProgram, "fontAtlas");
            _edgeUniform = GL.GetUniformLocation(_shaderProgram, "edge");
            _sizeUniform = GL.GetUniformLocation(_shaderProgram, "size");
            _outputUniform = GL.GetUniformLocation(_shaderProgram, "img");
            _drawUniform = GL.GetUniformLocation(_shaderProgram, "draw");
            _scaleUniform = GL.GetUniformLocation(_shaderProgram, "scale");

            _dataBuffer = Buffer.Create();

            CreateTextureAtlas();
        }

        public void UpdateStorage()
        {
            _dataUniformBuffer = _osd.CurrentContentMapData;

            Buffer.Delete(_dataBuffer);

            _dataBuffer = Buffer.Create(_dataUniformBuffer.Length);
            Buffer.SetData(_dataBuffer, 0, _dataUniformBuffer);
        }

        private void CreateTextureAtlas()
        {
            var atlasInfo = new TextureCreateInfo(Common.Osd.AtlasTexturSize,
                Common.Osd.AtlasTexturSize,
                1,
                1,
                1,
                1,
                1,
                4,
                Format.R8G8B8A8Unorm,
                DepthStencilMode.Depth,
                Target.Texture2D,
                SwizzleComponent.Red,
                SwizzleComponent.Green,
                SwizzleComponent.Blue,
                SwizzleComponent.Alpha);

            _fontAtlas = new TextureStorage(_renderer, atlasInfo, 1);
            var view = _fontAtlas.CreateDefaultView();
            view.SetData(_osd.GetAtlasTextureData());
        }

        public TextureView Draw(TextureView view)
        {
            if (_isUpdated)
            {
                UpdateStorage();
            }

            _isUpdated = false;

            var textureView = view.CreateView(view.Info, 0, 0) as TextureView;

            int scale = (int)view.ScaleFactor;

            Vector2 padding = default;

            switch (_osd.Location)
            {
                case OsdLocation.TopLeft:
                    padding = new Vector2(OuterPadding);
                    break;
                case OsdLocation.TopRight:
                    padding = new Vector2(view.Width / scale - _osd.Width - OuterPadding, OuterPadding);
                    break;
                case OsdLocation.BottomLeft:
                    padding = new Vector2(OuterPadding, view.Height / scale - _osd.Height * _osd.LineHeight - OuterPadding);
                    break;
                case OsdLocation.BottomRight:
                    padding = new Vector2(view.Width / scale - _osd.Width - OuterPadding, view.Height / scale - _osd.Height * _osd.LineHeight - OuterPadding);
                    break;
            }

            int previousProgram = GL.GetInteger(GetPName.CurrentProgram);
            GL.BindImageTexture(0, textureView.Handle, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba8);
            GL.UseProgram(_shaderProgram);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, (_fontAtlas.DefaultView as TextureView).Handle);
            GL.Uniform1(_fontAtlasUniform, 0);
            GL.Uniform1(_outputUniform, 0);
            GL.Uniform1(_lineHeightUniform, _osd.LineHeight);
            GL.Uniform4(_colorUniform, Color4.Yellow);
            GL.Uniform2(_paddingUniform, padding.X * scale, padding.Y * scale);
            GL.Uniform1(_edgeUniform, (uint)view.Height);
            GL.Uniform1(_sizeUniform, (uint)Common.Osd.AtlasTexturSize);
            GL.Uniform1(_drawUniform, (uint)0);
            GL.Uniform1(_scaleUniform, scale);

            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _dataBuffer.ToInt32());

            var length = _osd.Length - 1;
            int threadGroupWorkRegionDim = 16;
            GL.DispatchCompute((_osd.Width + threadGroupWorkRegionDim - 1) / threadGroupWorkRegionDim * scale, (_osd.Height * _osd.LineHeight + threadGroupWorkRegionDim - 1) / threadGroupWorkRegionDim * scale, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);
            GL.Uniform1(_drawUniform, (uint)1);
            GL.Uniform2(_paddingUniform, padding.X + InnerPadding, padding.Y + InnerPadding);
            GL.DispatchCompute((length + threadGroupWorkRegionDim - 1) / threadGroupWorkRegionDim, 1, 1);
            GL.UseProgram(previousProgram);
            GL.BindImageTexture(0, 0, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba8);

            GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);

            return textureView;
        }
    }
}
