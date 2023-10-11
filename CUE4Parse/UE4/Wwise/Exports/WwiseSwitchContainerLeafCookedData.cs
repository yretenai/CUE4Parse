using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Utils;

namespace CUE4Parse.UE4.Wwise.Exports;

[StructFallback]
public class WwiseSwitchContainerLeafCookedData
{
    public List<WwiseGroupValueCookedData> GroupValueSet { get; init; } = new();
    public List<WwiseSoundBankCookedData> SoundBanks { get; init; } = new();
    public List<WwiseMediaCookedData> Media { get; init; } = new();
    public List<WwiseExternalSourceCookedData> ExternalSources { get; init; } = new();

    public WwiseSwitchContainerLeafCookedData()
    {
    }

    public WwiseSwitchContainerLeafCookedData(FStructFallback fallback)
    {
        GroupValueSet = fallback.GetOrDefault(nameof(GroupValueSet), GroupValueSet);
        SoundBanks = fallback.GetOrDefault(nameof(SoundBanks), SoundBanks);
        Media = fallback.GetOrDefault(nameof(Media), Media);
        ExternalSources = fallback.GetOrDefault(nameof(ExternalSources), ExternalSources);
    }
}