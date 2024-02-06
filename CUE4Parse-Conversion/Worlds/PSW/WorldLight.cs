using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Writers;

namespace CUE4Parse_Conversion.Worlds.PSW;

public enum WorldLightType {
    Directional,
    Point,
    Spot,
    Rect
}

public class WorldLight {
    public int Parent;
    public FColor Color;
    public WorldLightType Type;
    public float Width;
    public float Height;
    public float Length;
    public float AttenuationRadius;
    public float Radius;
    public float Temperature;
    public float ShadowBias;
    public float Intensity;
    public float LightSourceAngle;

    public void Serialize(FArchiveWriter Ar) {
        Ar.Write(Parent);
        Color.Serialize(Ar);
        Ar.Write((int) Type);
        Ar.Write(Width);
        Ar.Write(Height);
        Ar.Write(Length);
        Ar.Write(AttenuationRadius);
        Ar.Write(Radius);
        Ar.Write(Temperature);
        Ar.Write(ShadowBias);
        Ar.Write(Intensity);
        Ar.Write(LightSourceAngle);
    }
}