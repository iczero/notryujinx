using OpenTK.Graphics.OpenGL;
using Ryujinx.Common;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.OpenGL.Image;
using System;

namespace Ryujinx.Graphics.OpenGL.Effects.Smaa
{
    internal partial class SmaaPostProcessingEffect : IPostProcessingEffect
    {
        private readonly OpenGLRenderer _renderer;
        private TextureStorage _outputTexture;
        private TextureStorage _searchTexture;
        private TextureStorage _areaTexture;
        private int[] _edgeShaderPrograms;
        private int[] _blendShaderPrograms;
        private int[] _neighbourShaderPrograms;
        private TextureStorage _edgeOutputTexture;
        private TextureStorage _blendOutputTexture;
        private string[] _qualities;
        private int _inputUniform;
        private int _outputUniform;
        private int _samplerAreaUniform;
        private int _samplerSearchUniform;
        private int _samplerBlendUniform;
        private int _resolutionUniform;

        public int Quality { get; set; } = 1;

        public SmaaPostProcessingEffect(OpenGLRenderer renderer, int quality)
        {
            _renderer = renderer;

            _edgeShaderPrograms = Array.Empty<int>();
            _blendShaderPrograms = Array.Empty<int>();
            _neighbourShaderPrograms = Array.Empty<int>();

            _qualities = new string[] { "SMAA_PRESET_LOW", "SMAA_PRESET_MEDIUM", "SMAA_PRESET_HIGH", "SMAA_PRESET_ULTRA" };

            Quality = quality;

            Initialize();
        }

        public void Dispose()
        {
            _searchTexture?.Dispose();
            _areaTexture?.Dispose();
            _outputTexture?.Dispose();
            _edgeOutputTexture?.Dispose();
            _blendOutputTexture?.Dispose();

            DeleteShaders();
        }

        private void DeleteShaders()
        {
            for (int i = 0; i < _edgeShaderPrograms.Length; i++)
            {
                GL.DeleteProgram(_edgeShaderPrograms[i]);
                GL.DeleteProgram(_blendShaderPrograms[i]);
                GL.DeleteProgram(_neighbourShaderPrograms[i]);
            }
        }

