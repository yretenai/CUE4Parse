using CUE4Parse.UE4.Objects.UObject;

namespace CUE4Parse.UE4.Wwise.Exports;

public interface IWwiseDebugName
{
    public FName DebugName { get; set; }
}