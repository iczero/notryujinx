using Ryujinx.Common.Memory;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.Texture.FileFormats
{
    public static class DdsFileFormat
    {
        [Flags]
        private enum DdsFlags : uint
        {
            Caps = 1,
            Height = 2,
            Width = 4,
            Pitch = 8,
            PixelFormat = 0x1000,
            MipMapCount = 0x20000,
            LinearSize = 0x80000,
            Depth = 0x800000,
        }

        [Flags]
        private enum DdsCaps : uint
        {
            Complex = 8,
            Texture = 0x1000,
            MipMap = 0x400000,
        }

        [Flags]
        private enum DdsCaps2 : uint
        {
            None = 0,
            CubeMap = 0x200,
            CubeMapPositiveX = 0x400,
            CubeMapNegativeX = 0x800,
            CubeMapPositiveY = 0x1000,
            CubeMapNegativeY = 0x2000,
            CubeMapPositiveZ = 0x4000,
            CubeMapNegativeZ = 0x8000,
            Volume = 0x200000,
        }

        [Flags]
        private enum DdsPfFlags : uint
        {
            AlphaPixels = 1,
            Alpha = 2,
            FourCC = 4,
            Rgb = 0x40,
            Rgba = AlphaPixels | Rgb,
            Yuv = 0x200,
            Luminance = 0x20000,
        }

        private struct DdsPixelFormat
        {
            public uint Size;
            public DdsPfFlags Flags;
            public uint FourCC;
            public uint RGBBitCount;
            public uint RBitMask;
            public uint GBitMask;
            public uint BBitMask;
            public uint ABitMask;
        }

        private struct DdsHeader
        {
            public uint Size;
            public DdsFlags Flags;
            public uint Height;
            public uint Width;
            public uint PitchOrLinearSize;
            public uint Depth;
            public uint MipMapCount;
            public Array11<uint> Reserved1;
            public DdsPixelFormat DdsPf;
            public DdsCaps Caps;
            public DdsCaps2 Caps2;
            public uint Caps3;
            public uint Caps4;
            public uint Reserved2;
        }

        private enum D3d10ResourceDimension : uint
        {
            Unknown = 0,
            Buffer = 1,
            Texture1D = 2,
            Texture2D = 3,
            Texture3D = 4,
        }

        private struct DdsHeaderDxt10
        {
            public DxgiFormat DxgiFormat;
            public D3d10ResourceDimension ResourceDimension;
            public uint MiscFlag;
            public uint ArraySize;
            public uint MiscFlags2;
        }

        private const uint DdsMagic = 0x20534444;
        private const uint Dxt1FourCC = 'D' | ('X' << 8) | ('T' << 16) | ('1' << 24);
        private const uint Dxt3FourCC = 'D' | ('X' << 8) | ('T' << 16) | ('3' << 24);
        private const uint Dxt5FourCC = 'D' | ('X' << 8) | ('T' << 16) | ('5' << 24);
        private const uint Dx10FourCC = 'D' | ('X' << 8) | ('1' << 16) | ('0' << 24);

        public static ImageLoadResult TryLoadHeader(ReadOnlySpan<byte> ddsData, out ImageParameters parameters)
        {
            return TryLoadHeaderImpl(ddsData, out parameters, out _);
        }

        private static ImageLoadResult TryLoadHeaderImpl(ReadOnlySpan<byte> ddsData, out ImageParameters parameters, out int dataOffset)
        {
            parameters = default;
            dataOffset = 0;

            if (ddsData.Length < 4 + Unsafe.SizeOf<DdsHeader>())
            {
                return ImageLoadResult.DataTooShort;
            }

            uint magic = ddsData.Read<uint>();
            DdsHeader header = ddsData[4..].Read<DdsHeader>();

            if (magic != DdsMagic ||
                header.Size != Unsafe.SizeOf<DdsHeader>() ||
                header.DdsPf.Size != Unsafe.SizeOf<DdsPixelFormat>())
            {
                return ImageLoadResult.CorruptedHeader;
            }

            int depth = header.Flags.HasFlag(DdsFlags.Depth) ? (int)header.Depth : 1;
            int levels = header.Flags.HasFlag(DdsFlags.MipMapCount) ? (int)header.MipMapCount : 1;
            int layers = 1;
            ImageDimensions dimensions = header.Flags.HasFlag(DdsFlags.Depth) ? ImageDimensions.Dim3D : ImageDimensions.Dim2D;
            ImageFormat format = GetFormat(header.DdsPf);

            if (header.Caps2.HasFlag(DdsCaps2.CubeMap))
            {
                layers = 6;
                dimensions = ImageDimensions.DimCube;
            }

            dataOffset = 4 + Unsafe.SizeOf<DdsHeader>();

            if (header.DdsPf.Flags.HasFlag(DdsPfFlags.FourCC) && header.DdsPf.FourCC == Dx10FourCC)
            {
                if (ddsData.Length < 4 + Unsafe.SizeOf<DdsHeader>() + Unsafe.SizeOf<DdsHeaderDxt10>())
                {
                    return ImageLoadResult.DataTooShort;
                }

                DdsHeaderDxt10 headerDxt10 = ddsData[dataOffset..].Read<DdsHeaderDxt10>();

                if (dimensions != ImageDimensions.Dim3D)
                {
                    if (headerDxt10.MiscFlag == 4u)
                    {
                        // Cube array.
                        layers = (int)Math.Max(1, headerDxt10.ArraySize) * 6;
                        dimensions = headerDxt10.ArraySize > 1 ? ImageDimensions.DimCubeArray : ImageDimensions.DimCube;
                    }
                    else
                    {
                        // 2D array.
                        layers = (int)Math.Max(1, headerDxt10.ArraySize);
                        dimensions = headerDxt10.ArraySize > 1 ? ImageDimensions.Dim2DArray : ImageDimensions.Dim2D;
                    }
                }

                format = ConvertToImageFormat(headerDxt10.DxgiFormat);

                dataOffset += Unsafe.SizeOf<DdsHeaderDxt10>();
            }

            parameters = new((int)header.Width, (int)header.Height, depth * layers, levels, format, dimensions);

            return ImageLoadResult.Success;
        }

        public static int CalculateSize(in ImageParameters parameters)
        {
            if (parameters.Format == ImageFormat.Unknown)
            {
                return 0;
            }

            int size = 0;
            (int bw, int bh, int bpp) = GetBlockSizeAndBpp(parameters.Format);

            for (int l = 0; l < parameters.Levels; l++)
            {
                int w = Math.Max(1, parameters.Width >> l);
                int h = Math.Max(1, parameters.Height >> l);
                int d = parameters.Dimensions == ImageDimensions.Dim3D ? Math.Max(1, parameters.DepthOrLayers >> l) : parameters.DepthOrLayers;

                w = (w + bw - 1) / bw;
                h = (h + bh - 1) / bh;

                size += w * bpp * h * d;
            }

            return size;
        }

        public static ImageLoadResult TryLoadData(ReadOnlySpan<byte> ddsData, Span<byte> output)
        {
            ImageLoadResult result = TryLoadHeaderImpl(ddsData, out ImageParameters parameters, out int dataOffset);

            if (result != ImageLoadResult.Success)
            {
                return result;
            }

            if (parameters.Format == ImageFormat.Unknown)
            {
                return ImageLoadResult.UnsupportedFormat;
            }

            int size = CalculateSize(parameters);

            // Some basic validation for completely bogus sizes.
            if (size <= 0 || dataOffset + size <= 0)
            {
                return ImageLoadResult.CorruptedHeader;
            }

            if (dataOffset + size > ddsData.Length)
            {
                return ImageLoadResult.DataTooShort;
            }

            if (output.Length < size)
            {
                return ImageLoadResult.OutputTooShort;
            }

            if (parameters.DepthOrLayers > 1 && parameters.Dimensions != ImageDimensions.Dim3D)
            {
                int inOffset = dataOffset;

                for (int z = 0; z < parameters.DepthOrLayers; z++)
                {
                    for (int l = 0; l < parameters.Levels; l++)
                    {
                        (int sliceOffset, int sliceSize) = GetSlice(parameters, z, l);
                        ddsData.Slice(inOffset, sliceSize).CopyTo(output.Slice(sliceOffset, sliceSize));
                        inOffset += sliceSize;
                    }
                }
            }
            else
            {
                ddsData.Slice(dataOffset, size).CopyTo(output);
            }

            return ImageLoadResult.Success;
        }

        public static void Save(Stream output, ImageParameters parameters, ReadOnlySpan<byte> data)
        {
            DdsFlags flags = DdsFlags.Caps | DdsFlags.Height | DdsFlags.Width | DdsFlags.PixelFormat;
            DdsCaps caps = DdsCaps.Texture;
            DdsCaps2 caps2 = DdsCaps2.None;

            if (parameters.Levels > 1)
            {
                flags |= DdsFlags.MipMapCount;
                caps |= DdsCaps.MipMap | DdsCaps.Complex;
            }

            if (parameters.Dimensions == ImageDimensions.DimCube)
            {
                caps2 |= DdsCaps2.CubeMap | DdsCaps2.CubeMapPositiveX;

                if (parameters.DepthOrLayers > 1)
                {
                    caps2 |= DdsCaps2.CubeMapNegativeX;
                }

                if (parameters.DepthOrLayers > 2)
                {
                    caps2 |= DdsCaps2.CubeMapPositiveY;
                }

                if (parameters.DepthOrLayers > 3)
                {
                    caps2 |= DdsCaps2.CubeMapNegativeY;
                }

                if (parameters.DepthOrLayers > 4)
                {
                    caps2 |= DdsCaps2.CubeMapPositiveZ;
                }

                if (parameters.DepthOrLayers > 5)
                {
                    caps2 |= DdsCaps2.CubeMapNegativeZ;
                }
            }
            else if (parameters.Dimensions == ImageDimensions.Dim3D)
            {
                flags |= DdsFlags.Depth;
                caps2 |= DdsCaps2.Volume;
            }

            bool isArray = parameters.Dimensions == ImageDimensions.Dim2DArray ||
                           parameters.Dimensions == ImageDimensions.DimCubeArray;
            bool needsDxt10Header = isArray;

            DdsPixelFormat pixelFormat = needsDxt10Header ? CreateDx10PixelFormat() : CreatePixelFormat(parameters.Format);

            (int bw, int bh, int bpp) = GetBlockSizeAndBpp(parameters.Format);

            int pitchOrLinearSize = ((parameters.Width + bw - 1) / bw) * bpp;

            if (bw > 1 || bh > 1)
            {
                flags |= DdsFlags.LinearSize;
                pitchOrLinearSize *= ((parameters.Height + bh - 1) / bh) * parameters.DepthOrLayers;
            }
            else
            {
                flags |= DdsFlags.Pitch;
            }

            DdsHeader header = new()
            {
                Size = (uint)Unsafe.SizeOf<DdsHeader>(),
                Flags = flags,
                Height = (uint)parameters.Height,
                Width = (uint)parameters.Width,
                PitchOrLinearSize = (uint)pitchOrLinearSize,
                Depth = (uint)parameters.DepthOrLayers,
                MipMapCount = (uint)parameters.Levels,
                Reserved1 = default,
                DdsPf = pixelFormat,
                Caps = caps,
                Caps2 = caps2,
                Caps3 = 0,
                Caps4 = 0,
                Reserved2 = 0,
            };

            output.Write(DdsMagic);
            output.Write(header);

            if (needsDxt10Header)
            {
                output.Write(CreateDxt10Header(parameters.Format, parameters.Dimensions, parameters.DepthOrLayers));
            }

            if (parameters.DepthOrLayers > 1 && parameters.Dimensions != ImageDimensions.Dim3D)
            {
                // On DDS, the order is:
                // [Layer 0 Level 0] [Layer 0 Level 1] [Layer 1 Level 0] [Layer 1 Level 1]
                // While on the input data, the order is:
                // [Layer 0 Level 0] [Layer 1 Level 0] [Layer 0 Level 1] [Layer 1 Level 1]

                for (int z = 0; z < parameters.DepthOrLayers; z++)
                {
                    for (int l = 0; l < parameters.Levels; l++)
                    {
                        (int sliceOffset, int sliceSize) = GetSlice(parameters, z, l);
                        output.Write(data.Slice(sliceOffset, sliceSize));
                    }
                }
            }
            else
            {
                output.Write(data);
            }
        }

        private static (int, int) GetSlice(ImageParameters parameters, int layer, int level)
        {
            int size = 0;
            int sliceSize = 0;
            int depth, layers;

            if (parameters.Dimensions == ImageDimensions.Dim3D)
            {
                depth = parameters.DepthOrLayers;
                layers = 1;
            }
            else
            {
                depth = 1;
                layers = parameters.DepthOrLayers;
            }

            (int bw, int bh, int bpp) = GetBlockSizeAndBpp(parameters.Format);

            for (int l = 0; l <= level; l++)
            {
                int w = Math.Max(1, parameters.Width >> l);
                int h = Math.Max(1, parameters.Height >> l);
                int d = Math.Max(1, depth >> l);

                w = (w + bw - 1) / bw;
                h = (h + bh - 1) / bh;

                for (int z = 0; z < (l < level ? layers : layer + 1); z++)
                {
                    sliceSize = w * bpp * h * d;
                    size += sliceSize;
                }
            }

            return (size - sliceSize, sliceSize);
        }

        private static void Write<T>(this Stream stream, T value) where T : unmanaged
        {
            stream.Write(MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref value, 1)));
        }

        private static T Read<T>(this ReadOnlySpan<byte> span) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(span)[0];
        }

        private static DdsHeaderDxt10 CreateDxt10Header(ImageFormat format, ImageDimensions dimensions, int depthOrLayers)
        {
            D3d10ResourceDimension resourceDimension = dimensions == ImageDimensions.Dim3D
                ? D3d10ResourceDimension.Texture3D
                : D3d10ResourceDimension.Texture2D;

            uint arraySize = 1;

            if (dimensions == ImageDimensions.Dim2DArray)
            {
                arraySize = (uint)depthOrLayers;
            }
            else if (dimensions == ImageDimensions.DimCubeArray)
            {
                arraySize = (uint)depthOrLayers / 6;
            }

            return new DdsHeaderDxt10()
            {
                DxgiFormat = ConvertToDxgiFormat(format),
                ResourceDimension = resourceDimension,
                MiscFlag = dimensions == ImageDimensions.DimCube || dimensions == ImageDimensions.DimCubeArray ? 4u : 0u,
                ArraySize = arraySize,
                MiscFlags2 = 1, // Straight alpha.
            };
        }

        private static DdsPixelFormat CreateDx10PixelFormat()
        {
            return new DdsPixelFormat()
            {
                Size = (uint)Unsafe.SizeOf<DdsPixelFormat>(),
                Flags = DdsPfFlags.FourCC,
                FourCC = Dx10FourCC,
            };
        }

        private static DdsPixelFormat CreatePixelFormat(ImageFormat format)
        {
            DdsPixelFormat pf = new()
            {
                Size = (uint)Unsafe.SizeOf<DdsPixelFormat>(),
            };

            switch (format)
            {
                case ImageFormat.Bc1RgbaUnorm:
                    pf.Flags = DdsPfFlags.FourCC;
                    pf.FourCC = Dxt1FourCC;
                    break;
                case ImageFormat.Bc2Unorm:
                    pf.Flags = DdsPfFlags.FourCC;
                    pf.FourCC = Dxt3FourCC;
                    break;
                case ImageFormat.Bc3Unorm:
                    pf.Flags = DdsPfFlags.FourCC;
                    pf.FourCC = Dxt5FourCC;
                    break;
                case ImageFormat.R8G8B8A8Unorm:
                    pf.Flags |= DdsPfFlags.Rgba;
                    pf.RGBBitCount = 32;
                    pf.RBitMask = 0xffu;
                    pf.GBitMask = 0xffu << 8;
                    pf.BBitMask = 0xffu << 16;
                    pf.ABitMask = 0xffu << 24;
                    break;
                case ImageFormat.B8G8R8A8Unorn:
                    pf.Flags |= DdsPfFlags.Rgba;
                    pf.RGBBitCount = 32;
                    pf.RBitMask = 0xffu << 16;
                    pf.GBitMask = 0xffu << 8;
                    pf.BBitMask = 0xffu;
                    pf.ABitMask = 0xffu << 24;
                    break;
                case ImageFormat.R5G6B5Unorm:
                    pf.Flags |= DdsPfFlags.Rgb;
                    pf.RGBBitCount = 16;
                    pf.RBitMask = 0x1fu << 11;
                    pf.GBitMask = 0x3fu << 5;
                    pf.BBitMask = 0x1fu;
                    break;
                case ImageFormat.R5G5B5A1Unorm:
                    pf.Flags |= DdsPfFlags.Rgba;
                    pf.RGBBitCount = 16;
                    pf.RBitMask = 0x1fu << 10;
                    pf.GBitMask = 0x1fu << 5;
                    pf.BBitMask = 0x1fu;
                    pf.ABitMask = 1u << 15;
                    break;
                case ImageFormat.R4G4B4A4Unorm:
                    pf.Flags |= DdsPfFlags.Rgba;
                    pf.RGBBitCount = 16;
                    pf.RBitMask = 0xfu << 8;
                    pf.GBitMask = 0xfu << 4;
                    pf.BBitMask = 0xfu;
                    pf.ABitMask = 0xfu << 12;
                    break;
            }

            return pf;
        }

        private static (int, int, int) GetBlockSizeAndBpp(ImageFormat format)
        {
            int bw = 1;
            int bh = 1;
            int bpp = 0;

            switch (format)
            {
                case ImageFormat.Bc1RgbaUnorm:
                    bw = bh = 4;
                    bpp = 8;
                    break;
                case ImageFormat.Bc2Unorm:
                    bw = bh = 4;
                    bpp = 16;
                    break;
                case ImageFormat.Bc3Unorm:
                    bw = bh = 4;
                    bpp = 16;
                    break;
                case ImageFormat.R8G8B8A8Unorm:
                case ImageFormat.B8G8R8A8Unorn:
                    bpp = 4;
                    break;
                case ImageFormat.R5G6B5Unorm:
                    bpp = 2;
                    break;
                case ImageFormat.R5G5B5A1Unorm:
                    bpp = 2;
                    break;
                case ImageFormat.R4G4B4A4Unorm:
                    bpp = 2;
                    break;
            }

            if (bpp == 0)
            {
                throw new ArgumentException($"Invalid format {format}.");
            }

            return (bw, bh, bpp);
        }

        private static DxgiFormat ConvertToDxgiFormat(ImageFormat format)
        {
            return format switch
            {
                ImageFormat.Bc1RgbaUnorm => DxgiFormat.FormatBC1Unorm,
                ImageFormat.Bc2Unorm => DxgiFormat.FormatBC2Unorm,
                ImageFormat.Bc3Unorm => DxgiFormat.FormatBC3Unorm,
                ImageFormat.R8G8B8A8Unorm => DxgiFormat.FormatR8G8B8A8Unorm,
                ImageFormat.B8G8R8A8Unorn => DxgiFormat.FormatB8G8R8A8Unorm,
                ImageFormat.R5G6B5Unorm => DxgiFormat.FormatB5G6R5Unorm,
                ImageFormat.R5G5B5A1Unorm => DxgiFormat.FormatB5G5R5A1Unorm,
                ImageFormat.R4G4B4A4Unorm => DxgiFormat.FormatB4G4R4A4Unorm,
                _ => DxgiFormat.FormatUnknown,
            };
        }

        private static ImageFormat GetFormat(DdsPixelFormat pixelFormat)
        {
            if (pixelFormat.Flags.HasFlag(DdsPfFlags.FourCC))
            {
                return pixelFormat.FourCC switch
                {
                    Dxt1FourCC => ImageFormat.Bc1RgbaUnorm,
                    Dxt3FourCC => ImageFormat.Bc2Unorm,
                    Dxt5FourCC => ImageFormat.Bc3Unorm,
                    _ => ImageFormat.Unknown,
                };
            }
            else
            {
                if ((pixelFormat.Flags & DdsPfFlags.Rgba) == DdsPfFlags.Rgba &&
                    pixelFormat.RGBBitCount == 32 &&
                    pixelFormat.RBitMask == 0xffu &&
                    pixelFormat.GBitMask == 0xffu << 8 &&
                    pixelFormat.BBitMask == 0xffu << 16 &&
                    pixelFormat.ABitMask == 0xffu << 24)
                {
                    return ImageFormat.R8G8B8A8Unorm;
                }
                else if ((pixelFormat.Flags & DdsPfFlags.Rgba) == DdsPfFlags.Rgba &&
                    pixelFormat.RGBBitCount == 32 &&
                    pixelFormat.RBitMask == 0xffu << 16 &&
                    pixelFormat.GBitMask == 0xffu << 8 &&
                    pixelFormat.BBitMask == 0xffu &&
                    pixelFormat.ABitMask == 0xffu << 24)
                {
                    return ImageFormat.B8G8R8A8Unorn;
                }
                else if ((pixelFormat.Flags & DdsPfFlags.Rgba) == DdsPfFlags.Rgb &&
                    pixelFormat.RGBBitCount == 16 &&
                    pixelFormat.RBitMask == 0x1fu << 11 &&
                    pixelFormat.GBitMask == 0x3fu << 5 &&
                    pixelFormat.BBitMask == 0x1fu)
                {
                    return ImageFormat.R5G6B5Unorm;
                }
                else if ((pixelFormat.Flags & DdsPfFlags.Rgba) == DdsPfFlags.Rgba &&
                    pixelFormat.RGBBitCount == 16 &&
                    pixelFormat.RBitMask == 0x1fu << 10 &&
                    pixelFormat.GBitMask == 0x1fu << 5 &&
                    pixelFormat.BBitMask == 0x1fu &&
                    pixelFormat.ABitMask == 1u << 15)
                {
                    return ImageFormat.R5G5B5A1Unorm;
                }
                else if ((pixelFormat.Flags & DdsPfFlags.Rgba) == DdsPfFlags.Rgba &&
                    pixelFormat.RGBBitCount == 16 &&
                    pixelFormat.RBitMask == 0xfu << 8 &&
                    pixelFormat.GBitMask == 0xfu << 4 &&
                    pixelFormat.BBitMask == 0xfu &&
                    pixelFormat.ABitMask == 0xfu << 12)
                {
                    return ImageFormat.R4G4B4A4Unorm;
                }
            }

            return ImageFormat.Unknown;
        }

        private static ImageFormat ConvertToImageFormat(DxgiFormat format)
        {
            return format switch
            {
                DxgiFormat.FormatBC1Unorm => ImageFormat.Bc1RgbaUnorm,
                DxgiFormat.FormatBC2Unorm => ImageFormat.Bc2Unorm,
                DxgiFormat.FormatBC3Unorm => ImageFormat.Bc3Unorm,
                DxgiFormat.FormatR8G8B8A8Unorm => ImageFormat.R8G8B8A8Unorm,
                DxgiFormat.FormatB5G6R5Unorm => ImageFormat.R5G6B5Unorm,
                DxgiFormat.FormatB5G5R5A1Unorm => ImageFormat.R5G5B5A1Unorm,
                DxgiFormat.FormatB4G4R4A4Unorm => ImageFormat.R4G4B4A4Unorm,
                _ => ImageFormat.Unknown,
            };
        }
    }
}
