using Avalonia.Media;
using DynamicData;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Ava.Ui.Models;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.FileSystem.Content;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Color = Avalonia.Media.Color;

namespace Ryujinx.Ava.Ui.ViewModels
{
    public class AvatarProfileViewModel : BaseModel
    {
        private static readonly Dictionary<string, byte[]> AvatarDict = new();
        private static readonly ManualResetEvent PreloadResetEvent = new(false);

        private ObservableCollection<ProfileImageModel> _images;

        private int _selectedIndex;
        private Color _backgroundColor = Colors.White;
        private bool _isActive;
        private int _imagesLoaded;
        private byte[] _selectedImage;

        public bool IsActive
        {
            get => _isActive;
            set => _isActive = value;
        }

        public AvatarProfileViewModel()
        {
            _images = new ObservableCollection<ProfileImageModel>();

            PreloadResetEvent.WaitOne();

            IsActive = true;

            ReloadImages();
        }

        public Color BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                _backgroundColor = value;

                IsActive = false;
                
                ReloadImages();
            }
        }

        public ObservableCollection<ProfileImageModel> Images
        {
            get => _images;
            set
            {
                _images = value;
                OnPropertyChanged();
            }
        }

        public int ImageCount => AvatarDict.Count;

        public int ImagesLoaded
        {
            get => _imagesLoaded;
            set
            {
                _imagesLoaded = value;
                OnPropertyChanged();
            }
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                _selectedIndex = value;

                if (_selectedIndex == -1)
                {
                    SelectedImage = null;
                }
                else
                {
                    SelectedImage = _images[_selectedIndex].Data;
                }

                OnPropertyChanged();
            }
        }

        public byte[] SelectedImage
        {
            get => _selectedImage;
            private set => _selectedImage = value;
        }

        public void ReloadImages()
        {
            Task.Run(() =>
            {
                lock (this)
                {
                    IsActive = true;
                    
                    Images.Clear();

                    int index = 0;
                    int selectedIndex = _selectedIndex;

                    var newImages = new List<ProfileImageModel>();

                    ImagesLoaded = 0;

                    foreach (var image in AvatarDict)
                    {
                        if (!IsActive)
                        {
                            return;
                        }

                        var data = ProcessImage(image.Value);
                        newImages.Add(new ProfileImageModel(image.Key, data));
                        if (index++ == selectedIndex)
                        {
                            SelectedImage = data;
                        }

                        ImagesLoaded++;
                    }

                    ImagesLoaded = 0;
                    Images.AddRange(newImages);
                }
            });
        }

        private byte[] ProcessImage(byte[] data)
        {
            using (MemoryStream streamJpg = new())
            {
                Image avatarImage = Image.Load(data, new PngDecoder());

                avatarImage.Mutate(x => x.BackgroundColor(new Rgba32(BackgroundColor.R,
                    BackgroundColor.G,
                    BackgroundColor.B,
                    BackgroundColor.A)));
                avatarImage.SaveAsJpeg(streamJpg);

                return streamJpg.ToArray();
            }
        }

        public static void PreloadAvatars(ContentManager contentManager, VirtualFileSystem virtualFileSystem)
        {
            if (AvatarDict.Count > 0)
            {
                PreloadResetEvent.Set();
                return;
            }

            string contentPath =
                contentManager.GetInstalledContentPath(0x010000000000080A, StorageId.NandSystem, NcaContentType.Data);
            string avatarPath = virtualFileSystem.SwitchPathToSystemPath(contentPath);

            if (!string.IsNullOrWhiteSpace(avatarPath))
            {
                using (IStorage ncaFileStream = new LocalStorage(avatarPath, FileAccess.Read, FileMode.Open))
                {
                    Nca nca = new(virtualFileSystem.KeySet, ncaFileStream);
                    IFileSystem romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

                    foreach (DirectoryEntryEx item in romfs.EnumerateEntries())
                    {
                        // TODO: Parse DatabaseInfo.bin and table.bin files for more accuracy.

                        if (item.Type == DirectoryEntryType.File && item.FullPath.Contains("chara") &&
                            item.FullPath.Contains("szs"))
                        {
                            using var file = new UniqueRef<IFile>();

                            romfs.OpenFile(ref file.Ref(), ("/" + item.FullPath).ToU8Span(), OpenMode.Read).ThrowIfFailure();

                            using (MemoryStream stream = new())
                            using (MemoryStream streamPng = new())
                            {
                                file.Get.AsStream().CopyTo(stream);

                                stream.Position = 0;

                                Image avatarImage = Image.LoadPixelData<Rgba32>(DecompressYaz0(stream), 256, 256);

                                avatarImage.SaveAsPng(streamPng);

                                AvatarDict.Add(item.FullPath, streamPng.ToArray());
                            }
                        }
                    }
                }
            }

            PreloadResetEvent.Set();
        }

        private static byte[] DecompressYaz0(Stream stream)
        {
            using (BinaryReader reader = new(stream))
            {
                reader.ReadInt32(); // Magic

                uint decodedLength = BinaryPrimitives.ReverseEndianness(reader.ReadUInt32());

                reader.ReadInt64(); // Padding

                byte[] input = new byte[stream.Length - stream.Position];
                stream.Read(input, 0, input.Length);

                uint inputOffset = 0;

                byte[] output = new byte[decodedLength];
                uint outputOffset = 0;

                ushort mask = 0;
                byte header = 0;

                while (outputOffset < decodedLength)
                {
                    if ((mask >>= 1) == 0)
                    {
                        header = input[inputOffset++];
                        mask = 0x80;
                    }

                    if ((header & mask) != 0)
                    {
                        if (outputOffset == output.Length)
                        {
                            break;
                        }

                        output[outputOffset++] = input[inputOffset++];
                    }
                    else
                    {
                        byte byte1 = input[inputOffset++];
                        byte byte2 = input[inputOffset++];

                        uint dist = (uint)((byte1 & 0xF) << 8) | byte2;
                        uint position = outputOffset - (dist + 1);

                        uint length = (uint)byte1 >> 4;
                        if (length == 0)
                        {
                            length = (uint)input[inputOffset++] + 0x12;
                        }
                        else
                        {
                            length += 2;
                        }

                        uint gap = outputOffset - position;
                        uint nonOverlappingLength = length;

                        if (nonOverlappingLength > gap)
                        {
                            nonOverlappingLength = gap;
                        }

                        System.Buffer.BlockCopy(output, (int)position, output, (int)outputOffset, (int)nonOverlappingLength);
                        outputOffset += nonOverlappingLength;
                        position += nonOverlappingLength;
                        length -= nonOverlappingLength;

                        while (length-- > 0)
                        {
                            output[outputOffset++] = output[position++];
                        }
                    }
                }

                return output;
            }
        }
    }
}