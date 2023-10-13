using System.ComponentModel;

namespace CUE4Parse.UE4.Assets.Exports.Material
{
    public enum EMaterialFormat
    {
        [Description("First Layer Only")]
        FirstLayer,
        [Description("All Layers (Without Referenced Textures, Not Guessing Types)")]
        AllLayersNoRef,
        [Description("All Layers (With All Referenced Textures)")]
        AllLayers,
        [Description("All Layers (With All Referenced Textures, Not Guessing Types)")]
        AllLayersNoGuess,
    }
}
