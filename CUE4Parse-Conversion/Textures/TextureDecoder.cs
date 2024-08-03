using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AssetRipper.TextureDecoder.Astc;
using AssetRipper.TextureDecoder.Bc;
using AssetRipper.TextureDecoder.Etc;
using AssetRipper.TextureDecoder.Rgb;
using AssetRipper.TextureDecoder.Rgb.Formats;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.Compression;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.Utils;
using SkiaSharp;

namespace CUE4Parse_Conversion.Textures;

public static class TextureDecoder {
    private static readonly MemoryPool<byte> _shared = MemoryPool<byte>.Shared;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKBitmap? Decode(this UTexture2D texture, int maxMipSize, ETexturePlatform platform = ETexturePlatform.DesktopMobile) => texture.Decode(texture.GetMipByMaxSize(maxMipSize), platform);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKBitmap? Decode(this UTexture2D texture, ETexturePlatform platform = ETexturePlatform.DesktopMobile) => texture.Decode(texture.GetFirstMip(), platform);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKBitmap? Decode(this UTexture texture, ETexturePlatform platform = ETexturePlatform.DesktopMobile) => texture.Decode(texture.GetFirstMip(), platform);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe SKBitmap? Decode(this UTexture texture, FTexture2DMipMap? mip, ETexturePlatform platform = ETexturePlatform.DesktopMobile, int zLayer = 0) {
        if (texture.PlatformData is { FirstMipToSerialize: >= 0, VTData: { } vt } && vt.IsInitialized()) {
            var tileSize = (int) vt.TileSize;
            var tileBorderSize = (int) vt.TileBorderSize;
            var tilePixelSize = (int) vt.GetPhysicalTileSize();
            var level = texture.PlatformData.FirstMipToSerialize;

            FVirtualTextureTileOffsetData tileOffsetData;
            if (vt.IsLegacyData()) {
                // calculate the max address in this mip
                // aka get the next mip max address and subtract it by the current mip max address
                var blockWidthInTiles = vt.GetWidthInTiles();
                var blockHeightInTiles = vt.GetHeightInTiles();
                var maxAddress = vt.TileIndexPerMip[Math.Min(level + 1, vt.NumMips)];
                tileOffsetData = new FVirtualTextureTileOffsetData(blockWidthInTiles, blockHeightInTiles, Math.Max(maxAddress - vt.TileIndexPerMip[level], 1));
            } else {
                tileOffsetData = vt.TileOffsetData[level];
            }

            var bitmapWidth = (int) tileOffsetData.Width * tileSize;
            var bitmapHeight = (int) tileOffsetData.Height * tileSize;
            var maxLevel = Math.Ceiling(Math.Log2(Math.Max(tileOffsetData.Width, tileOffsetData.Height)));
            if (maxLevel == 0 || vt.IsLegacyData()) {
                // if we are here that means the mip is tiled and so the bitmap size must be lowered by one-fourth
                // if texture is legacy we must always lower the bitmap size because GetXXXXInTiles gives the number of tiles in mip 0
                // but that doesn't mean the mip is tiled in the first place
                var baseLevel = vt.IsLegacyData() ? maxLevel : Math.Ceiling(Math.Log2(Math.Max(vt.TileOffsetData[0].Width, vt.TileOffsetData[0].Height)));
                var factor = Convert.ToInt32(Math.Max(Math.Pow(2, vt.IsLegacyData() ? level : level - baseLevel), 1));
                bitmapWidth /= factor;
                bitmapHeight /= factor;
            }

            var tempInfo = new SKImageInfo(bitmapWidth, bitmapHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            var bytesPerPixel = tempInfo.BytesPerPixel;
            var rowBytes = tempInfo.RowBytes;
            var tileRowBytes = tileSize * bytesPerPixel;
            var imageBytes = tempInfo.BytesSize;
            var pixelDataPtr = NativeMemory.Alloc((nuint) imageBytes);
            var result = new Span<byte>(pixelDataPtr, imageBytes);
            // TODO: what if > 1 fallback?
            if (vt.LayerFallbackColors.Length > 0) {
                var fallback = vt.LayerFallbackColors[0].ToFColor(false);
                for (var pix = 0; pix < result.Length; pix += 4) {
                    result[pix] = fallback.B;
                    result[pix + 1] = fallback.G;
                    result[pix + 2] = fallback.R;
                    result[pix + 3] = fallback.A;
                }
            }

            for (uint layer = 0; layer < vt.NumLayers; layer++) {
                var layerFormat = vt.LayerTypes[layer];
                if (PixelFormatUtils.PixelFormats.ElementAtOrDefault((int) layerFormat) is not { Supported: true } formatInfo || formatInfo.BlockBytes == 0)
                    throw new NotImplementedException($"The supplied pixel format {layerFormat} is not supported!");

                var tileWidthInBlocks = tilePixelSize.DivideAndRoundUp(formatInfo.BlockSizeX);
                var tileHeightInBlocks = tilePixelSize.DivideAndRoundUp(formatInfo.BlockSizeY);
                var packedStride = tileWidthInBlocks * formatInfo.BlockBytes;
                var packedOutputSize = packedStride * tileHeightInBlocks;

                var layerData = MemoryPool<byte>.Shared.Rent(packedOutputSize);

                for (uint tileIndexInMip = 0; tileIndexInMip < tileOffsetData.MaxAddress; tileIndexInMip++) {
                    if (!vt.IsValidAddress(level, tileIndexInMip)) continue;

                    var tileX = (int) MathUtils.ReverseMortonCode2(tileIndexInMip) * tileSize;
                    var tileY = (int) MathUtils.ReverseMortonCode2(tileIndexInMip >> 1) * tileSize;
                    var (chunkIndex, tileStart, tileLength) = vt.GetTileData(level, tileIndexInMip, layer);

                    if (vt.Chunks[chunkIndex].CodecType[layer] == EVirtualTextureCodec.ZippedGPU_DEPRECATED) {
                        Compression.Decompress(vt.Chunks[chunkIndex].BulkData.Data!, (int) tileStart, (int) tileLength,
                                               layerData.Memory, 0, packedOutputSize, CompressionMethod.Zlib);
                    } else {
                        vt.Chunks[chunkIndex].BulkData.Data.AsSpan((int) tileStart, packedOutputSize).CopyTo(layerData.Memory.Span);
                    }

                    using var data = DecodeBytes(layerData.Memory.Span, tilePixelSize, tilePixelSize, formatInfo);

                    for (var i = 0; i < tileSize; i++) {
                        var tileOffset = ((i + tileBorderSize) * tilePixelSize + tileBorderSize) * bytesPerPixel;
                        var offset = tileX * bytesPerPixel + (tileY + i) * rowBytes;
                        var srcSpan = data.Memory.Span.Slice(tileOffset, tileRowBytes);
                        var destSpan = result[offset..];
                        srcSpan.CopyTo(destSpan);
                    }
                }
            }

            if (texture.IsNormalMap) {
                ReconstructNormalZ(result, bitmapWidth, bitmapHeight);
            }

            var bitmap = new SKBitmap();
            var imageInfo = new SKImageInfo(bitmapWidth, bitmapHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            bitmap.InstallPixels(imageInfo, (nint) pixelDataPtr, rowBytes, (bmpPixelAddr, _) => NativeMemory.Free(bmpPixelAddr.ToPointer()));
            return bitmap;
        }

        if (mip != null) {
            var sizeX = mip.SizeX;
            var sizeY = mip.SizeY;
            var sizeZ = mip.SizeZ;

            if (texture.Format == EPixelFormat.PF_BC7) {
                sizeX = (sizeX + 3) / 4 * 4;
                sizeY = (sizeY + 3) / 4 * 4;
                sizeZ = (sizeZ + 3) / 4 * 4;
            }

            using var data = DecodeTexture(mip, sizeX, sizeY, sizeZ, texture.Format, texture.IsNormalMap, platform);

            return InstallPixels(GetImageDataRange(data.Memory, sizeX, sizeY, zLayer), new SKImageInfo(sizeX, sizeY, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void AddImageLayer(uint tileX, int tileSize, uint tileY, int bitmapWidth, IMemoryOwner<byte> pixels, IMemoryOwner<byte> data) {
        var (xOffset, yOffset) = (tileX * tileSize, tileY * tileSize);
        for (var x = 0; x < tileSize; ++x) {
            for (var y = 0; y < tileSize; ++y) {
                var tilePixelIndex = (x + y * tileSize) * 4;
                var pixelIndex = (yOffset + y) * bitmapWidth + xOffset + x;
                var pixelStride = (int) (pixelIndex * 4);

                pixels.Memory.Span[pixelStride] += data.Memory.Span[tilePixelIndex];
                pixels.Memory.Span[pixelStride + 1] += data.Memory.Span[tilePixelIndex + 1];
                pixels.Memory.Span[pixelStride + 2] += data.Memory.Span[tilePixelIndex + 2];
                pixels.Memory.Span[pixelStride + 3] += data.Memory.Span[tilePixelIndex + 3];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static SKBitmap[]? DecodeTextureArray(this UTexture2DArray texture, ETexturePlatform platform = ETexturePlatform.DesktopMobile) {
        var mip = texture.GetFirstMip();

        if (mip is null) return null;

        var sizeX = mip.SizeX;
        var sizeY = mip.SizeY;
        var sizeZ = mip.SizeZ;

        if (texture.Format == EPixelFormat.PF_BC7) {
            sizeX = (sizeX + 3) / 4 * 4;
            sizeY = (sizeY + 3) / 4 * 4;
            sizeZ = (sizeZ + 3) / 4 * 4;
        }

        using var data = DecodeTexture(mip, sizeX, sizeY, sizeZ, texture.Format, texture.IsNormalMap, platform);
        var bitmaps = new List<SKBitmap>();
        var offset = sizeX * sizeY * 4;
        for (var i = 0; i < sizeZ; i++) {
            if (offset * (i + 1) > data.Memory.Length) break;

            bitmaps.Add(InstallPixels(GetImageDataRange(data.Memory, sizeX, sizeY, i), new SKImageInfo(sizeX, sizeY, SKColorType.Bgra8888, SKAlphaType.Unpremul)));
        }

        return bitmaps.ToArray();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Memory<byte> GetImageDataRange(Memory<byte> data, int sizeX, int sizeY, int zLayer) {
        var offset = sizeX * sizeY * 4;
        var startIndex = offset * zLayer;
        var endIndex = startIndex + offset;

        return endIndex > data.Length ? data : data[startIndex..endIndex];
    }

    public static IMemoryOwner<byte> DecodeTexture(FTexture2DMipMap? mip, int sizeX, int sizeY, int sizeZ, EPixelFormat format, bool isNormalMap, ETexturePlatform platform) {
        if (mip?.BulkData.Data is not { Length: > 0 }) throw new ParserException("Supplied MipMap is null or has empty data!");
        if (PixelFormatUtils.PixelFormats.ElementAtOrDefault((int) format) is not { Supported: true } formatInfo || formatInfo.BlockBytes == 0) throw new NotImplementedException($"The supplied pixel format {format} is not supported!");

        var isXBPS = platform == ETexturePlatform.XboxAndPlaystation;
        var isNX = platform == ETexturePlatform.NintendoSwitch;

        // If the platform requires deswizzling, check if we should even try.
        if (isXBPS || isNX) {
            var blockSizeX = mip.SizeX / formatInfo.BlockSizeX;
            var blockSizeY = mip.SizeY / formatInfo.BlockSizeY;
            var totalBlocks = mip.BulkData.Data.Length / formatInfo.BlockBytes;
            if (blockSizeX * blockSizeY > totalBlocks) throw new ParserException("The supplied MipMap could not be untiled!");
        }

        var bytes = mip.BulkData.Data;

        // Handle deswizzling if necessary.
        if (isXBPS) bytes = PlatformDeswizzlers.DeswizzleXBPS(bytes, mip, formatInfo);
        else if (isNX) bytes = PlatformDeswizzlers.GetDeswizzledData(bytes, mip, formatInfo);

        var decodedBytes = DecodeBytes(bytes, sizeX, sizeY, formatInfo);
        if (isNormalMap) {
            ReconstructNormalZ(decodedBytes.Memory.Span, sizeX, sizeY);
        }

        return decodedBytes;
    }

    // https://developer.download.nvidia.com/whitepapers/2008/real-time-normal-map-dxt-compression.pdf
    // 5. Real-Time Compression on the GPU
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ReconstructNormalZ(Span<byte> buffer, int sizeX, int sizeY) {
        for (var x = 0; x < sizeX; x++) {
            for (var y = 0; y < sizeY; y++) {
                var o = (x + y * sizeX) * 4;
                var nx = buffer[o + 2] / (double) byte.MaxValue; // bg[r]a
                var ny = buffer[o + 1] / (double) byte.MaxValue; // b[g]ra
                var nx2 = 2 * nx - 1;
                var ny2 = 2 * ny - 1;
                var nz = Math.Sqrt(1 - nx2 * nx2 - ny2 * ny2);

                buffer[o] = (byte) (nz * byte.MaxValue); // [b]gra
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static IMemoryOwner<byte> DecodeBytes(Span<byte> bytes, int sizeX, int sizeY, FPixelFormatInfo formatInfo) {
        var rented = _shared.Rent(sizeX * sizeY * 4);
        switch (formatInfo.UnrealFormat) {
            case EPixelFormat.PF_DXT1: {
                Bc1.Decompress(bytes, sizeX, sizeY, rented.Memory.Span);
                break;
            }
            case EPixelFormat.PF_DXT5:
                Bc3.Decompress(bytes, sizeX, sizeY, rented.Memory.Span);
                break;
            case EPixelFormat.PF_BC4:
                Bc4.Decompress(bytes, sizeX, sizeY, rented.Memory.Span);
                break;
            case EPixelFormat.PF_BC5:
                Bc5.Decompress(bytes, sizeX, sizeY, rented.Memory.Span);
                break;
            case EPixelFormat.PF_BC6H:
                Bc6h.Decompress(bytes, sizeX, sizeY, true, rented.Memory.Span);
                break;
            case EPixelFormat.PF_BC7:
                Bc7.Decompress(bytes, sizeX, sizeY, rented.Memory.Span);
                break;
            case EPixelFormat.PF_ASTC_4x4:
            case EPixelFormat.PF_ASTC_6x6:
            case EPixelFormat.PF_ASTC_8x8:
            case EPixelFormat.PF_ASTC_10x10:
            case EPixelFormat.PF_ASTC_12x12:
                AstcDecoder.DecodeASTC(bytes, sizeX, sizeY, formatInfo.BlockSizeX, formatInfo.BlockSizeY, rented.Memory.Span);
                break;
            case EPixelFormat.PF_ETC1:
                EtcDecoder.DecompressETC(bytes, sizeX, sizeY, rented.Memory.Span);
                break;
            case EPixelFormat.PF_ETC2_RGB:
                EtcDecoder.DecompressETC2(bytes, sizeX, sizeY, rented.Memory.Span);
                break;
            case EPixelFormat.PF_ETC2_RGBA:
                EtcDecoder.DecompressETC2A8(bytes, sizeX, sizeY, rented.Memory.Span);
                break;
            case EPixelFormat.PF_R16F:
            case EPixelFormat.PF_R16F_FILTER:
            case EPixelFormat.PF_G16:
                RgbConverter.Convert<ColorR<ushort>, ushort, ColorBGRA32, byte>(bytes, sizeX, sizeY, rented.Memory.Span);
                break;
            case EPixelFormat.PF_B8G8R8A8:
                bytes[..(sizeX * sizeY * 4)].CopyTo(rented.Memory.Span);
                break;
            case EPixelFormat.PF_R8:
            case EPixelFormat.PF_A8:
            case EPixelFormat.PF_G8:
                RgbConverter.Convert<ColorR<byte>, byte, ColorBGRA32, byte>(bytes, sizeX, sizeY, rented.Memory.Span);
                break;
            case EPixelFormat.PF_FloatRGB:
                RgbConverter.Convert<ColorRGB<Half>, Half, ColorBGRA32, byte>(bytes, sizeX, sizeY, rented.Memory.Span);
                break;
            case EPixelFormat.PF_FloatRGBA:
                RgbConverter.Convert<ColorRGBA<Half>, Half, ColorBGRA32, byte>(bytes, sizeX, sizeY, rented.Memory.Span);
                break;
            default: throw new NotImplementedException($"Unknown pixel format: {formatInfo.UnrealFormat}");
        }

        return rented;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static SKBitmap InstallPixels(Memory<byte> data, SKImageInfo info) {
        var bitmap = new SKBitmap();
        unsafe {
            var pixelsPtr = NativeMemory.Alloc((nuint) data.Length);
            using var pin = data.Pin();
            Unsafe.CopyBlockUnaligned(pixelsPtr, pin.Pointer, (uint) data.Length);
            bitmap.InstallPixels(info, new IntPtr(pixelsPtr), info.RowBytes, (address, _) => NativeMemory.Free(address.ToPointer()));
        }

        return bitmap;
    }
}
