using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.UObject;

namespace CUE4Parse.UE4.Wwise.Exports;

[StructFallback]
public class WwiseGroupValueCookedData : IWwiseDebugName {
    public WwiseGroupValueCookedData() { }

    public WwiseGroupValueCookedData(FStructFallback fallback) {
        Type = fallback.GetOrDefault<EWwiseGroupType>(nameof(Type));
        GroupId = fallback.GetOrDefault<int>(nameof(GroupId));
        ID = fallback.GetOrDefault<int>(nameof(ID));
        DebugName = fallback.GetOrDefault<FName>(nameof(DebugName));
    }

    public EWwiseGroupType Type { get; set; }
    public int GroupId { get; set; }
    public int ID { get; set; }
    public FName DebugName { get; set; }
}
