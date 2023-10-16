using System.Runtime.InteropServices;
using static CUE4Parse.Utils.TypeConversionUtils;

namespace CUE4Parse.UE4.Objects.Core.Math;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public record struct FVector4Half(ushort X, ushort Y, ushort Z, ushort W) : IUStruct {
    public static readonly FVector4Half Zero = new(0, 0, 0, 0);

    public static implicit operator FVector(FVector4Half v) => new(HalfToFloat(v.X), HalfToFloat(v.Y), HalfToFloat(v.Z));
    public static implicit operator FVector4(FVector4Half v) => new(HalfToFloat(v.X), HalfToFloat(v.Y), HalfToFloat(v.Z), HalfToFloat(v.W));
}
