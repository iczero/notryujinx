using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using IO = System.IO;

namespace Ryujinx.Common
{
    public class Osd
    {
        private readonly Dictionary<int, Character> _characterMap;
        public const int AtlasTexturSize = 2300;

        public byte[] CurrentContentMapData { get; private set; }
        public int Length { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public event EventHandler ContentUpdated;

        public bool IsEnabled { get; set; }

        public OsdLocation Location { get; set; } = OsdLocation.TopLeft;
        public int LineHeight { get; set; } = 18;

        public Osd()
        {
            _characterMap = new Dictionary<int, Character>();
            var fontMetadata = EmbeddedResources.Read("Ryujinx.Ui.Common/Resources/atlas/noto-bmfont.fnt");
            using var fontContent = new MemoryStream(fontMetadata);
            int count;
            using (var textReader = new IO.StreamReader(fontContent))
            {
                int i = 0;
                while (!textReader.EndOfStream)
                {
                    var line = textReader.ReadLine();

                    if (!line.StartsWith("char"))
                    {
                        continue;
                    }

                    if (line.StartsWith("chars"))
                    {
                        int.TryParse(line.Split("=", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[1], out count);
                    }
                    else
                    {
                        var reg = new Regex("\\s");
                        var parts = reg.Split(line);
                        parts = parts.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

                        int id = GetValue(parts[1]);
                        if (id < 0)
                        {
                            continue;
                        }

                        int x = GetValue(parts[2]);
                        int y = GetValue(parts[3]);
                        int width = GetValue(parts[4]) + 1;
                        int height = GetValue(parts[5]) + 1;
                        int xOffset = GetValue(parts[6]);
                        int yOffset = GetValue(parts[7]);
                        int xAdvance = GetValue(parts[8]);
                        int channel = GetValue(parts[10]);

                        if (_characterMap.ContainsKey(id))
                        {

                        }

                        _characterMap[id] = new Character((uint)id, (uint)id, new Vector2(width, height), Vector2.Zero, xAdvance, x, y, xOffset, yOffset);
                    }

                    int GetValue(string pair)
                    {
                        return int.Parse(pair.Split("=", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[1]);
                    }
                }
            }

            UpdateContent("");
        }

        public byte[] GetAtlasTextureData()
        {
            var atlasImage = EmbeddedResources.Read("Ryujinx.Ui.Common/Resources/atlas/font_atlas.zip");
            using var memoryStream = new MemoryStream(atlasImage);
            using var archive = new ZipArchive(memoryStream);
            using var output = new MemoryStream();
            archive.Entries.First().Open().CopyTo(output);
            return output.ToArray();
        }

        public Character GetCharacter(char character)
        {
            return _characterMap[character];
        }

        public void UpdateContent(string content)
        {
            var lines = content.Split("\n", StringSplitOptions.RemoveEmptyEntries);

            using var mapStream = new MemoryStream();
            using var writer = new BinaryWriter(mapStream);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int x = 0;
                foreach (var character in line)
                {
                    var glyph = _characterMap[character];
                    if (glyph == null)
                    {
                        continue;
                    }

                    // Element content: horizontal offset, line offset,  Character x and y offset in map,character x and y offset in glyph, Width, Height
                    writer.Write(x);
                    writer.Write(i);
                    writer.Write(glyph.X);
                    writer.Write(glyph.Y);
                    writer.Write(glyph.OffsetX);
                    writer.Write(glyph.OffsetY);
                    writer.Write((int)glyph.Size.X);
                    writer.Write((int)glyph.Size.Y);

                    // bitshift by 6 to get value in pixels (2^6 = 64)
                    x += glyph.Advance;
                }

                Width = Math.Max(Width, x + 10);
            }

            CurrentContentMapData = mapStream.ToArray();
            Length = content.Replace("\n", "").Length ;
            Height = lines.Length + 1;

            ContentUpdated?.Invoke(this, EventArgs.Empty);
        }

        public class Character
        {
            public Character(uint characterCode, uint characterIndex, Vector2 size, Vector2 bearing, int advance, int x, int y, int offsetX, int offsetY)
            {
                CharacterCode = characterCode;
                CharacterIndex = characterIndex;
                Size = size;
                Bearing = bearing;
                Advance = advance;
                OffsetX = offsetX;
                OffsetY = offsetY;
                X = x;
                Y = y;
            }

            public uint CharacterCode { get; }
            public uint CharacterIndex { get; }
            public Vector2 Size { get; }
            public Vector2 Bearing { get; }
            public int Advance { get; }
            public IntPtr Buffer { get; }
            public int OffsetX { get; set; }
            public int OffsetY { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
        }
    }
}
