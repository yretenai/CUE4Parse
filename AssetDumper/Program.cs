using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Animations;
using CUE4Parse_Conversion.Materials;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Sounds;
using CUE4Parse_Conversion.Textures;
using CUE4Parse_Conversion.Worlds;
using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.GameTypes.OS.Assets.Exports;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.Sound;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Exports.Wwise;
using CUE4Parse.UE4.Localization;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Shaders;
using CUE4Parse.UE4.Versions;
using CUE4Parse.UE4.VirtualFileSystem;
using CUE4Parse.UE4.Wwise.Exports;
using DragonLib;
using DragonLib.CommandLine;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using SkiaSharp;

namespace AssetDumper;

public static class Program {
    public static async Task Main() {
        var flags = CommandLineFlagsParser.ParseFlags<Flags>();
        if (flags == null) {
            return;
        }

        Oodle.Load(Environment.CurrentDirectory);

        var target = Path.GetFullPath(flags.OutputPath);
        var targetBaseDir = new DirectoryInfo(target);
        targetBaseDir.Create();

        if (!flags.Dry) {
            await using var assetDumperStream = new FileStream(Path.Combine(target, "CUE4AssetDumper.root"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            await using var assetDumperWriter = new StreamWriter(assetDumperStream, Encoding.UTF8, leaveOpen: true);
            using var assetDumperReader = new StreamReader(assetDumperStream, Encoding.UTF8, leaveOpen: true);

            if (!Directory.Exists(flags.PakPath)) {
                while (!assetDumperReader.EndOfStream) {
                    var line = await assetDumperReader.ReadLineAsync();
                    if (line is null) {
                        continue;
                    }

                    var parts = line.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length < 2) {
                        continue;
                    }

                    if (parts[0] == "path") {
                        flags.PakPath = parts[1];
                        continue;
                    }

                    if (parts[0] == "game") {
                        flags.Game = Enum.Parse<EGame>(parts[1]);
                        continue;
                    }

                    if (parts[0] == "mappings") {
                        flags.Mappings = parts[1];
                    }

                    if (parts[0] == "flags") {
                        var enabled = parts[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                        flags.NoJSON = !enabled.Contains("json", StringComparer.InvariantCultureIgnoreCase);
                        flags.NoMaterial = !enabled.Contains("material", StringComparer.InvariantCultureIgnoreCase);
                        flags.NoTextures = !enabled.Contains("textures", StringComparer.InvariantCultureIgnoreCase);
                        flags.NoSounds = !enabled.Contains("sounds", StringComparer.InvariantCultureIgnoreCase);
                        flags.NoMeshes = !enabled.Contains("meshes", StringComparer.InvariantCultureIgnoreCase);
                        flags.NoWorlds = !enabled.Contains("worlds", StringComparer.InvariantCultureIgnoreCase);
                        flags.NoAnimations = !enabled.Contains("animations", StringComparer.InvariantCultureIgnoreCase);
                        flags.NoUnknown = !enabled.Contains("unknown", StringComparer.InvariantCultureIgnoreCase);
                        flags.WwiseEvents = enabled.Contains("wwnames", StringComparer.InvariantCultureIgnoreCase);
                        flags.WwiseRename = enabled.Contains("wwise", StringComparer.InvariantCultureIgnoreCase);
                        flags.SaveLocRes = enabled.Contains("locres", StringComparer.InvariantCultureIgnoreCase);
                        flags.Raw = enabled.Contains("raw", StringComparer.InvariantCultureIgnoreCase);
                        flags.DebugMappings = enabled.Contains("debug-mappings", StringComparer.InvariantCultureIgnoreCase);
                        continue;
                    }

                    if (parts[0] == "keys") {
                        var keys = parts[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        flags.Keys = keys.Where(x => x.Trim().Length >= 32).ToList();
                        continue;
                    }

                    if (parts[0] == "keyguids") {
                        var keys = parts[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        flags.KeyGuids = keys.Where(x => x.Trim().Length >= 32).ToList();
                        continue;
                    }

                    if (parts[0] == "filters") {
                        var filters = parts[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        foreach (var filter in filters) {
                            flags.Filters.Add(new Regex(filter, RegexOptions.Compiled | RegexOptions.IgnoreCase));
                        }

                        continue;
                    }

                    if (parts[0] == "skip") {
                        var skip = parts[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        foreach (var skipClass in skip) {
                            flags.SkipClasses.Add(skipClass);
                        }

                        continue;
                    }

                    if (parts[0] == "format") {
                        var formats = parts[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                        flags.LodFormat = Enum.Parse<ELodFormat>(formats[0]);
                        if (formats.Length > 1) {
                            flags.MeshFormat = Enum.Parse<EMeshFormat>(formats[1]);
                            if (formats.Length > 2) {
                                flags.TextureFormat = Enum.Parse<ETextureFormat>(formats[2]);
                                if (formats.Length > 3) {
                                    flags.Platform = Enum.Parse<ETexturePlatform>(formats[3]);
                                    if (formats.Length > 4) {
                                        flags.AnimationFormat = Enum.Parse<EAnimFormat>(formats[4]);
                                        if (formats.Length > 5) {
                                            flags.Language = Enum.Parse<ELanguage>(formats[5]);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            assetDumperStream.SetLength(0);

            await assetDumperWriter.WriteLineAsync($"path: {flags.PakPath}");
            await assetDumperWriter.WriteLineAsync($"game: {flags.Game}");
            await assetDumperWriter.WriteLineAsync($"mappings: {flags.Mappings}");
            await assetDumperWriter.WriteLineAsync($"flags: {(flags.NoJSON ? "" : "json,")}{(flags.NoMaterial ? "" : "material,")}{(flags.NoTextures ? "" : "textures,")}{(flags.NoSounds ? "" : "sounds,")}{(flags.NoMeshes ? "" : "meshes,")}{(flags.NoWorlds ? "" : "worlds,")}{(flags.NoAnimations ? "" : "animations,")}{(flags.NoUnknown ? "" : "unknown,")}{(flags.WwiseEvents ? "wwnames," : "")}{(flags.WwiseRename ? "wwise," : "")}{(flags.SaveLocRes ? "locres," : "")}{(flags.Raw ? "raw," : "")}{(flags.DebugMappings ? "debug-mappings," : "")}");
            await assetDumperWriter.WriteLineAsync($"keys: {string.Join(',', flags.Keys)}");
            await assetDumperWriter.WriteLineAsync($"keyguids: {string.Join(',', flags.Keys)}");
            await assetDumperWriter.WriteLineAsync($"filters: {string.Join(',', flags.Filters.Select(x => x.ToString()))}");
            await assetDumperWriter.WriteLineAsync($"skip: {string.Join(',', flags.SkipClasses)}");
            await assetDumperWriter.WriteLineAsync($"format: {flags.LodFormat},{flags.MeshFormat},{flags.TextureFormat},{flags.Platform},{flags.AnimationFormat},{flags.Language}");
        }

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(target, "Log.txt"), LogEventLevel.Information)
            .CreateLogger();

        using var Provider = new DefaultFileProvider(Path.GetFullPath(flags.PakPath), SearchOption.AllDirectories, false, new VersionContainer(flags.Game, flags.Platform));
        Provider.UseLazySerialization = false;
        flags.Mappings ??= Directory.GetFiles(flags.PakPath, "*.usmap", SearchOption.AllDirectories).SingleOrDefault();
        if (File.Exists(flags.Mappings)) {
            Provider.MappingsContainer = new FileUsmapTypeMappingsProvider(flags.Mappings);
        }

        if (flags.DebugMappings && Provider.MappingsContainer is not null) {
            await using var mappingsDump = new StreamWriter(Path.Combine(target, "Mappings.cs"));
            Provider.MappingsContainer.MappingsForGame.DumpDummyClasses(mappingsDump);
        }

        Provider.Initialize();
        if (flags.Keys.Any()) {
            var keys = flags.Keys.Select(x => new FAesKey(x)).ToList();
            var guids = flags.KeyGuids.Select(x => new FGuid(x)).ToList();

            if (guids.Any()) {
                await Provider.SubmitKeysAsync(guids.Zip(keys).Select(x => new KeyValuePair<FGuid, FAesKey>(x.First, x.Second)));
            }

            var remain = keys.Skip(guids.Count).ToArray();
            if (remain.Any()) {
                var foundKeys = new Dictionary<FGuid, FAesKey>();
                foreach (var reader in Provider.UnloadedVfs) {
                    if (foundKeys.ContainsKey(reader.EncryptionKeyGuid)) {
                        continue;
                    }

                    foreach (var key in keys) {
                        if (reader.TestAesKey(key)) {
                            foundKeys[reader.EncryptionKeyGuid] = key;
                        }
                    }
                }

                await Provider.SubmitKeysAsync(foundKeys);
            }
        }

        if (File.Exists(Path.Combine(target, "keys.txt"))) {
            var lines = await File.ReadAllLinesAsync(Path.Combine(target, "keys.txt"));
            var keys = lines.Where(x => x.Trim().Length >= 32)
                .Select(x => {
                    // formats:
                    // key{s;32}
                    // guid{s:32} key{s:32}
                    x = x.Trim();
                    var hasGuid = x.Length > 32;
                    var guid = new FGuid();
                    if (hasGuid) {
                        guid = new FGuid(x[..32].Trim());
                    }

                    return new KeyValuePair<FGuid, FAesKey>(guid, new FAesKey(x));
                })
                .Where(x => !string.IsNullOrEmpty(x.Value.KeyString));

            await Provider.SubmitKeysAsync(keys);
        }

        await Provider.MountAsync();

        AbstractUePackage.SkipClasses.UnionWith(flags.SkipClasses);

        foreach (var keyGuid in Provider.RequiredKeys) {
            if (!Provider.Keys.ContainsKey(keyGuid)) {
                Log.Error("Requires missing encryption key 0x{Key}", keyGuid.ToString(EGuidFormats.Digits));
            }
        }

        Provider.LoadLocalization(flags.Language);

        if (flags.SaveLocRes) {
            await File.WriteAllTextAsync(Path.Combine(target, "localization.json"), JsonConvert.SerializeObject(Provider.LocalizedResources, Formatting.Indented));
        }

        if (flags.Dry) {
            flags.StubHistory = true;
        }

        History? history = null;
        History? oldHistory = null;
        if (!flags.StubHistory) {
            history = new History();
            var targetHistoryPath = Path.Combine(target, Path.GetFileName(target) + ".history");
            oldHistory = new History(flags.HistoryPath ?? targetHistoryPath);
        }

        var wwiseNames = new HashSet<string>();
        var wwiseRename = new Dictionary<string, string>();
        var wemList = new List<string>();

        var filesEnumerable = Provider.Files.DistinctBy(x => x.Key);
        if (flags.Filters.Any()) {
            filesEnumerable = filesEnumerable.Where(x => flags.Filters.Any(y => y.IsMatch(x.Key)));
        }

        if (!flags.Raw) {
            filesEnumerable = filesEnumerable.Where(x => x.Value.Extension is not ("ubulk" or "uexp" or "uptnl"));
        }

        var files = filesEnumerable.ToArray();
        var count = (float)files.Length;
        var processed = 0;

        var exportOptions = new ExporterOptions {
            MeshFormat = flags.MeshFormat,
            AnimFormat = flags.AnimationFormat,
            MaterialFormat = EMaterialFormat.AllLayers,
            TextureFormat = flags.TextureFormat,
            Platform = ETexturePlatform.DesktopMobile,
            SocketFormat = ESocketFormat.Bone,
            LodFormat = ELodFormat.FirstLod,
            ExportMaterials = true,
            ExportMorphTargets = true,
        };

        foreach (var (path, gameFile) in files) {
            var pc = ++processed / count * 100;

            var historyType = History.HistoryType.New;
            if (flags.StubHistory == false && oldHistory != null && history != null) {
                var historyEntry = await history.Add(Provider, gameFile);
                historyType = oldHistory.Has(historyEntry);
            }

            if (gameFile is VfsEntry vfs) {
                Log.Information("{Percent:N3}% {HistoryType:G} {GameFile} from {Source}", pc, historyType, gameFile, vfs.Vfs.Name);
            } else {
                Log.Information("{Percent:N3}% {HistoryType:G} {GameFile}", pc, historyType, gameFile);
            }

            if (flags.Raw) {
                var data = await gameFile.TryReadAsync();
                if (data != null) {
                    var rawPath = Path.Combine(target, "Raw", gameFile.Path);
                    rawPath.EnsureDirectoryExists();
                    await File.WriteAllBytesAsync(rawPath, data);
                }
            }

            if (flags.Dry) {
                continue;
            }

            if (historyType == History.HistoryType.Same) {
                continue;
            }

            var targetGameFile = Path.Combine(target, gameFile.Path);
            var targetJsonPath = Path.Combine(target, "Json", gameFile.Path + ".json");

            try {
                switch (gameFile.Extension.ToLowerInvariant()) {
                    case "wem":
                    default: {
                        if (flags.NoUnknown) {
                            break;
                        }

                        if (Provider.TrySaveAsset(path, out var data)) {
                            targetGameFile.EnsureDirectoryExists();
                            await using var stream = new FileStream(targetGameFile, FileMode.Create, FileAccess.Write);
                            stream.Write(data, 0, data.Length);
                            if (flags.WwiseRename && gameFile.Extension == "wem") {
                                wemList.Add(gameFile.Path);
                            }
                        }

                        break;
                    }

                    case "locres": {
                        if (flags.NoJSON) {
                            break;
                        }

                        if (Provider.TryCreateReader(path, out var archive)) {
                            targetJsonPath.EnsureDirectoryExists();
                            await File.WriteAllTextAsync(targetJsonPath, JsonConvert.SerializeObject(new FTextLocalizationResource(archive), Formatting.Indented));
                        }

                        break;
                    }

                    case "locmeta": {
                        if (flags.NoJSON) {
                            break;
                        }

                        if (Provider.TryCreateReader(path, out var archive)) {
                            targetJsonPath.EnsureDirectoryExists();
                            await File.WriteAllTextAsync(targetJsonPath, JsonConvert.SerializeObject(new FTextLocalizationMetaDataResource(archive), Formatting.Indented));
                        }

                        break;
                    }

                    case "ushaderbytecode":
                    case "ushadercode": {
                        if (flags.NoJSON) {
                            break;
                        }

                        if (Provider.TryCreateReader(path, out var archive)) {
                            targetJsonPath.EnsureDirectoryExists();
                            await File.WriteAllTextAsync(targetJsonPath, JsonConvert.SerializeObject(new FShaderCodeArchive(archive), Formatting.Indented));
                        }

                        break;
                    }

                    case "uasset":
                    case "umap": {
                        try {
                            var exports = Provider.LoadAllObjects(path).ToArray();
                            var targetPath = Path.Combine(target, gameFile.PathWithoutExtension.SanitizeDirname());

                            if (!flags.NoJSON) {
                                // FMovieScene causes a lot of out-of-memory issues while serializing.
                                if (exports.All(x => x.Class?.Name.StartsWith("MovieScene") != true)) {
                                    targetJsonPath.EnsureDirectoryExists();
                                    try {
                                        await File.WriteAllTextAsync(targetJsonPath, JsonConvert.SerializeObject(exports, Formatting.Indented, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii }));
                                    } catch (Exception e) {
                                        Log.Error(e, "Failed to convert UObject exports to JSON");
                                    }
                                }
                            }

                            for (var exportIndex = 0; exportIndex < exports.Length; exportIndex++) {
                                var export = exports[exportIndex];
                                if (export.ExportType == "AkAudioEvent") {
                                    wwiseNames.Add(export.Name);

                                    if (export is AkAudioEvent akAudioEvent && (flags.WwiseRename || flags.WwiseEvents)) {
                                        if (flags.WwiseEvents) {
                                            wwiseNames.Add(akAudioEvent.EventCookedData.DebugName.PlainText);
                                        }

                                        foreach (var (locale, entry) in akAudioEvent.EventCookedData.EventLanguageMap) {
                                            if (flags.WwiseEvents) {
                                                var allDebug = entry.Media.Select(x => x as IWwiseDebugName)
                                                    .Concat(
                                                        entry.ExternalSources.Select(x => x as IWwiseDebugName))
                                                    .Concat(
                                                        entry.SoundBanks.Select(x => x as IWwiseDebugName))
                                                    .Concat(
                                                        entry.SwitchContainerLeaves.SelectMany(x => x.Media).Select(x => x as IWwiseDebugName))
                                                    .Concat(
                                                        entry.SwitchContainerLeaves.SelectMany(x => x.ExternalSources).Select(x => x as IWwiseDebugName))
                                                    .Concat(
                                                        entry.SwitchContainerLeaves.SelectMany(x => x.SoundBanks).Select(x => x as IWwiseDebugName));

                                                foreach (var media in allDebug) {
                                                    wwiseNames.Add(media.DebugName.PlainText);
                                                }
                                            }

                                            if (flags.WwiseRename) {
                                                foreach (var media in entry.Media) {
                                                    wwiseRename[media.MediaPathName.PlainText] = locale.LanguageName.PlainText + "/" + media.DebugName.PlainText.Replace('\\', '/').Replace(':', '_').Replace("..", "_");
                                                }
                                            }
                                        }
                                    }
                                }

                                try {
                                    switch (export) {
                                        case UTexture2D texture2D when !flags.NoTextures: {
                                            var texture = texture2D.Decode();
                                            if (texture != null) {
                                                targetPath.EnsureDirectoryExists();
                                                await using var fs = new FileStream(targetPath + $".{exportIndex}.png", FileMode.Create, FileAccess.Write);
                                                using var data = texture.Encode(SKEncodedImageFormat.Png, 100);
                                                await using var stream = data.AsStream();
                                                await stream.CopyToAsync(fs);
                                            }

                                            break;
                                        }
                                        case AnimatedTexture2D animated2d when !flags.NoTextures: {
                                            var texture = animated2d.FileBlob;
                                            if (texture.Length > 0 && animated2d.FileType != AnimatedTextureType.None) {
                                                targetPath.EnsureDirectoryExists();
                                                await using var fs = new FileStream(targetPath + $".{exportIndex}.{animated2d.FileType.ToString("G").ToLower()}", FileMode.Create, FileAccess.Write);
                                                await fs.WriteAsync(texture);
                                            }

                                            break;
                                        }
                                        case UAkMediaAssetData or USoundWave when !flags.NoSounds: {
                                            export.Decode(true, out var format, out var data);
                                            if (data != null && !string.IsNullOrEmpty(format)) {
                                                targetPath.EnsureDirectoryExists();
                                                await using var stream = new FileStream(targetPath + $".{exportIndex}.{format}", FileMode.Create, FileAccess.Write);
                                                stream.Write(data, 0, data.Length);
                                            }

                                            break;
                                        }
                                        case UAnimSequence animSequence when !flags.NoAnimations: {
                                            target.EnsureDirectoryExists();

                                            var exporter = flags.AnimationFormat switch {
                                                EAnimFormat.ActorX => new AnimExporterV2(animSequence, exportOptions, exportIndex),
                                                EAnimFormat.LegacyActorX => new AnimExporter(animSequence, exportOptions, exportIndex),
                                                _ => throw new ArgumentOutOfRangeException(),
                                            };

                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                        case UMaterialInstanceConstant materialInterface when !flags.NoMaterial: {
                                            target.EnsureDirectoryExists();
                                            var exporter = new MaterialExporter2(materialInterface, exportOptions);
                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                        case UMaterial material when !flags.NoMaterial && material.CachedExpressionData != null: {
                                            target.EnsureDirectoryExists();
                                            var exporter = new MaterialExporter2(material, exportOptions);
                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                        case UUnrealMaterial unrealMaterial when !flags.NoMaterial: {
                                            target.EnsureDirectoryExists();
                                            var exporter = new MaterialExporter2(unrealMaterial, exportOptions);
                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                        case USkeletalMesh skeletalMesh when !flags.NoMeshes: {
                                            target.EnsureDirectoryExists();
                                            var exporter = new MeshExporter(skeletalMesh, exportOptions, exportIndex);
                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                        case USkeleton skeleton when !flags.NoMeshes: {
                                            target.EnsureDirectoryExists();
                                            var exporter = new MeshExporter(skeleton, exportOptions, exportIndex);
                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                        case UStaticMesh staticMesh when !flags.NoMeshes: {
                                            target.EnsureDirectoryExists();
                                            var exporter = new MeshExporter(staticMesh, exportOptions, exportIndex);
                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                        case UWorld world when !flags.NoWorlds: {
                                            target.EnsureDirectoryExists();
                                            var exporter = new WorldExporter(world, flags.Platform, exportIndex);
                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                    }
                                } catch (Exception e) {
                                    Log.Error(e, "Failed to convert UObject export #{Export}", exportIndex);
                                }
                            }
                        } catch (Exception e) {
                            Log.Error(e, "Failed to process file");
                        }

                        break;
                    }
                }
            } catch (Exception e) {
                Log.Error(e, "Unknown error");
            }
        }

        if (!flags.StubHistory && history != null) {
            var targetHistoryPath = Path.Combine(target, Path.GetFileName(target) + ".history");
            history.Save(targetHistoryPath);
        }

        if (flags.WwiseEvents) {
            if (File.Exists(Path.Combine(target, "wwnames.txt"))) {
                wwiseNames.UnionWith(await File.ReadAllLinesAsync(Path.Combine(target, "wwnames.txt")));
            }

            await File.WriteAllTextAsync(Path.Combine(target, "wwnames.txt"), string.Join('\n', wwiseNames));
        }

        if (flags.WwiseRename) {
            foreach (var (hashedPath, realPath) in wwiseRename) {
                if (string.IsNullOrEmpty(realPath)) {
                    continue;
                }

                var gamePath = wemList.FirstOrDefault(x => x.EndsWith(hashedPath, StringComparison.OrdinalIgnoreCase));
                if (gamePath is null) {
                    continue;
                }

                var path = Path.Combine(target, "Wwise", realPath);
                path.EnsureDirectoryExists();
                path = Path.ChangeExtension(path, Path.GetExtension(gamePath));
                File.Copy(Path.Combine(target, gamePath), path, true);
            }
        }
    }
}
