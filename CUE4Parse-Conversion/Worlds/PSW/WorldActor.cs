﻿using System.Collections.Generic;
using CUE4Parse_Conversion.ActorX;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Writers;

namespace CUE4Parse_Conversion.Worlds.PSW;

public class WorldActor {
    public string? AssetPath;
    public WorldActorFlags Flags;
    public string? Name;
    public int Parent;
    public FVector Position;
    public FQuat Rotation;
    public FVector Scale;
    public List<(string, string)> Materials { get; set; } = [];

    public void Serialize(FArchiveWriter Ar, (int Start, int Count) materialIndex) {
        Ar.Write(Name ?? "None", 256);
        Ar.Write(AssetPath ?? "None", 256);
        Ar.Write(Parent);
        Position.Serialize(Ar);
        Rotation.Serialize(Ar);
        Scale.Serialize(Ar);
        Ar.Write((uint) Flags);
        Ar.Write(materialIndex.Start);
        Ar.Write(materialIndex.Count);
    }
}
