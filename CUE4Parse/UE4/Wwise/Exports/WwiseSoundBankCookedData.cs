using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.UObject;

namespace CUE4Parse.UE4.Wwise.Exports;

[StructFallback]
public class WwiseSoundBankCookedData : IWwiseDebugName {
    public WwiseSoundBankCookedData() { }

    public WwiseSoundBankCookedData(FStructFallback fallback) {
        SoundBankId = fallback.GetOrDefault<int>(nameof(SoundBankId));
        SoundBankPathName = fallback.GetOrDefault<FName>(nameof(SoundBankPathName));
        MemoryAlignment = fallback.GetOrDefault<int>(nameof(MemoryAlignment));
        DeviceMemory = fallback.GetOrDefault<bool>(nameof(DeviceMemory));
        ContainsMedia = fallback.GetOrDefault<bool>(nameof(ContainsMedia));
        SoundBankType = fallback.GetOrDefault<EWwiseSoundBankType>(nameof(SoundBankType));
        DebugName = fallback.GetOrDefault<FName>(nameof(DebugName));
    }

    public int SoundBankId { get; set; }
    public FName SoundBankPathName { get; set; }
    public int MemoryAlignment { get; set; }
    public bool DeviceMemory { get; set; }
    public bool ContainsMedia { get; set; }
    public EWwiseSoundBankType SoundBankType { get; set; }
    public FName DebugName { get; set; }
}
