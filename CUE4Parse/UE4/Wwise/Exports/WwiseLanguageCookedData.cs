using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Utils;
using CUE4Parse.UE4.Objects.UObject;

namespace CUE4Parse.UE4.Wwise.Exports;

[StructFallback]
public class WwiseLanguageCookedData
{
    public int LanguageId { get; set; }
    public FName LanguageName { get; set; }
    public EWwiseLanguageRequirement LanguageRequirement { get; set; }

    public WwiseLanguageCookedData()
    {
    }

    public WwiseLanguageCookedData(FStructFallback fallback)
    {
        LanguageId = fallback.GetOrDefault<int>(nameof(LanguageId));
        LanguageName = fallback.GetOrDefault<FName>(nameof(LanguageName));
        LanguageRequirement = fallback.GetOrDefault<EWwiseLanguageRequirement>(nameof(LanguageRequirement));
    }
}