        private unsafe void RecreateShaders(int width, int height)
        {
            string baseShader = EmbeddedResources.ReadAllText("Ryujinx.Graphics.OpenGL/Shaders/smaa.hlsl");
            var pixelSizeDefine = string.Format("#define SMAA_RT_METRICS float4(1.0 / {0}.0, 1.0 / {1}.0, {2}, {3}) \n", width, height, width, height);

            _edgeShaderPrograms = new int[_qualities.Length];
            _blendShaderPrograms = new int[_qualities.Length];
            _neighbourShaderPrograms = new int[_qualities.Length];

            for (int i = 0; i < +_edgeShaderPrograms.Length; i++)
            {
                var presets = "#version 430 core \n#define " + _qualities[i] + " 1 \n" + pixelSizeDefine + "#define SMAA_GLSL_4 1 \n";
                presets += "\nlayout (local_size_x = 16, local_size_y = 16) in;\n" + baseShader;

                var edgeShaderData = EmbeddedResources.ReadAllText("Ryujinx.Graphics.OpenGL/Shaders/smaa_edge.glsl");
                var blendShaderData = EmbeddedResources.ReadAllText("Ryujinx.Graphics.OpenGL/Shaders/smaa_blend.glsl");
                var neighbourShaderData = EmbeddedResources.ReadAllText("Ryujinx.Graphics.OpenGL/Shaders/smaa_neighbour.glsl");

                var edgeProgram = GL.CreateProgram();
                var shader = GL.CreateShader(ShaderType.ComputeShader);
                var shaders = new string[] { presets, edgeShaderData };
                GL.ShaderSource(shader, 2, shaders, (int[])null);
                GL.CompileShader(shader);
                GL.GetShader(shader, ShaderParameter.CompileStatus, out var status);
                if (status == 0)
                {
                    var log = GL.GetShaderInfoLog(shader);
                    return;
                }
                GL.AttachShader(edgeProgram, shader);
                GL.LinkProgram(edgeProgram);
                GL.GetProgram(edgeProgram, GetProgramParameterName.LinkStatus, out status);
                if (status == 0)
                {
                    var log = GL.GetProgramInfoLog(edgeProgram);
                    return;
                }
                GL.DeleteShader(shader);

                var blendProgram = GL.CreateProgram();
                shader = GL.CreateShader(ShaderType.ComputeShader);
                shaders[1] = blendShaderData;
                GL.ShaderSource(shader, 2, shaders, (int*)IntPtr.Zero);
                GL.CompileShader(shader);
                GL.GetShader(shader, ShaderParameter.CompileStatus, out status);
                if (status == 0)
                {
                    var log = GL.GetShaderInfoLog(shader);
                    return;
                }
                GL.AttachShader(blendProgram, shader);
                GL.LinkProgram(blendProgram);

                GL.GetProgram(blendProgram, GetProgramParameterName.LinkStatus, out status);
                if (status == 0)
                {
                    var log = GL.GetProgramInfoLog(blendProgram);
                    return;
                }
                GL.DeleteShader(shader);

                var neighbourProgram = GL.CreateProgram();
                shader = GL.CreateShader(ShaderType.ComputeShader);
                shaders[1] = neighbourShaderData;
                GL.ShaderSource(shader, 2, shaders, (int*)IntPtr.Zero);
                GL.CompileShader(shader);
                GL.GetShader(shader, ShaderParameter.CompileStatus, out status);
                if (status == 0)
                {
                    var log = GL.GetShaderInfoLog(shader);
                    return;
                }
                GL.AttachShader(neighbourProgram, shader);
                GL.LinkProgram(neighbourProgram);

                GL.GetProgram(neighbourProgram, GetProgramParameterName.LinkStatus, out status);
                if (status == 0)
                {
                    var log = GL.GetProgramInfoLog(neighbourProgram);
                    return;
                }
                GL.DeleteShader(shader);

                _edgeShaderPrograms[i] = edgeProgram;
                _blendShaderPrograms[i] = blendProgram;
                _neighbourShaderPrograms[i] = neighbourProgram;
            }

            _inputUniform = GL.GetUniformLocation(_edgeShaderPrograms[0], "inputTexture");
            _outputUniform = GL.GetUniformLocation(_edgeShaderPrograms[0], "imgOutput");
            _samplerAreaUniform = GL.GetUniformLocation(_blendShaderPrograms[0], "samplerArea");
            _samplerSearchUniform = GL.GetUniformLocation(_blendShaderPrograms[0], "samplerSearch");
            _samplerBlendUniform = GL.GetUniformLocation(_neighbourShaderPrograms[0], "samplerBlend");
            _resolutionUniform = GL.GetUniformLocation(_edgeShaderPrograms[0], "invResolution");
        }

        public void Initialize()
        {
            var areaInfo = new TextureCreateInfo(AreaWidth,
                AreaHeight,
                1,
                1,
                1,
                1,
                1,
                1,
                Format.R8G8Unorm,
                DepthStencilMode.Depth,
                Target.Texture2D,
                SwizzleComponent.Red,
                SwizzleComponent.Green,
                SwizzleComponent.Blue,
                SwizzleComponent.Alpha);

            var searchInfo = new TextureCreateInfo(SearchWidth,
                SearchHeight,
                1,
                1,
                1,
                1,
                1,
                1,
                Format.R8Unorm,
                DepthStencilMode.Depth,
                Target.Texture2D,
                SwizzleComponent.Red,
                SwizzleComponent.Green,
                SwizzleComponent.Blue,
                SwizzleComponent.Alpha);

            _areaTexture = new TextureStorage(_renderer, areaInfo, 1);
            _searchTexture = new TextureStorage(_renderer, searchInfo, 1);

            var areaView = _areaTexture.CreateDefaultView();
            var searchView = _searchTexture.CreateDefaultView();

            areaView.SetData(AreaTexture);
            searchView.SetData(SearchTexBytes);
        }

