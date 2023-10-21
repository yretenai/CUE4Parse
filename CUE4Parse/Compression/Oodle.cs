using CUE4Parse.UE4.Exceptions;
using CUE4Parse.UE4.Readers;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace CUE4Parse.Compression {
    [Serializable]
    public class OodleException : ParserException {
        public OodleException(FArchive reader, string? message = null, Exception? innerException = null) : base(reader, message, innerException) { }
        public OodleException(string? message, Exception? innerException) : base(message, innerException) { }
        public OodleException(string message) : base(message) { }
        public OodleException() : base("Oodle decompression failed") { }
    }

    public static class Oodle {
        public static IEnumerable<string> OodleLibName {
            get {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    yield return "oo2core*win64.dll";

<<<<<<< HEAD
                    yield break;
                }
=======
        private const string WARFRAME_CONTENT_HOST = "https://content.warframe.com";
        private const string WARFRAME_ORIGIN_HOST = "https://origin.warframe.com";
        private const string WARFRAME_INDEX_PATH = "/origin/50F7040A/index.txt.lzma";
        private const string WARFRAME_INDEX_URL = WARFRAME_ORIGIN_HOST + WARFRAME_INDEX_PATH;
        public const string OODLE_DLL_NAME = "oo2core_9_win64.dll";
>>>>>>> fork/master

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                    if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64) {
                        yield return "oo2core*linuxarm64.so";
                    } else {
                        yield return "oo2core*linux64.so";
                    }

                    yield break;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64) {
                        yield return "oo2core*macarm64.dylib";
                    }

                    yield return "oo2core*mac64.dylib";
                }

                throw new PlatformNotSupportedException();
            }
        }

        public static bool IsReady => DecompressDelegate != null;
        public static OodleLZ_Decompress? DecompressDelegate { get; set; }

        public static bool Load(string? path) {
            if (Directory.Exists(path) && new FileInfo(path).Attributes.HasFlag(FileAttributes.Directory)) {
                foreach (var oodleLibName in OodleLibName) {
                    var files = Directory.GetFiles(path, oodleLibName, SearchOption.TopDirectoryOnly);
                    if (files.Length == 0) {
                        continue;
                    }

                    path = files[0];
                    break;
                }
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
                return false;
            }

            var handle = NativeLibrary.Load(path);
            if (handle == IntPtr.Zero) {
                return false;
            }

            var address = NativeLibrary.GetExport(handle, nameof(OodleLZ_Decompress));
            if (address == IntPtr.Zero) {
                return false;
            }

            DecompressDelegate = Marshal.GetDelegateForFunctionPointer<OodleLZ_Decompress>(address);
            return true;
        }

        public static unsafe void Decompress(Memory<byte> input, int inputOffset, int inputSize,
                                             Memory<byte> output, int outputOffset, int outputSize, FArchive? reader = null) {
            if (DecompressDelegate == null) {
                if (reader != null) throw new OodleException(reader, "Oodle library not loaded");

                throw new OodleException("Oodle library not loaded");
            }

            var inputSlice = input.Slice(inputOffset, inputSize);
            var outputSlice = output.Slice(outputOffset, outputSize);
            using var inPin = inputSlice.Pin();
            using var outPin = outputSlice.Pin();

            var decodedSize = DecompressDelegate(inPin.Pointer, inputSlice.Length, outPin.Pointer, outputSlice.Length);

            if (decodedSize <= 0) {
                if (reader != null) throw new OodleException(reader, $"Oodle decompression failed with result {decodedSize}");

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
        public unsafe delegate int OodleLZ_Decompress(void* srcBuf, int srcSize, void* rawBuf, int rawSize, int fuzzSafe = 1, int checkCRC = 0, OodleLZ_Verbosity verbosity = OodleLZ_Verbosity.None, void* decBufBase = null, int decBufSize = 0, void* fpCallback = null, void* callbackUserData = null, void* decoderMemory = null, int decoderMemorySize = 0, OodleLZ_Decode_ThreadPhase threadPhase = OodleLZ_Decode_ThreadPhase.Unthreaded);
    }
}
