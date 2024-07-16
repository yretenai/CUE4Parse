using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Readers;
using Serilog;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

namespace CUE4Parse.Compression {
    public class OodleException : ParserException {
        public OodleException(FArchive reader, string? message = null, Exception? innerException = null) : base(reader, message, innerException) { }

        public OodleException(string? message, Exception? innerException) : base(message, innerException) { }

        public OodleException(string message) : base(message) { }

        public OodleException() : base("Oodle decompression failed") { }
    }

    public static class Oodle {
        // this will return a platform-appropriate library name, wildcarded to suppress prefixes, suffixes and version masks
        // - oo2core_9_win32.dll
        // - oo2core_9_win64.dll
        // - oo2core_9_winuwparm64.dll
        // - liboo2coremac64.2.9.10.dylib
        // - liboo2corelinux64.so.9
        // - liboo2corelinuxarm64.so.9
        // - liboo2corelinuxarm32.so.9
        public static IEnumerable<string> OodleLibName {
            get {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                        yield return "*oo2core*winuwparm64*.dll";
                    else if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
                        yield return "*oo2core*win32*.dll";

                    yield return "*oo2core*win64*.dll";

                    yield break;
                }

                // you can find these in the unreal source post-installation
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                    if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                        yield return "*oo2core*linuxarm64*.so*";
                    else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm ||
                             RuntimeInformation.ProcessArchitecture == Architecture.Armv6)
                        yield return "*oo2core*linuxarm32*.so*";

                    yield return "*oo2core*linux64*.so*";

                    yield break;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                        yield return "*oo2core*macarm64*.dylib"; // todo: this doesn't exist.

                    yield return "*oo2core*mac64*.dylib";

                    yield break;
                }

                throw new PlatformNotSupportedException();
            }
        }

        [MemberNotNullWhen(true, nameof(DecompressDelegate))]
        [MemberNotNullWhen(true, nameof(MemorySizeNeededDelegate))]
        public static bool IsReady => DecompressDelegate != null && MemorySizeNeededDelegate != null;

        public static OodleLZ_Decompress? DecompressDelegate { get; set; }
        public static OodleLZDecoder_MemorySizeNeeded? MemorySizeNeededDelegate { get; set; }

        public static bool TryFindOodleDll(string? path, [MaybeNullWhen(false)] out string result) {
            path ??= Environment.CurrentDirectory;
            foreach (var oodleLibName in OodleLibName) {
                var files = Directory.GetFiles(path, oodleLibName, SearchOption.TopDirectoryOnly);
                if (files.Length == 0)
                    continue;

                result = files[0];
                return true;
            }

            result = null;
            return false;
        }

        public static bool LoadOodleDll(string? path = null) {
            if (IsReady)
                return true;

            path ??= Environment.CurrentDirectory;

            if (Directory.Exists(path) && new FileInfo(path).Attributes.HasFlag(FileAttributes.Directory)) {
                if (!TryFindOodleDll(path, out var oodlePath)) {
                    return false;
                }

                path = oodlePath;
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            if (!NativeLibrary.TryLoad(path, out var handle))
                return false;

            if (!NativeLibrary.TryGetExport(handle, nameof(OodleLZDecoder_MemorySizeNeeded), out var address))
                return false;

            MemorySizeNeededDelegate = Marshal.GetDelegateForFunctionPointer<OodleLZDecoder_MemorySizeNeeded>(address);

            if (!NativeLibrary.TryGetExport(handle, nameof(OodleLZ_Decompress), out address))
                return false;

            DecompressDelegate = Marshal.GetDelegateForFunctionPointer<OodleLZ_Decompress>(address);
            return true;
        }

        public static unsafe void Decompress(Memory<byte> input, int inputOffset, int inputSize,
                                             Memory<byte> output, int outputOffset, int outputSize, FArchive? reader = null) {
            if (!IsReady) {
                LoadOodleDll();
                if (!IsReady) {
                    if (reader != null)
                        throw new OodleException(reader, "Oodle library not loaded");

                    throw new OodleException("Oodle library not loaded");
                }
            }

            var inputSlice = input.Slice(inputOffset, inputSize);
            var outputSlice = output.Slice(outputOffset, outputSize);
            using var inPin = inputSlice.Pin();
            using var outPin = outputSlice.Pin();
            var blockDecoderMemorySizeNeeded = MemorySizeNeededDelegate(-1, -1);
            using var pool = MemoryPool<byte>.Shared.Rent(blockDecoderMemorySizeNeeded);
            using var poolPin = pool.Memory.Pin();

            var decodedSize = DecompressDelegate((byte*) inPin.Pointer, inputSlice.Length, (byte*) outPin.Pointer, outputSlice.Length, 1, 0, OodleLZ_Verbosity.Minimal, null, 0, null, null, (byte*) poolPin.Pointer, blockDecoderMemorySizeNeeded, OodleLZ_Decode_ThreadPhase.Unthreaded);

            if (decodedSize <= 0) {
                if (reader != null)
                    throw new OodleException(reader, $"Oodle decompression failed with result {decodedSize}");

                throw new OodleException($"Oodle decompression failed with result {decodedSize}");
            }

            if (decodedSize < outputSize) {
                // Not sure whether this should be an exception or not
                Log.Warning("Oodle decompression just decompressed {0} bytes of the expected {1} bytes", decodedSize, outputSize);
            }
        }

        public enum OodleLZ_Decode_ThreadPhase {
            ThreadPhase1 = 1,
            ThreadPhase2 = 2,
            ThreadPhaseAll = 3,
            Unthreaded = ThreadPhaseAll,
        }

        public enum OodleLZ_Verbosity {
            None = 0,
            Minimal = 1,
            Some = 2,
            Lots = 3,
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate int OodleLZ_Decompress(byte* srcBuf, long srcSize, byte* rawBuf, long rawSize, [MarshalAs(UnmanagedType.I4)] int fuzzSafe, [MarshalAs(UnmanagedType.I4)] int checkCRC, OodleLZ_Verbosity verbosity, byte* decBufBase, long decBufSize, void* fpCallback, void* callbackUserData, byte* decoderMemory, long decoderMemorySize, OodleLZ_Decode_ThreadPhase threadPhase);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int OodleLZDecoder_MemorySizeNeeded(int compressor, long size);
    }
}
