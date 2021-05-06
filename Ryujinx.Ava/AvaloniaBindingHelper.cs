using OpenTK;
using System;
using System.Reflection;

namespace Ryujinx.Ava
{
    public class AvaloniaBindingHelper : IBindingsContext
    {
        private readonly Func<string, IntPtr> getProcAddress;

        public AvaloniaBindingHelper(Func<string, IntPtr> getProcAddress)
        {
            this.getProcAddress = getProcAddress;
        }

        public IntPtr GetProcAddress(string procName)
        {
            return getProcAddress(procName);
        }

        public static void InitializeGlBindings(Func<string, IntPtr> getProcAddress)
        {
            // We don't put a hard dependency on OpenTK.Graphics here.
            // So we need to use reflection to initialize the GL bindings, so users don't have to.

            // Try to load OpenTK.Graphics assembly.
            Assembly assembly;

            try
            {
                assembly = Assembly.Load("OpenTK.Graphics");
            }
            catch
            {
                // Failed to load graphics, oh well.
                // Up to the user I guess?
                // TODO: Should we expose this load failure to the user better?
                return;
            }

            AvaloniaBindingHelper provider = new(getProcAddress);

            void LoadBindings(string typeNamespace)
            {
                Type? type = assembly.GetType($"OpenTK.Graphics.{typeNamespace}.GL");
                if (type == null)
                {
                    return;
                }

                MethodInfo? load = type.GetMethod("LoadBindings");
                load.Invoke(null, new object[] {provider});
            }

            LoadBindings("ES11");
            LoadBindings("ES20");
            LoadBindings("ES30");
            LoadBindings("OpenGL");
            LoadBindings("OpenGL4");
        }
    }
}