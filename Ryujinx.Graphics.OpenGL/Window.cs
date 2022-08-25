using OpenTK.Graphics.OpenGL;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.OpenGL.Effects;
using Ryujinx.Graphics.OpenGL.Effects.Smaa;
using Ryujinx.Graphics.OpenGL.Image;
using System;

namespace Ryujinx.Graphics.OpenGL
{
    class Window : IWindow, IDisposable
    {
        private const int TextureCount = 3;
        private readonly OpenGLRenderer _renderer;

        private int _width;
        private int _height;
        private bool _sizeChanged;
        private int _copyFramebufferHandle;
        private int _stagingFrameBuffer;
        private int[] _stagingTextures;
        private IPostProcessingEffect _antiAliasing;
        private IScaler _scaler;
        private bool _isLinear;
        private int _currentTexture;
        private AntiAliasing _currentAntiAliasing;
        private bool _changeEffect;
        private UpscaleType _currentScaler;
        private float _upscalerLevel;
        private bool _changeScaler;

        internal BackgroundContextWorker BackgroundContext { get; private set; }

        internal bool ScreenCaptureRequested { get; set; }

        public Window(OpenGLRenderer renderer)
        {
            _renderer = renderer;
            _stagingTextures = new int[TextureCount];
        }

        public void Present(ITexture texture, ImageCrop crop, Action<object> swapBuffersCallback)
        {
            GL.Disable(EnableCap.FramebufferSrgb);

            if (_sizeChanged)
            {
                if (_stagingFrameBuffer != 0)
                {
                    GL.DeleteTextures(_stagingTextures.Length, _stagingTextures);
                    GL.DeleteFramebuffer(_stagingFrameBuffer);
                }

                CreateStagingFramebuffer();
                _sizeChanged = false;
            }

            (int oldDrawFramebufferHandle, int oldReadFramebufferHandle) = ((Pipeline)_renderer.Pipeline).GetBoundFramebuffers();

            CopyTextureToFrameBufferRGB(_stagingFrameBuffer, GetCopyFramebufferHandleLazy(), (TextureView)texture, crop, swapBuffersCallback);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, oldReadFramebufferHandle);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, oldDrawFramebufferHandle);

            GL.Enable(EnableCap.FramebufferSrgb);

