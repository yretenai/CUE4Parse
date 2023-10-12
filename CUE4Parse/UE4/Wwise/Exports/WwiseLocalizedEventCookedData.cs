using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.UObject;

namespace CUE4Parse.UE4.Wwise.Exports;

[StructFallback]
public class WwiseLocalizedEventCookedData : UObject, IWwiseDebugName {
    public WwiseLocalizedEventCookedData() { }

    public WwiseLocalizedEventCookedData(FStructFallback fallback) {
        LoadFromProperties(fallback);
    }

    public Dictionary<WwiseLanguageCookedData, WwiseEventCookedData> EventLanguageMap { get; init; } = new();
    public int EventId { get; set; }
    public FName DebugName { get; set; }

    public override void Deserialize(FAssetArchive Ar, long validPos) {
        base.Deserialize(Ar, validPos);
        LoadFromProperties(this);
    }

    private void LoadFromProperties(IPropertyHolder holder) {
        var eventLanguageMap = PropertyUtil.GetOrDefault<UScriptMap>(holder, nameof(EventLanguageMap));
        foreach (var (key, value) in eventLanguageMap.Properties) {
            if (value == null) {
                continue;
            }

            EventLanguageMap[key.GetValue<WwiseLanguageCookedData>()!] = value.GetValue<WwiseEventCookedData>()!;
        }

        DebugName = PropertyUtil.GetOrDefault(holder, nameof(DebugName), DebugName);
        EventId = PropertyUtil.GetOrDefault(holder, nameof(EventId), EventId);
    }
}
