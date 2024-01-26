using System.Runtime.InteropServices;
using CUE4Parse.UE4.Readers;

namespace CUE4Parse.UE4.Objects.Core.Math;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FBox2f : IUStruct {
    /// <summary>
    /// Holds the box's minimum point.
    /// </summary>
    public FVector Min;

    /// <summary>
    /// Holds the box's maximum point.
    /// </summary>
    public FVector Max;

    /// <summary>
    /// Holds a flag indicating whether this box is valid.
    /// </summary>
    public byte IsValid; // It's a bool

    public FBox2f(FArchive Ar) {
        Min = Ar.Read<FVector>();
        Max = Ar.Read<FVector>();
        IsValid = Ar.Read<byte>();
    }
}