            // Restore unpack alignment to 4, as performance overlays such as RTSS may change this to load their resources.
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        }

        public void ChangeVSyncMode(bool vsyncEnabled) { }

        private void CreateStagingFramebuffer()
        {
            _stagingFrameBuffer = GL.GenFramebuffer();
            GL.GenTextures(_stagingTextures.Length, _stagingTextures);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _stagingFrameBuffer);

            foreach (var stagingTexture in _stagingTextures)
            {
                GL.BindTexture(TextureTarget.Texture2D, stagingTexture);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, _width, _height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, stagingTexture, 0);
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        public void SetSize(int width, int height)
        {
            _width = width;
            _height = height;
            _sizeChanged = true;
        }

        private void CopyTextureToFrameBufferRGB(int drawFramebuffer, int readFramebuffer, TextureView view, ImageCrop crop, Action<object> swapBuffersCallback)
        {
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, drawFramebuffer);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, readFramebuffer);

            GL.FramebufferTexture2D(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _stagingTextures[_currentTexture], 0);

            TextureView viewConverted = view.Format.IsBgr() ? _renderer.TextureCopy.BgraSwap(view) : view;

            UpdateEffect();

            if (_antiAliasing != null)
            {
                viewConverted = _antiAliasing.Run(viewConverted, _width, _height);
            }
            
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, drawFramebuffer);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, readFramebuffer);

            GL.FramebufferTexture(
                FramebufferTarget.ReadFramebuffer,
                FramebufferAttachment.ColorAttachment0,
                viewConverted.Handle,
                0);

            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);

            GL.Disable(EnableCap.RasterizerDiscard);
            GL.Disable(IndexedEnableCap.ScissorTest, 0);

            GL.Clear(ClearBufferMask.ColorBufferBit);

            int srcX0, srcX1, srcY0, srcY1;
            float scale = viewConverted.ScaleFactor;

            if (crop.Left == 0 && crop.Right == 0)
            {
                srcX0 = 0;
                srcX1 = (int)(viewConverted.Width / scale);
            }
            else
            {
                srcX0 = crop.Left;
                srcX1 = crop.Right;
            }

            if (crop.Top == 0 && crop.Bottom == 0)
            {
                srcY0 = 0;
                srcY1 = (int)(viewConverted.Height / scale);
            }
            else
            {
                srcY0 = crop.Top;
                srcY1 = crop.Bottom;
            }

            if (scale != 1f)
            {
                srcX0 = (int)(srcX0 * scale);
                srcY0 = (int)(srcY0 * scale);
                srcX1 = (int)Math.Ceiling(srcX1 * scale);
                srcY1 = (int)Math.Ceiling(srcY1 * scale);
            }

            float ratioX = crop.IsStretched ? 1.0f : MathF.Min(1.0f, _height * crop.AspectRatioX / (_width * crop.AspectRatioY));
            float ratioY = crop.IsStretched ? 1.0f : MathF.Min(1.0f, _width * crop.AspectRatioY / (_height * crop.AspectRatioX));

            int dstWidth = (int)(_width * ratioX);
            int dstHeight = (int)(_height * ratioY);

            int dstPaddingX = (_width - dstWidth) / 2;
            int dstPaddingY = (_height - dstHeight) / 2;

            int dstX0 = crop.FlipX ? _width - dstPaddingX : dstPaddingX;
            int dstX1 = crop.FlipX ? dstPaddingX : _width - dstPaddingX;

            int dstY0 = crop.FlipY ? dstPaddingY : _height - dstPaddingY;
            int dstY1 = crop.FlipY ? _height - dstPaddingY : dstPaddingY;

            if (ScreenCaptureRequested)
            {
                CaptureFrame(srcX0, srcY0, srcX1, srcY1, view.Format.IsBgr(), crop.FlipX, crop.FlipY);

                ScreenCaptureRequested = false;
            }

            if (_scaler != null)
            {
                viewConverted = _scaler.Run(viewConverted, dstWidth, dstHeight);

                srcX0 = 0;
                srcY0 = 0;
                srcX1 = viewConverted.Width;
                srcY1 = viewConverted.Height;

                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, drawFramebuffer);
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, readFramebuffer);

                GL.FramebufferTexture(
                    FramebufferTarget.ReadFramebuffer,
                    FramebufferAttachment.ColorAttachment0,
                    viewConverted.Handle,
                    0);
            }

            GL.BlitFramebuffer(
                srcX0,
                srcY0,
                srcX1,
                srcY1,
                dstX0,
                dstY0,
                dstX1,
                dstY1,
                ClearBufferMask.ColorBufferBit,
                _isLinear ? BlitFramebufferFilter.Linear : BlitFramebufferFilter.Nearest);

            // Remove Alpha channel
            GL.ColorMask(false, false, false, true);
            GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            for (int i = 0; i < Constants.MaxRenderTargets; i++)
            {
                ((Pipeline)_renderer.Pipeline).RestoreComponentMask(i);
            }

            // Set clip control, viewport and the framebuffer to the output to placate overlays and OBS capture.
            GL.ClipControl(ClipOrigin.LowerLeft, ClipDepthMode.NegativeOneToOne);
            GL.Viewport(0, 0, _width, _height);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, drawFramebuffer);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _stagingFrameBuffer);

            swapBuffersCallback((object)_stagingTextures[_currentTexture]);
            _currentTexture = ++_currentTexture % _stagingTextures.Length;

            ((Pipeline)_renderer.Pipeline).RestoreClipControl();
            ((Pipeline)_renderer.Pipeline).RestoreScissor0Enable();
            ((Pipeline)_renderer.Pipeline).RestoreRasterizerDiscard();
            ((Pipeline)_renderer.Pipeline).RestoreViewport0();

            if (viewConverted != view)
            {
                viewConverted.Dispose();
            }
        }

        private int GetCopyFramebufferHandleLazy()
        {
            int handle = _copyFramebufferHandle;

            if (handle == 0)
            {
                handle = GL.GenFramebuffer();

                _copyFramebufferHandle = handle;
            }

            return handle;
        }

        public void InitializeBackgroundContext(IOpenGLContext baseContext)
        {
            BackgroundContext = new BackgroundContextWorker(baseContext);
        }

        public void CaptureFrame(int x, int y, int width, int height, bool isBgra, bool flipX, bool flipY)
        {
            long size = Math.Abs(4 * width * height);
            byte[] bitmap = new byte[size];

            GL.ReadPixels(x, y, width, height, isBgra ? PixelFormat.Bgra : PixelFormat.Rgba, PixelType.UnsignedByte, bitmap);

            _renderer.OnScreenCaptured(new ScreenCaptureImageInfo(width, height, isBgra, bitmap, flipX, flipY));
        }

        public void Dispose()
        {
            BackgroundContext.Dispose();

            if (_copyFramebufferHandle != 0)
            {
                GL.DeleteFramebuffer(_copyFramebufferHandle);

                _copyFramebufferHandle = 0;
            }

            if (_stagingFrameBuffer != 0)
            {
                GL.DeleteTextures(_stagingTextures.Length, _stagingTextures);
                GL.DeleteFramebuffer(_stagingFrameBuffer);
                _stagingFrameBuffer = 0;
                _stagingTextures = null;
            }

            _antiAliasing?.Dispose();
            _scaler?.Dispose();
        }

        public void ApplyEffect(AntiAliasing effect)
        {
            if (_currentAntiAliasing == effect && _antiAliasing != null)
            {
                _currentAntiAliasing = effect;
                return;
            }

            _currentAntiAliasing = effect;

            _changeEffect = true;
        }

        public void ApplyScaler(UpscaleType scalerType)
        {

            if (_currentScaler == scalerType && _antiAliasing != null)
            {
                _currentScaler = scalerType;
                return;
            }

            _currentScaler = scalerType;

            _changeScaler = true;
        }

        private void UpdateEffect()
        {
            if (_changeEffect)
            {
                _changeEffect = false;

                switch (_currentAntiAliasing)
                {
                    case AntiAliasing.Fxaa:
                        _antiAliasing?.Dispose();
                        _antiAliasing = null;
                        _antiAliasing = new FxaaPostProcessingEffect(_renderer);
                        break;
                    case AntiAliasing.None:
                        _antiAliasing?.Dispose();
                        _antiAliasing = null;
                        break;
                    case AntiAliasing.SmaaLow:
                        if (_antiAliasing is SmaaPostProcessingEffect smaa)
                        {
                            smaa.Quality = 0;
                        }
                        else
                        {
                            _antiAliasing?.Dispose();
                            _antiAliasing = new SmaaPostProcessingEffect(_renderer, 0);
                        }
                        break;
                    case AntiAliasing.SmaaMedium:
                        if (_antiAliasing is SmaaPostProcessingEffect smaam)
                        {
                            smaam.Quality = 1;
                        }
                        else
                        {
                            _antiAliasing?.Dispose();
                            _antiAliasing = new SmaaPostProcessingEffect(_renderer, 1);
                        }
                        break;
                    case AntiAliasing.SmaaHigh:
                        if (_antiAliasing is SmaaPostProcessingEffect smaah)
                        {
                            smaah.Quality = 2;
                        }
                        else
                        {
                            _antiAliasing?.Dispose();
                            _antiAliasing = new SmaaPostProcessingEffect(_renderer, 2);
                        }
                        break;
                    case AntiAliasing.SmaaUltra:
                        if (_antiAliasing is SmaaPostProcessingEffect smaau)
                        {
                            smaau.Quality = 3;
                        }
                        else
                        {
                            _antiAliasing?.Dispose();
                            _antiAliasing = new SmaaPostProcessingEffect(_renderer, 3);
                        }
                        break;
                }
            }

            if (_changeScaler)
            {
                _changeScaler = false;

                switch (_currentScaler)
                {
                    case UpscaleType.Bilinear:
                    case UpscaleType.Nearest:
                        _scaler?.Dispose();
                        _scaler = null;
                        _isLinear = _currentScaler == UpscaleType.Bilinear;
                        break;
                    case UpscaleType.Fsr:
                        if (!(_scaler is FsrUpscaler))
                        {
                            _scaler?.Dispose();
                            _scaler = new FsrUpscaler(_renderer, _antiAliasing);
                        }

                        _scaler.Level = _upscalerLevel;
                        break;
                }
            }
        }

        public void SetUpscalerLevel(float level)
        {
            _upscalerLevel = level;

            _changeScaler = true;
        }
    }
}
