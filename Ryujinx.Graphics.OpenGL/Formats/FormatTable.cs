using OpenTK.Graphics.OpenGL;
using Ryujinx.Graphics.GAL;
using System;

namespace Ryujinx.Graphics.OpenGL.Formats
{
    struct FormatTable
    {
        private static FormatInfo[] _table;

        static FormatTable()
        {
            _table = new FormatInfo[Enum.GetNames(typeof(Format)).Length];

            Add(Format.R8Unorm,             new FormatInfo(1, true,  false, All.R8,                PixelFormat.Red,            PixelType.UnsignedByte));
            Add(Format.R8Snorm,             new FormatInfo(1, true,  false, All.R8Snorm,           PixelFormat.Red,            PixelType.Byte));
            Add(Format.R8Uint,              new FormatInfo(1, false, false, All.R8ui,              PixelFormat.RedInteger,     PixelType.UnsignedByte));
            Add(Format.R8Sint,              new FormatInfo(1, false, false, All.R8i,               PixelFormat.RedInteger,     PixelType.Byte));
            Add(Format.R16Float,            new FormatInfo(1, false, false, All.R16f,              PixelFormat.Red,            PixelType.HalfFloat));
            Add(Format.R16Unorm,            new FormatInfo(1, true,  false, All.R16,               PixelFormat.Red,            PixelType.UnsignedShort));
            Add(Format.R16Snorm,            new FormatInfo(1, true,  false, All.R16Snorm,          PixelFormat.Red,            PixelType.Short));
            Add(Format.R16Uint,             new FormatInfo(1, false, false, All.R16ui,             PixelFormat.RedInteger,     PixelType.UnsignedShort));
            Add(Format.R16Sint,             new FormatInfo(1, false, false, All.R16i,              PixelFormat.RedInteger,     PixelType.Short));
            Add(Format.R32Float,            new FormatInfo(1, false, false, All.R32f,              PixelFormat.Red,            PixelType.Float));
            Add(Format.R32Uint,             new FormatInfo(1, false, false, All.R32ui,             PixelFormat.RedInteger,     PixelType.UnsignedInt));
            Add(Format.R32Sint,             new FormatInfo(1, false, false, All.R32i,              PixelFormat.RedInteger,     PixelType.Int));
            Add(Format.R8G8Unorm,           new FormatInfo(2, true,  false, All.Rg8,               PixelFormat.Rg,             PixelType.UnsignedByte));
            Add(Format.R8G8Snorm,           new FormatInfo(2, true,  false, All.Rg8Snorm,          PixelFormat.Rg,             PixelType.Byte));
            Add(Format.R8G8Uint,            new FormatInfo(2, false, false, All.Rg8ui,             PixelFormat.RgInteger,      PixelType.UnsignedByte));
            Add(Format.R8G8Sint,            new FormatInfo(2, false, false, All.Rg8i,              PixelFormat.RgInteger,      PixelType.Byte));
            Add(Format.R16G16Float,         new FormatInfo(2, false, false, All.Rg16f,             PixelFormat.Rg,             PixelType.HalfFloat));
            Add(Format.R16G16Unorm,         new FormatInfo(2, true,  false, All.Rg16,              PixelFormat.Rg,             PixelType.UnsignedShort));
            Add(Format.R16G16Snorm,         new FormatInfo(2, true,  false, All.Rg16Snorm,         PixelFormat.Rg,             PixelType.Short));
            Add(Format.R16G16Uint,          new FormatInfo(2, false, false, All.Rg16ui,            PixelFormat.RgInteger,      PixelType.UnsignedShort));
            Add(Format.R16G16Sint,          new FormatInfo(2, false, false, All.Rg16i,             PixelFormat.RgInteger,      PixelType.Short));
            Add(Format.R32G32Float,         new FormatInfo(2, false, false, All.Rg32f,             PixelFormat.Rg,             PixelType.Float));
            Add(Format.R32G32Uint,          new FormatInfo(2, false, false, All.Rg32ui,            PixelFormat.RgInteger,      PixelType.UnsignedInt));
            Add(Format.R32G32Sint,          new FormatInfo(2, false, false, All.Rg32i,             PixelFormat.RgInteger,      PixelType.Int));
            Add(Format.R8G8B8Unorm,         new FormatInfo(3, true,  false, All.Rgb8,              PixelFormat.Rgb,            PixelType.UnsignedByte));
            Add(Format.R8G8B8Snorm,         new FormatInfo(3, true,  false, All.Rgb8Snorm,         PixelFormat.Rgb,            PixelType.Byte));
            Add(Format.R8G8B8Uint,          new FormatInfo(3, false, false, All.Rgb8ui,            PixelFormat.RgbInteger,     PixelType.UnsignedByte));
            Add(Format.R8G8B8Sint,          new FormatInfo(3, false, false, All.Rgb8i,             PixelFormat.RgbInteger,     PixelType.Byte));
            Add(Format.R16G16B16Float,      new FormatInfo(3, false, false, All.Rgb16f,            PixelFormat.Rgb,            PixelType.HalfFloat));
            Add(Format.R16G16B16Unorm,      new FormatInfo(3, true,  false, All.Rgb16,             PixelFormat.Rgb,            PixelType.UnsignedShort));
            Add(Format.R16G16B16Snorm,      new FormatInfo(3, true,  false, All.Rgb16Snorm,        PixelFormat.Rgb,            PixelType.Short));
            Add(Format.R16G16B16Uint,       new FormatInfo(3, false, false, All.Rgb16ui,           PixelFormat.RgbInteger,     PixelType.UnsignedShort));
            Add(Format.R16G16B16Sint,       new FormatInfo(3, false, false, All.Rgb16i,            PixelFormat.RgbInteger,     PixelType.Short));
            Add(Format.R32G32B32Float,      new FormatInfo(3, false, false, All.Rgb32f,            PixelFormat.Rgb,            PixelType.Float));
            Add(Format.R32G32B32Uint,       new FormatInfo(3, false, false, All.Rgb32ui,           PixelFormat.RgbInteger,     PixelType.UnsignedInt));
            Add(Format.R32G32B32Sint,       new FormatInfo(3, false, false, All.Rgb32i,            PixelFormat.RgbInteger,     PixelType.Int));
            Add(Format.R8G8B8A8Unorm,       new FormatInfo(4, true,  false, All.Rgba8,             PixelFormat.Rgba,           PixelType.UnsignedByte));
            Add(Format.R8G8B8A8Snorm,       new FormatInfo(4, true,  false, All.Rgba8Snorm,        PixelFormat.Rgba,           PixelType.Byte));
            Add(Format.R8G8B8A8Uint,        new FormatInfo(4, false, false, All.Rgba8ui,           PixelFormat.RgbaInteger,    PixelType.UnsignedByte));
            Add(Format.R8G8B8A8Sint,        new FormatInfo(4, false, false, All.Rgba8i,            PixelFormat.RgbaInteger,    PixelType.Byte));
            Add(Format.R16G16B16A16Float,   new FormatInfo(4, false, false, All.Rgba16f,           PixelFormat.Rgba,           PixelType.HalfFloat));
            Add(Format.R16G16B16A16Unorm,   new FormatInfo(4, true,  false, All.Rgba16,            PixelFormat.Rgba,           PixelType.UnsignedShort));
            Add(Format.R16G16B16A16Snorm,   new FormatInfo(4, true,  false, All.Rgba16Snorm,       PixelFormat.Rgba,           PixelType.Short));
            Add(Format.R16G16B16A16Uint,    new FormatInfo(4, false, false, All.Rgba16ui,          PixelFormat.RgbaInteger,    PixelType.UnsignedShort));
            Add(Format.R16G16B16A16Sint,    new FormatInfo(4, false, false, All.Rgba16i,           PixelFormat.RgbaInteger,    PixelType.Short));
            Add(Format.R32G32B32A32Float,   new FormatInfo(4, false, false, All.Rgba32f,           PixelFormat.Rgba,           PixelType.Float));
            Add(Format.R32G32B32A32Uint,    new FormatInfo(4, false, false, All.Rgba32ui,          PixelFormat.RgbaInteger,    PixelType.UnsignedInt));
            Add(Format.R32G32B32A32Sint,    new FormatInfo(4, false, false, All.Rgba32i,           PixelFormat.RgbaInteger,    PixelType.Int));
            Add(Format.S8Uint,              new FormatInfo(1, false, false, All.StencilIndex8,     PixelFormat.StencilIndex,   PixelType.UnsignedByte));
            Add(Format.D16Unorm,            new FormatInfo(1, false, false, All.DepthComponent16,  PixelFormat.DepthComponent, PixelType.UnsignedShort));
            Add(Format.D24X8Unorm,          new FormatInfo(1, false, false, All.DepthComponent24,  PixelFormat.DepthComponent, PixelType.UnsignedInt));
            Add(Format.D32Float,            new FormatInfo(1, false, false, All.DepthComponent32f, PixelFormat.DepthComponent, PixelType.Float));
            Add(Format.D24UnormS8Uint,      new FormatInfo(1, false, false, All.Depth24Stencil8,   PixelFormat.DepthStencil,   PixelType.UnsignedInt248));
            Add(Format.D32FloatS8Uint,      new FormatInfo(1, false, false, All.Depth32fStencil8,  PixelFormat.DepthStencil,   PixelType.Float32UnsignedInt248Rev));
            Add(Format.R8G8B8X8Srgb,        new FormatInfo(4, false, false, All.Srgb8,             PixelFormat.Rgba,           PixelType.UnsignedByte));
            Add(Format.R8G8B8A8Srgb,        new FormatInfo(4, false, false, All.Srgb8Alpha8,       PixelFormat.Rgba,           PixelType.UnsignedByte));
            Add(Format.R4G4B4A4Unorm,       new FormatInfo(4, true,  false, All.Rgba4,             PixelFormat.Rgba,           PixelType.UnsignedShort4444Reversed));
            Add(Format.R5G5B5X1Unorm,       new FormatInfo(4, true,  false, All.Rgb5,              PixelFormat.Rgb,            PixelType.UnsignedShort1555Reversed));
            Add(Format.R5G5B5A1Unorm,       new FormatInfo(4, true,  false, All.Rgb5A1,            PixelFormat.Rgba,           PixelType.UnsignedShort1555Reversed));
            Add(Format.R5G6B5Unorm,         new FormatInfo(3, true,  false, All.Rgb565,            PixelFormat.Rgb,            PixelType.UnsignedShort565Reversed));
            Add(Format.R10G10B10A2Unorm,    new FormatInfo(4, true,  false, All.Rgb10A2,           PixelFormat.Rgba,           PixelType.UnsignedInt2101010Reversed));
            Add(Format.R10G10B10A2Uint,     new FormatInfo(4, false, false, All.Rgb10A2ui,         PixelFormat.RgbaInteger,    PixelType.UnsignedInt2101010Reversed));
            Add(Format.R11G11B10Float,      new FormatInfo(3, false, false, All.R11fG11fB10f,      PixelFormat.Rgb,            PixelType.UnsignedInt10F11F11FRev));
            Add(Format.R9G9B9E5Float,       new FormatInfo(3, false, false, All.Rgb9E5,            PixelFormat.Rgb,            PixelType.UnsignedInt5999Rev));
            Add(Format.Bc1RgbUnorm,         new FormatInfo(2, true,  false, All.CompressedRgbS3tcDxt1Ext));
            Add(Format.Bc1RgbaUnorm,        new FormatInfo(1, true,  false, All.CompressedRgbaS3tcDxt1Ext));
            Add(Format.Bc2Unorm,            new FormatInfo(1, true,  false, All.CompressedRgbaS3tcDxt3Ext));
            Add(Format.Bc3Unorm,            new FormatInfo(1, true,  false, All.CompressedRgbaS3tcDxt5Ext));
            Add(Format.Bc1RgbSrgb,          new FormatInfo(2, false, false, All.CompressedSrgbS3tcDxt1Ext));
            Add(Format.Bc1RgbaSrgb,         new FormatInfo(1, true,  false, All.CompressedSrgbAlphaS3tcDxt1Ext));
            Add(Format.Bc2Srgb,             new FormatInfo(1, false, false, All.CompressedSrgbAlphaS3tcDxt3Ext));
            Add(Format.Bc3Srgb,             new FormatInfo(1, false, false, All.CompressedSrgbAlphaS3tcDxt5Ext));
            Add(Format.Bc4Unorm,            new FormatInfo(1, true,  false, All.CompressedRedRgtc1));
            Add(Format.Bc4Snorm,            new FormatInfo(1, true,  false, All.CompressedSignedRedRgtc1));
            Add(Format.Bc5Unorm,            new FormatInfo(1, true,  false, All.CompressedRgRgtc2));
            Add(Format.Bc5Snorm,            new FormatInfo(1, true,  false, All.CompressedSignedRgRgtc2));
            Add(Format.Bc7Unorm,            new FormatInfo(1, true,  false, All.CompressedRgbaBptcUnorm));
            Add(Format.Bc7Srgb,             new FormatInfo(1, false, false, All.CompressedSrgbAlphaBptcUnorm));
            Add(Format.Bc6HUfloat,          new FormatInfo(1, false, false, All.CompressedRgbBptcUnsignedFloat));
            Add(Format.Bc6HSfloat,          new FormatInfo(1, false, false, All.CompressedRgbBptcSignedFloat));
            Add(Format.R8Uscaled,           new FormatInfo(1, false, true,  All.R8ui,              PixelFormat.RedInteger,     PixelType.UnsignedByte));
            Add(Format.R8Sscaled,           new FormatInfo(1, false, true,  All.R8i,               PixelFormat.RedInteger,     PixelType.Byte));
            Add(Format.R16Uscaled,          new FormatInfo(1, false, true,  All.R16ui,             PixelFormat.RedInteger,     PixelType.UnsignedShort));
            Add(Format.R16Sscaled,          new FormatInfo(1, false, true,  All.R16i,              PixelFormat.RedInteger,     PixelType.Short));
            Add(Format.R32Uscaled,          new FormatInfo(1, false, true,  All.R32ui,             PixelFormat.RedInteger,     PixelType.UnsignedInt));
            Add(Format.R32Sscaled,          new FormatInfo(1, false, true,  All.R32i,              PixelFormat.RedInteger,     PixelType.Int));
            Add(Format.R8G8Uscaled,         new FormatInfo(2, false, true,  All.Rg8ui,             PixelFormat.RgInteger,      PixelType.UnsignedByte));
            Add(Format.R8G8Sscaled,         new FormatInfo(2, false, true,  All.Rg8i,              PixelFormat.RgInteger,      PixelType.Byte));
            Add(Format.R16G16Uscaled,       new FormatInfo(2, false, true,  All.Rg16ui,            PixelFormat.RgInteger,      PixelType.UnsignedShort));
            Add(Format.R16G16Sscaled,       new FormatInfo(2, false, true,  All.Rg16i,             PixelFormat.RgInteger,      PixelType.Short));
            Add(Format.R32G32Uscaled,       new FormatInfo(2, false, true,  All.Rg32ui,            PixelFormat.RgInteger,      PixelType.UnsignedInt));
            Add(Format.R32G32Sscaled,       new FormatInfo(2, false, true,  All.Rg32i,             PixelFormat.RgInteger,      PixelType.Int));
            Add(Format.R8G8B8Uscaled,       new FormatInfo(3, false, true,  All.Rgb8ui,            PixelFormat.RgbInteger,     PixelType.UnsignedByte));
            Add(Format.R8G8B8Sscaled,       new FormatInfo(3, false, true,  All.Rgb8i,             PixelFormat.RgbInteger,     PixelType.Byte));
            Add(Format.R16G16B16Uscaled,    new FormatInfo(3, false, true,  All.Rgb16ui,           PixelFormat.RgbInteger,     PixelType.UnsignedShort));
            Add(Format.R16G16B16Sscaled,    new FormatInfo(3, false, true,  All.Rgb16i,            PixelFormat.RgbInteger,     PixelType.Short));
            Add(Format.R32G32B32Uscaled,    new FormatInfo(3, false, true,  All.Rgb32ui,           PixelFormat.RgbInteger,     PixelType.UnsignedInt));
            Add(Format.R32G32B32Sscaled,    new FormatInfo(3, false, true,  All.Rgb32i,            PixelFormat.RgbInteger,     PixelType.Int));
            Add(Format.R8G8B8A8Uscaled,     new FormatInfo(4, false, true,  All.Rgba8ui,           PixelFormat.RgbaInteger,    PixelType.UnsignedByte));
            Add(Format.R8G8B8A8Sscaled,     new FormatInfo(4, false, true,  All.Rgba8i,            PixelFormat.RgbaInteger,    PixelType.Byte));
            Add(Format.R16G16B16A16Uscaled, new FormatInfo(4, false, true,  All.Rgba16ui,          PixelFormat.RgbaInteger,    PixelType.UnsignedShort));
            Add(Format.R16G16B16A16Sscaled, new FormatInfo(4, false, true,  All.Rgba16i,           PixelFormat.RgbaInteger,    PixelType.Short));
            Add(Format.R32G32B32A32Uscaled, new FormatInfo(4, false, true,  All.Rgba32ui,          PixelFormat.RgbaInteger,    PixelType.UnsignedInt));
            Add(Format.R32G32B32A32Sscaled, new FormatInfo(4, false, true,  All.Rgba32i,           PixelFormat.RgbaInteger,    PixelType.Int));
            Add(Format.R10G10B10A2Snorm,    new FormatInfo(4, true,  false, All.Rgb10A2,           PixelFormat.Rgba,           (PixelType)All.Int2101010Rev));
            Add(Format.R10G10B10A2Sint,     new FormatInfo(4, false, false, All.Rgb10A2,           PixelFormat.RgbaInteger,    (PixelType)All.Int2101010Rev));
            Add(Format.R10G10B10A2Uscaled,  new FormatInfo(4, false, true,  All.Rgb10A2ui,         PixelFormat.RgbaInteger,    PixelType.UnsignedInt2101010Reversed));
            Add(Format.R10G10B10A2Sscaled,  new FormatInfo(4, false, true,  All.Rgb10A2,           PixelFormat.RgbaInteger,    PixelType.UnsignedInt2101010Reversed));
            Add(Format.R8G8B8X8Unorm,       new FormatInfo(4, true,  false, All.Rgb8,              PixelFormat.Rgba,           PixelType.UnsignedByte));
            Add(Format.R8G8B8X8Snorm,       new FormatInfo(4, true,  false, All.Rgb8Snorm,         PixelFormat.Rgba,           PixelType.Byte));
            Add(Format.R8G8B8X8Uint,        new FormatInfo(4, false, false, All.Rgb8ui,            PixelFormat.RgbaInteger,    PixelType.UnsignedByte));
            Add(Format.R8G8B8X8Sint,        new FormatInfo(4, false, false, All.Rgb8i,             PixelFormat.RgbaInteger,    PixelType.Byte));
            Add(Format.R16G16B16X16Float,   new FormatInfo(4, false, false, All.Rgb16f,            PixelFormat.Rgba,           PixelType.HalfFloat));
            Add(Format.R16G16B16X16Unorm,   new FormatInfo(4, true,  false, All.Rgb16,             PixelFormat.Rgba,           PixelType.UnsignedShort));
            Add(Format.R16G16B16X16Snorm,   new FormatInfo(4, true,  false, All.Rgb16Snorm,        PixelFormat.Rgba,           PixelType.Short));
            Add(Format.R16G16B16X16Uint,    new FormatInfo(4, false, false, All.Rgb16ui,           PixelFormat.RgbaInteger,    PixelType.UnsignedShort));
            Add(Format.R16G16B16X16Sint,    new FormatInfo(4, false, false, All.Rgb16i,            PixelFormat.RgbaInteger,    PixelType.Short));
            Add(Format.R32G32B32X32Float,   new FormatInfo(4, false, false, All.Rgb32f,            PixelFormat.Rgba,           PixelType.Float));
            Add(Format.R32G32B32X32Uint,    new FormatInfo(4, false, false, All.Rgb32ui,           PixelFormat.RgbaInteger,    PixelType.UnsignedInt));
            Add(Format.R32G32B32X32Sint,    new FormatInfo(4, false, false, All.Rgb32i,            PixelFormat.RgbaInteger,    PixelType.Int));
            Add(Format.Astc4x4Unorm,        new FormatInfo(1, true,  false, All.CompressedRgbaAstc4X4Khr));
            Add(Format.Astc5x4Unorm,        new FormatInfo(1, true,  false, All.CompressedRgbaAstc5X4Khr));
            Add(Format.Astc5x5Unorm,        new FormatInfo(1, true,  false, All.CompressedRgbaAstc5X5Khr));
            Add(Format.Astc6x5Unorm,        new FormatInfo(1, true,  false, All.CompressedRgbaAstc6X5Khr));
            Add(Format.Astc6x6Unorm,        new FormatInfo(1, true,  false, All.CompressedRgbaAstc6X6Khr));
            Add(Format.Astc8x5Unorm,        new FormatInfo(1, true,  false, All.CompressedRgbaAstc8X5Khr));
            Add(Format.Astc8x6Unorm,        new FormatInfo(1, true,  false, All.CompressedRgbaAstc8X6Khr));
            Add(Format.Astc8x8Unorm,        new FormatInfo(1, true,  false, All.CompressedRgbaAstc8X8Khr));
            Add(Format.Astc10x5Unorm,       new FormatInfo(1, true,  false, All.CompressedRgbaAstc10X5Khr));
            Add(Format.Astc10x6Unorm,       new FormatInfo(1, true,  false, All.CompressedRgbaAstc10X6Khr));
            Add(Format.Astc10x8Unorm,       new FormatInfo(1, true,  false, All.CompressedRgbaAstc10X8Khr));
            Add(Format.Astc10x10Unorm,      new FormatInfo(1, true,  false, All.CompressedRgbaAstc10X10Khr));
            Add(Format.Astc12x10Unorm,      new FormatInfo(1, true,  false, All.CompressedRgbaAstc12X10Khr));
            Add(Format.Astc12x12Unorm,      new FormatInfo(1, true,  false, All.CompressedRgbaAstc12X12Khr));
            Add(Format.Astc4x4Srgb,         new FormatInfo(1, false, false, All.CompressedSrgb8Alpha8Astc4X4Khr));
            Add(Format.Astc5x4Srgb,         new FormatInfo(1, false, false, All.CompressedSrgb8Alpha8Astc5X4Khr));
            Add(Format.Astc5x5Srgb,         new FormatInfo(1, false, false, All.CompressedSrgb8Alpha8Astc5X5Khr));
            Add(Format.Astc6x5Srgb,         new FormatInfo(1, false, false, All.CompressedSrgb8Alpha8Astc6X5Khr));
            Add(Format.Astc6x6Srgb,         new FormatInfo(1, false, false, All.CompressedSrgb8Alpha8Astc6X6Khr));
            Add(Format.Astc8x5Srgb,         new FormatInfo(1, false, false, All.CompressedSrgb8Alpha8Astc8X5Khr));
            Add(Format.Astc8x6Srgb,         new FormatInfo(1, false, false, All.CompressedSrgb8Alpha8Astc8X6Khr));
            Add(Format.Astc8x8Srgb,         new FormatInfo(1, false, false, All.CompressedSrgb8Alpha8Astc8X8Khr));
            Add(Format.Astc10x5Srgb,        new FormatInfo(1, false, false, All.CompressedSrgb8Alpha8Astc10X5Khr));
            Add(Format.Astc10x6Srgb,        new FormatInfo(1, false, false, All.CompressedSrgb8Alpha8Astc10X6Khr));
            Add(Format.Astc10x8Srgb,        new FormatInfo(1, false, false, All.CompressedSrgb8Alpha8Astc10X8Khr));
            Add(Format.Astc10x10Srgb,       new FormatInfo(1, false, false, All.CompressedSrgb8Alpha8Astc10X10Khr));
            Add(Format.Astc12x10Srgb,       new FormatInfo(1, false, false, All.CompressedSrgb8Alpha8Astc12X10Khr));
            Add(Format.Astc12x12Srgb,       new FormatInfo(1, false, false, All.CompressedSrgb8Alpha8Astc12X12Khr));
            Add(Format.B5G6R5Unorm,         new FormatInfo(3, true,  false, All.Rgb565,            PixelFormat.Bgr,            PixelType.UnsignedShort565));
            Add(Format.B5G5R5X1Unorm,       new FormatInfo(4, true,  false, All.Rgb5,              PixelFormat.Bgra,           PixelType.UnsignedShort5551));
            Add(Format.B5G5R5A1Unorm,       new FormatInfo(4, true,  false, All.Rgb5A1,            PixelFormat.Bgra,           PixelType.UnsignedShort5551));
            Add(Format.A1B5G5R5Unorm,       new FormatInfo(4, true,  false, All.Rgb5A1,            PixelFormat.Bgra,           PixelType.UnsignedShort1555Reversed));
            Add(Format.B8G8R8X8Unorm,       new FormatInfo(4, true,  false, All.Rgba8,             PixelFormat.Bgra,           PixelType.UnsignedByte));
            Add(Format.B8G8R8A8Unorm,       new FormatInfo(4, true,  false, All.Rgba8,             PixelFormat.Bgra,           PixelType.UnsignedByte));
            Add(Format.B8G8R8X8Srgb,        new FormatInfo(4, false, false, All.Srgb8,             PixelFormat.BgraInteger,    PixelType.UnsignedByte));
            Add(Format.B8G8R8A8Srgb,        new FormatInfo(4, false, false, All.Srgb8Alpha8,       PixelFormat.BgraInteger,    PixelType.UnsignedByte));
        }

        private static void Add(Format format, FormatInfo info)
        {
            _table[(int)format] = info;
        }

        public static FormatInfo GetFormatInfo(Format format)
        {
            return _table[(int)format];
        }
    }
}
