using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
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
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Internationalization;
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
using Newtonsoft.Json.Converters;
using Serilog;
using Serilog.Events;
using SkiaSharp;

namespace AssetDumper;

public static class Program {
    [SuppressMessage("ReSharper.DPA", "DPA0001: Memory allocation issues")]
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
                var existing = JsonConvert.DeserializeObject<Flags>(await assetDumperReader.ReadToEndAsync(), new StringEnumConverter());
                if (existing is not null) {
                    flags.PakPath = existing.PakPath;
                    flags.Game = existing.Game;
                    flags.Mappings = existing.Mappings;
                    flags.NoJSON = existing.NoJSON;
                    flags.NoMaterial = existing.NoMaterial;
                    flags.NoTextures = existing.NoTextures;
                    flags.NoSounds = existing.NoSounds;
                    flags.NoMeshes = existing.NoMeshes;
                    flags.NoWorlds = existing.NoWorlds;
                    flags.NoAnimations = existing.NoAnimations;
                    flags.NoDataTable = existing.NoDataTable;
                    flags.NoStringTable = existing.NoStringTable;
                    flags.NoUnknown = existing.NoUnknown;
                    flags.TrackWwiseEvents = existing.TrackWwiseEvents;
                    flags.RenameWwiseAudio = existing.RenameWwiseAudio;
                    flags.SaveLocRes = existing.SaveLocRes;
                    flags.SaveRaw = existing.SaveRaw;
                    flags.DumpMappings = existing.DumpMappings;
                    flags.Keys = existing.Keys;
                    flags.KeyGuids = existing.KeyGuids;
                    flags.Filters = existing.Filters;
                    flags.SkipClasses = existing.SkipClasses;
                    flags.LodFormat = existing.LodFormat;
                    flags.MeshFormat = existing.MeshFormat;
                    flags.TextureFormat = existing.TextureFormat;
                    flags.AnimationFormat = existing.AnimationFormat;
                    flags.SocketFormat = existing.SocketFormat;
                    flags.MaterialFormat = existing.MaterialFormat;
                    flags.Platform = existing.Platform;
                    flags.Language = existing.Language;
                }
            }

            assetDumperStream.SetLength(0);

            await assetDumperWriter.WriteAsync(JsonConvert.SerializeObject(flags, Formatting.Indented, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.Default, Converters = { new StringEnumConverter() } }));
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

        if (flags.DumpMappings && Provider.MappingsContainer?.MappingsForGame is not null) {
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
            await File.WriteAllTextAsync(Path.Combine(target, "localization.json"), JsonConvert.SerializeObject(Provider.LocalizedResources, Formatting.Indented, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii, Converters = { new StringEnumConverter() } }));
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

        if (!flags.SaveRaw) {
            filesEnumerable = filesEnumerable.Where(x => x.Value.Extension is not ("ubulk" or "uexp" or "uptnl"));
        }

        var files = filesEnumerable.ToArray();
        var count = (float)files.Length;
        var processed = 0;

        var exportOptions = new ExporterOptions {
            MeshFormat = flags.MeshFormat,
            AnimFormat = flags.AnimationFormat,
            MaterialFormat = flags.MaterialFormat,
            TextureFormat = flags.TextureFormat,
            Platform = flags.Platform,
            SocketFormat = flags.SocketFormat,
            LodFormat = flags.LodFormat,
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

            if (flags.SaveRaw) {
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
                    default: {
                        if (flags.NoUnknown) {
                            break;
                        }

                        if (Provider.TrySaveAsset(path, out var data)) {
                            targetGameFile.EnsureDirectoryExists();
                            await using var stream = new FileStream(targetGameFile, FileMode.Create, FileAccess.Write);
                            stream.Write(data, 0, data.Length);
                            if (flags.RenameWwiseAudio && gameFile.Extension == "wem") {
                                wemList.Add(gameFile.Path);
                            }
                        }

                        break;
                    }

                    case "bnk":
                    case "wem": {
                        if (flags.NoSounds) {
                            break;
                        }

                        if (Provider.TrySaveAsset(path, out var data)) {
                            targetGameFile.EnsureDirectoryExists();
                            await using var stream = new FileStream(targetGameFile, FileMode.Create, FileAccess.Write);
                            stream.Write(data, 0, data.Length);
                            wemList.Add(gameFile.Path);
                        }

                        break;
                    }

                    case "locres": {
                        if (flags.NoJSON) {
                            break;
                        }

                        if (Provider.TryCreateReader(path, out var archive)) {
                            targetJsonPath.EnsureDirectoryExists();
                            await File.WriteAllTextAsync(targetJsonPath, JsonConvert.SerializeObject(new FTextLocalizationResource(archive), Formatting.Indented, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii, Converters = { new StringEnumConverter() } }));
                        }

                        break;
                    }

                    case "locmeta": {
                        if (flags.NoJSON) {
                            break;
                        }

                        if (Provider.TryCreateReader(path, out var archive)) {
                            targetJsonPath.EnsureDirectoryExists();
                            await File.WriteAllTextAsync(targetJsonPath, JsonConvert.SerializeObject(new FTextLocalizationMetaDataResource(archive), Formatting.Indented, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii, Converters = { new StringEnumConverter() } }));
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
                            await File.WriteAllTextAsync(targetJsonPath, JsonConvert.SerializeObject(new FShaderCodeArchive(archive), Formatting.Indented, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii, Converters = { new StringEnumConverter() } }));
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
                                        await File.WriteAllTextAsync(targetJsonPath, JsonConvert.SerializeObject(exports, Formatting.Indented, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii, Converters = { new StringEnumConverter() } }));
                                    } catch (Exception e) {
                                        Log.Error(e, "Failed to convert UObject exports to JSON");
                                    }
                                }
                            }

                            for (var exportIndex = 0; exportIndex < exports.Length; exportIndex++) {
                                var export = exports[exportIndex];
                                if (export.ExportType == "AkAudioEvent") {
                                    wwiseNames.Add(export.Name);

                                    if (export is AkAudioEvent akAudioEvent && (flags.RenameWwiseAudio || flags.TrackWwiseEvents)) {
                                        if (flags.TrackWwiseEvents) {
                                            wwiseNames.Add(akAudioEvent.EventCookedData.DebugName.PlainText);
                                        }

                                        foreach (var (locale, entry) in akAudioEvent.EventCookedData.EventLanguageMap) {
                                            if (flags.TrackWwiseEvents) {
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

                                            if (flags.RenameWwiseAudio) {
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
                                            targetPath.EnsureDirectoryExists();

                                            var exporter = flags.AnimationFormat switch {
                                                EAnimFormat.ActorX => new AnimExporterV2(animSequence, exportOptions, exportIndex),
                                                EAnimFormat.LegacyActorX => new AnimExporter(animSequence, exportOptions, exportIndex),
                                                _ => throw new ArgumentOutOfRangeException(),
                                            };

                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                        case UAnimMontage animMontage when !flags.NoAnimations: {
                                            targetPath.EnsureDirectoryExists();

                                            var exporter = flags.AnimationFormat switch {
                                                EAnimFormat.ActorX => new AnimExporterV2(animMontage, exportOptions, exportIndex),
                                                EAnimFormat.LegacyActorX => new AnimExporter(animMontage, exportOptions, exportIndex),
                                                _ => throw new ArgumentOutOfRangeException(),
                                            };

                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                        case UAnimComposite animComposite when !flags.NoAnimations: {
                                            targetPath.EnsureDirectoryExists();

                                            var exporter = flags.AnimationFormat switch {
                                                EAnimFormat.ActorX => new AnimExporterV2(animComposite, exportOptions, exportIndex),
                                                EAnimFormat.LegacyActorX => new AnimExporter(animComposite, exportOptions, exportIndex),
                                                _ => throw new ArgumentOutOfRangeException(),
                                            };

                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                        case UMaterialInstanceConstant materialInterface when !flags.NoMaterial: {
                                            targetPath.EnsureDirectoryExists();
                                            var exporter = new MaterialExporter2(materialInterface, exportOptions);
                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                        case UMaterial material when !flags.NoMaterial && material.CachedExpressionData != null: {
                                            targetPath.EnsureDirectoryExists();
                                            var exporter = new MaterialExporter2(material, exportOptions);
                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                        case UUnrealMaterial unrealMaterial when !flags.NoMaterial: {
                                            targetPath.EnsureDirectoryExists();
                                            var exporter = new MaterialExporter2(unrealMaterial, exportOptions);
                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                        case USkeletalMesh skeletalMesh when !flags.NoMeshes: {
                                            targetPath.EnsureDirectoryExists();
                                            var exporter = new MeshExporter(skeletalMesh, exportOptions, exportIndex);
                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                        case USkeleton skeleton when !flags.NoMeshes: {
                                            targetPath.EnsureDirectoryExists();
                                            var exporter = new MeshExporter(skeleton, exportOptions, exportIndex);
                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                        case UStaticMesh staticMesh when !flags.NoMeshes: {
                                            targetPath.EnsureDirectoryExists();
                                            var exporter = new MeshExporter(staticMesh, exportOptions, exportIndex);
                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                        case UWorld world when !flags.NoWorlds: {
                                            targetPath.EnsureDirectoryExists();
                                            var exporter = new WorldExporter(world, flags.Platform, exportIndex);
                                            exporter.TryWriteToDir(targetBaseDir, out _, out _);
                                            break;
                                        }
                                        case UDataTable dataTable when !flags.NoDataTable: {
                                            targetPath.EnsureDirectoryExists();
                                            await File.WriteAllTextAsync($"{targetPath}.{exportIndex}.json", JsonConvert.SerializeObject(dataTable, Formatting.Indented, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii, Converters = { new StringEnumConverter() } }));
                                            break;
                                        }
                                        case UStringTable stringTable when !flags.NoStringTable: {
                                            targetPath.EnsureDirectoryExists();
                                            await File.WriteAllTextAsync($"{targetPath}.{exportIndex}.json", JsonConvert.SerializeObject(stringTable, Formatting.Indented, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii, Converters = { new StringEnumConverter() } }));
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

        if (flags.TrackWwiseEvents) {
            if (File.Exists(Path.Combine(target, "wwnames.txt"))) {
                wwiseNames.UnionWith(await File.ReadAllLinesAsync(Path.Combine(target, "wwnames.txt")));
            }

            await File.WriteAllTextAsync(Path.Combine(target, "wwnames.txt"), string.Join('\n', wwiseNames));
        }

        if (flags.RenameWwiseAudio) {
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
