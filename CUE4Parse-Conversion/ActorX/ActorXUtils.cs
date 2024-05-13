using System;
using System.Runtime.CompilerServices;
using System.Text;
using CUE4Parse.UE4.Writers;

namespace CUE4Parse_Conversion.ActorX
{
    public static class ActorXUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeChunkHeader(this FArchiveWriter Ar, VChunkHeader header, string name)
        {
            header.ChunkId = name;
            header.Serialize(Ar);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(this FArchiveWriter Ar, string value, int len)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > len) {
                Ar.Write(bytes.AsSpan(0, len));
                return;
            }

            var padded = new byte[len];
            Buffer.BlockCopy(bytes, 0, padded, 0, bytes.Length);
            Ar.Write(padded);
        }
    }
}