        public TextureView Run(TextureView view, int width, int height)
        {
            if (_outputTexture == null || _outputTexture.Info.Width != view.Width || _outputTexture.Info.Height != view.Height)
            {
                _outputTexture?.Dispose();
                _outputTexture = new TextureStorage(_renderer, view.Info, view.ScaleFactor);
                _outputTexture.CreateDefaultView();
                _edgeOutputTexture = new TextureStorage(_renderer, view.Info, view.ScaleFactor);
                _edgeOutputTexture.CreateDefaultView();
                _blendOutputTexture = new TextureStorage(_renderer, view.Info, view.ScaleFactor);
                _blendOutputTexture.CreateDefaultView();

                DeleteShaders();

                RecreateShaders(view.Width, view.Height);
            }

            var textureView = _outputTexture.CreateView(view.Info, 0, 0) as TextureView;
            var edgeOutput = _edgeOutputTexture.DefaultView as TextureView;
            var blendOutput = _blendOutputTexture.DefaultView as TextureView;
            var areaTexture = _areaTexture.DefaultView as TextureView;
            var searchTexture = _searchTexture.DefaultView as TextureView;

            var framebuffer = new Framebuffer();
            framebuffer.Bind();
            framebuffer.AttachColor(0, edgeOutput);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.ClearColor(0, 0, 0, 0);
            framebuffer.AttachColor(0, blendOutput);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.ClearColor(0, 0, 0, 0);

            framebuffer.Dispose();

            var dispatchX = (view.Width + IPostProcessingEffect.LocalGroupSize - 1) / IPostProcessingEffect.LocalGroupSize;
            var dispatchY = (view.Height + IPostProcessingEffect.LocalGroupSize - 1) / IPostProcessingEffect.LocalGroupSize;

            int previousProgram = GL.GetInteger(GetPName.CurrentProgram);
            GL.BindImageTexture(0, edgeOutput.Handle, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba8);
            GL.UseProgram(_edgeShaderPrograms[Quality]);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, view.Handle);
            GL.Uniform1(_inputUniform, 0);
            GL.Uniform1(_outputUniform, 0);
            GL.Uniform2(_resolutionUniform, (float)view.Width, (float)view.Height);
            GL.DispatchCompute(dispatchX, dispatchY, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);

            GL.BindImageTexture(0, blendOutput.Handle, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba8);
            GL.UseProgram(_blendShaderPrograms[Quality]);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, edgeOutput.Handle);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, areaTexture.Handle);
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, searchTexture.Handle);
            GL.Uniform1(_inputUniform, 0);
            GL.Uniform1(_outputUniform, 0);
            GL.Uniform1(_samplerAreaUniform, 1);
            GL.Uniform1(_samplerSearchUniform, 2);
            GL.Uniform2(_resolutionUniform, (float)view.Width, (float)view.Height);
            GL.DispatchCompute(dispatchX, dispatchY, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);


            GL.BindImageTexture(0, textureView.Handle, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba8);
            GL.UseProgram(_neighbourShaderPrograms[Quality]);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, view.Handle);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, blendOutput.Handle);
            GL.Uniform1(_inputUniform, 0);
            GL.Uniform1(_outputUniform, 0);
            GL.Uniform1(_samplerBlendUniform, 1);
            GL.Uniform2(_resolutionUniform, (float)view.Width, (float)view.Height);
            GL.DispatchCompute(dispatchX, dispatchY, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);

            (_renderer.Pipeline as Pipeline).RestoreImages1And2();


            GL.UseProgram(previousProgram);

            return textureView;
        }
    }
}
