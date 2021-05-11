using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Ryujinx.Ava.Ui.Windows
{
    internal static class LocalizationLoader
    {
        public static void LoadFromEmbeddedResource(Dictionary<string, string> strings, string embeddedPath)
        {
            Stream stream = GetEmbeddedResourceStream(embeddedPath);
            if (stream == null)
            {
                return;
            }

            LoadFromStream(strings, stream);
        }

        private static Stream GetEmbeddedResourceStream(string embeddedPath)
        {
            return Assembly.GetCallingAssembly().GetManifestResourceStream(embeddedPath);
        }

        public static void LoadFromStream(Dictionary<string, string> strings, Stream input)
        {
            StreamReader reader = new(input);

            while (true)
            {
                string line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex < 0)
                {
                    continue;
                }

                string key = line.Substring(0, separatorIndex);
                string value = line.Substring(separatorIndex + 1);

                strings[key] = value.Replace("\\n", "\n");
            }
        }
    }
}