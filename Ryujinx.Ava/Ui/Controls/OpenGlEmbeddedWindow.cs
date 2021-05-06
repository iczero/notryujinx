using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;
using PInvoke;
using Ryujinx.Common.Configuration;
using SPB.Graphics;
using SPB.Platform.GLX;
using SPB.Platform.WGL;
using SPB.Windowing;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Ava.Ui.Controls
{
    public class OpenGlEmbeddedWindow : NativeEmbeddedWindow
    {
        public OpenGlEmbeddedWindow(int major, int minor, GraphicsDebugLevel graphicsDebugLevel)
        {
            Major = major;
            Minor = minor;
            DebugLevel = graphicsDebugLevel;
            
            GLFW.WindowHint(WindowHintClientApi.ClientApi, ClientApi.OpenGlApi);
            GLFW.WindowHint(WindowHintInt.ContextVersionMajor, major);
            GLFW.WindowHint(WindowHintInt.ContextVersionMinor, minor);
            GLFW.WindowHint(WindowHintBool.OpenGLForwardCompat, true);

            if (DebugLevel != GraphicsDebugLevel.None)
            {
                GLFW.WindowHint(WindowHintBool.OpenGLDebugContext, true);
            }
        }
        
        public NativeWindowBase Window { get; set; }

        public override unsafe void OnWindowCreated()
        {
            base.OnWindowCreated();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Window = new WGLWindow(new NativeHandle(WindowHandle));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Window = new GLXWindow(new NativeHandle(X11Display), new NativeHandle(WindowHandle));
            }

            GLFWWindow.MakeCurrent();

            GL.LoadBindings(new OpenToolkitBindingsContext());
            GLFW.MakeContextCurrent(null);
        }

        public void MakeCurrent()
        {
            GLFWWindow.MakeCurrent();
        }

        public unsafe void MakeCurrent(Window* window)
        {
            GLFW.MakeContextCurrent(window);
        }

        public override void Present()
        {
            GLFWWindow.SwapBuffers();
        }
    }
}