using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Readers;
using IronCompress;

namespace CUE4Parse.Compression {
    public static class Compression {
        public const int LOADING_COMPRESSION_CHUNK_SIZE = 131072;
        private static Iron Iron { get; } = new();

        public static byte[] Decompress(byte[] compressed, int uncompressedSize, CompressionMethod method, FArchive? reader = null) =>
            Decompress(compressed, 0, compressed.Length, uncompressedSize, method, reader);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Decompress(byte[] compressed, int compressedOffset, int compressedCount, int uncompressedSize, CompressionMethod method, FArchive? reader = null) {
            var uncompressed = new byte[uncompressedSize];
            Decompress(compressed, compressedOffset, compressedCount, uncompressed, 0, uncompressedSize, method);
            return uncompressed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Decompress(byte[] compressed, byte[] dst, CompressionMethod method, FArchive? reader = null) =>
            Decompress(compressed, 0, compressed.Length, dst, 0, dst.Length, method, reader);

        public static void Decompress(Memory<byte> compressed, int compressedOffset, int compressedSize, Memory<byte> uncompressed, int uncompressedOffset, int uncompressedSize, CompressionMethod method, FArchive? reader = null) {
            switch (method) {
                case CompressionMethod.None:
                    compressed.Span.Slice(compressedOffset, compressedSize).CopyTo(uncompressed.Span[uncompressedOffset..]);
                    return;
                case CompressionMethod.Zlib: {
                    unsafe {
                        using var pin = compressed.Pin();
                        using var unmanaged = new UnmanagedMemoryStream((byte*) pin.Pointer, compressed.Length);
                        using var zlib = new ZLibStream(unmanaged, CompressionMode.Decompress);
                        zlib.ReadExactly(uncompressed.Span.Slice(uncompressedOffset, uncompressedSize));
                        return;
                    }
                }
                case CompressionMethod.Gzip: {
                    using var uncompressedBuffer = Iron.Decompress(Codec.Gzip, compressed.Span, uncompressedSize);
                    uncompressedBuffer.AsSpan().CopyTo(uncompressed.Span.Slice(uncompressedSize, uncompressedSize));
                    break;
                }
                case CompressionMethod.Oodle:
                    Oodle.Decompress(compressed, compressedOffset, compressedSize, uncompressed, uncompressedOffset, uncompressedSize, reader);
                    return;
                case CompressionMethod.LZ4: {
                    using var uncompressedBuffer = Iron.Decompress(Codec.LZ4, compressed.Span, uncompressedSize);
                    uncompressedBuffer.AsSpan().CopyTo(uncompressed.Span.Slice(uncompressedSize, uncompressedSize));
                    break;
                }
                case CompressionMethod.Zstd: {
                    using var uncompressedBuffer = Iron.Decompress(Codec.Zstd, compressed.Span, uncompressedSize);
                    uncompressedBuffer.AsSpan().CopyTo(uncompressed.Span.Slice(uncompressedSize, uncompressedSize));
                    break;
                }
                default:
                    if (reader != null) throw new UnknownCompressionMethodException(reader, $"Compression method \"{method}\" is unknown");
                    else throw new UnknownCompressionMethodException($"Compression method \"{method}\" is unknown");
            }
        }
    }
}
