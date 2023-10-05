using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Writers;
using CUE4Parse_Conversion.ActorX;

namespace CUE4Parse_Conversion.Worlds.PSW;

public class WorldActor
{
    public string? AssetPath;
    public WorldActorFlags Flags;
    public string? Name;
    public int Parent;
    public FVector Position;
    public FQuat Rotation;
    public FVector Scale;

    public void Serialize(FArchiveWriter Ar)
    {
        Ar.Write(Name ?? "None", 64);
        Ar.Write(AssetPath ?? "None", 256);
        Ar.Write(Parent);
        Position.Serialize(Ar);
        Rotation.Serialize(Ar);
        Scale.Serialize(Ar);
        Ar.Write((uint) Flags);
    }
}