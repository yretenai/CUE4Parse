using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.UObject;

namespace CUE4Parse.UE4.Wwise.Exports;

[StructFallback]
public class WwiseEventCookedData
{
    public int EventId { get; set; }
    public List<WwiseSoundBankCookedData> SoundBanks { get; init; } = new();
    public List<WwiseMediaCookedData> Media { get; init; } = new();
    public List<WwiseExternalSourceCookedData> ExternalSources { get; init; } = new();
    public List<WwiseSwitchContainerLeafCookedData> SwitchContainerLeaves { get; init; } = new();
    public List<WwiseGroupValueCookedData> RequiredGroupValueSet { get; init; } = new();
    public EWwiseEventDestroyOptions DestroyOptions { get; set; }
    public FName DebugName { get; set; }

    public WwiseEventCookedData()
    {
    }

    public WwiseEventCookedData(FStructFallback fallback)
    {
        EventId = fallback.GetOrDefault<int>(nameof(EventId));
        SoundBanks = fallback.GetOrDefault(nameof(SoundBanks), SoundBanks);
        Media = fallback.GetOrDefault(nameof(Media), Media);
        ExternalSources = fallback.GetOrDefault(nameof(ExternalSources), ExternalSources);
        SwitchContainerLeaves = fallback.GetOrDefault(nameof(SwitchContainerLeaves), SwitchContainerLeaves);
        RequiredGroupValueSet = fallback.GetOrDefault(nameof(RequiredGroupValueSet), RequiredGroupValueSet);
        DestroyOptions = fallback.GetOrDefault<EWwiseEventDestroyOptions>(nameof(DestroyOptions));
        DebugName = fallback.GetOrDefault<FName>(nameof(DebugName));
    }
}