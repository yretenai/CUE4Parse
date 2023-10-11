using System.Collections.Generic;
using System.Text.RegularExpressions;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Textures;
using DragonLib.CommandLine;

namespace AssetDumper;

public record Flags : CommandLineFlags {
    [Flag("raw", Help = "Dump raw uasset files", Category = "Export")]
    public bool Raw { get; set; }

    [Flag("wwise-events", Help = "Keep track of Wwise Events", Category = "Export")]
    public bool WwiseEvents { get; set; }

    [Flag("wwise", Help = "Rename Wwise WEMs if a DebugName is present", Category = "Export")]
    public bool WwiseRename { get; set; }

    [Flag("locres", Help = "Saave localization data", Category = "Export")]
    public bool SaveLocRes { get; set; }

    [Flag("no-json", Help = "Suppress JSON generation", Category = "Export")]
    public bool NoJSON { get; set; }

    [Flag("no-materials", Help = "Suppress Material conversion", Category = "Export")]
    public bool NoMaterial { get; set; }

    [Flag("no-textures", Help = "Suppress Texture conversion", Category = "Export")]
    public bool NoTextures { get; set; }

    [Flag("no-sounds", Help = "Suppress Sound conversion", Category = "Export")]
    public bool NoSounds { get; set; }

    [Flag("no-meshes", Help = "Suppress Mesh conversion", Category = "Export")]
    public bool NoMeshes { get; set; }

    [Flag("no-world", Help = "Suppress World conversion", Category = "Export")]
    public bool NoWorlds { get; set; }

    [Flag("no-animations", Help = "Suppress Animation conversion", Category = "Export")]
    public bool NoAnimations { get; set; }

    [Flag("no-unknown", Help = "Suppress unknown files from being saved", Category = "Export")]
    public bool NoUnknown { get; set; }

    [Flag("dry", Help = "Only list files", Category = "Export")]
    public bool Dry { get; set; }

    [Flag("usmap", Help = "Unreal Engine Struct Mappings", Category = "CUE4Parse")]
    public string? Mappings { get; set; }

    [Flag("debug-usmap", Help = "Dump Unreal Engine Struct Mappings to a dummy file", Category = "CUE4Parse")]
    public bool DebugMappings { get; set; }
    
    [Flag("aes", Aliases = new[] { "k" }, Help = "AES key values for the packages", Category = "CUE4Parse")]
    public List<string> Keys { get; set; } = new();

    [Flag("guid", Aliases = new[] { "K" }, Help = "AES key guids for the packages", Category = "CUE4Parse")]
    public List<string> KeyGuids { get; set; } = new();

    [Flag("game", Help = "Unreal Version to use", Category = "CUE4Parse", EnumPrefix = new[] { "GAME_" }, ReplaceDashes = '_', ReplaceDots = '_')]
    public EGame Game { get; set; } = EGame.GAME_UE4_LATEST;

    [Flag("lod", Help = "LOD export format", Category = "CUE4Parse")]
    public ELodFormat LodFormat { get; set; } = ELodFormat.FirstLod;

    [Flag("mesh-format", Help = "Mesh format to export to", Category = "CUE4Parse")]
    public EMeshFormat MeshFormat { get; set; } = EMeshFormat.ActorX;

    [Flag("texture-format", Help = "Texture format to export to", Category = "CUE4Parse")]
    public ETextureFormat TextureFormat { get; set; } = ETextureFormat.Png;

    [Flag("anim-format", Help = "Animation format to export to", Category = "CUE4Parse")]
    public EAnimFormat AnimationFormat { get; set; } = EAnimFormat.ActorX;

    [Flag("platform", Help = "Platform of the game", Category = "CUE4Parse")]
    public ETexturePlatform Platform { get; set; } = ETexturePlatform.DesktopMobile;

    [Flag("language", Help = "Langauge to load LocRes for", Category = "CUE4Parse")]
    public ELanguage Language { get; set; } = ELanguage.English;

    [Flag("filter", Help = "Path filters", Category = "AssetDumper")]
    public List<Regex> Filters { get; set; } = new();
    
    [Flag("skip-class", Aliases = new[] { "e" }, Help = "Classes to skip", Category = "AssetDumper")]
    public HashSet<string> SkipClasses { get; set; } = new();

    [Flag("history-path", Aliases = new[] { "history" }, Help = "Path to the .history file for the previous version", Category = "AssetDumper")]
    public string? HistoryPath { get; set; }

    [Flag("history-name", Help = "This history's name", Category = "AssetDumper")]
    public string? HistoryName { get; set; }

    [Flag("stub-history", Help = "Stub history comparison", Category = "AssetDumper")]
    public bool StubHistory { get; set; }

    [Flag("output-path", IsRequired = true, Positional = 1, Help = "Path to where to save files", Category = "AssetDumper")]
    public string OutputPath { get; set; } = null!;

    [Flag("pak-path", IsRequired = true, Positional = 0, Help = "Path to where the packages are", Category = "AssetDumper")]
    public string PakPath { get; set; } = null!;
}
