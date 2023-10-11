using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.UObject;

namespace CUE4Parse.UE4.Wwise.Exports;

[StructFallback]
public class WwiseExternalSourceCookedData
{
    public int Cookie { get; set; }
    public FName DebugName { get; set; }

    public WwiseExternalSourceCookedData()
    {
    }

    public WwiseExternalSourceCookedData(FStructFallback fallback)
    {
        Cookie = fallback.GetOrDefault<int>(nameof(Cookie));
        DebugName = fallback.GetOrDefault<FName>(nameof(DebugName));
    }
}