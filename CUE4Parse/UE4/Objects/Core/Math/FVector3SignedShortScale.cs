using System.Runtime.InteropServices;

namespace CUE4Parse.UE4.Objects.Core.Math;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct FVector3SignedShortScale(short X, short Y, short Z, short W) : IUStruct {
    public static implicit operator FVector(FVector3SignedShortScale v) {
        // W having the value of short.MaxValue makes me believe I should use it (somehow) instead of a hardcoded constant
        var wf = v.W == 0 ? 1f : v.W;
        return new FVector(v.X / wf, v.Y / wf, v.Z / wf);
    }
}
