using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.UObject;

namespace CUE4Parse.UE4.Wwise.Exports;

[StructFallback]
public class WwiseMediaCookedData : IWwiseDebugName
{
    public int MediaId { get; set; }
    public FName MediaPathName { get; set; }
    public int PrefetchSize { get; set; }
    public int MemoryAlignment { get; set; }
    public bool DeviceMemory { get; set; }
    public bool Streaming { get; set; }
    public FName DebugName { get; set; }

    public WwiseMediaCookedData()
    {
    }

    public WwiseMediaCookedData(FStructFallback fallback)
    {
        MediaId = fallback.GetOrDefault<int>(nameof(MediaId));
        MediaPathName = fallback.GetOrDefault<FName>(nameof(MediaPathName));
        PrefetchSize = fallback.GetOrDefault<int>(nameof(PrefetchSize));
        MemoryAlignment = fallback.GetOrDefault<int>(nameof(MemoryAlignment));
        DeviceMemory = fallback.GetOrDefault<bool>(nameof(DeviceMemory));
        Streaming = fallback.GetOrDefault<bool>(nameof(Streaming));
        DebugName = fallback.GetOrDefault<FName>(nameof(DebugName));
    }
}