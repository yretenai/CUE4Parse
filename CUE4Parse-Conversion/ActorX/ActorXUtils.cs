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
            if (len == 0)
            {
                return;
            }
            
            Span<byte> padded = stackalloc byte[len];
            if (!string.IsNullOrEmpty(value))
            {
                var bytes = Encoding.UTF8.GetBytes(value)[..Math.Min(value.Length, len)].AsSpan();
                bytes.CopyTo(padded);
            }

            Ar.Write(padded);
        }
    }
}