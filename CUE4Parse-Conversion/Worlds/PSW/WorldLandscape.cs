using System;
using CUE4Parse_Conversion.ActorX;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Writers;

namespace CUE4Parse_Conversion.Worlds.PSW;

public class WorldLandscape {
    public int ActorId;
    public FVector4 Scale;
    public string? Path;
    public int ComponentSize;
    public int TileX;
    public int TileY;
    public int DimX;
    public int DimY;
    public int Type; // 0 = height, 1+ weightmaps.

    public float[] Heightmap = Array.Empty<float>();
    public int OrigX;
    public int OrigY;

    public void Serialize(FArchiveWriter Ar) // 256 + 4 + 4+ 4
    {
        Ar.Write(Path ?? "None", 256);
        Ar.Write(ActorId);
        Ar.Write(TileX);
        Ar.Write(TileY);
        Ar.Write(Type);
        Ar.Write(ComponentSize);
        Ar.Write(256 / (ComponentSize + 1));
        Ar.Write(Scale.Z);
        Ar.Write(Scale.W);
        Ar.Write(DimX);
        Ar.Write(DimY);
    }
}
