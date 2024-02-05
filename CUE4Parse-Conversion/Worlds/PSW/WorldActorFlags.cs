using System;

namespace CUE4Parse_Conversion.Worlds.PSW;

[Flags]
public enum WorldActorFlags : uint {
    NoCastShadow = 0b1,
    Hidden = 0b10,
}
