using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CUE4Parse_Conversion;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
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
using CUE4Parse_Conversion.Animations;
using CUE4Parse_Conversion.Materials;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Sounds;
using CUE4Parse_Conversion.Textures;
using CUE4Parse_Conversion.Worlds;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.VirtualFileSystem;
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

        var target = Path.GetFullPath(flags.OutputPath);
        var targetBaseDir = new DirectoryInfo(target);
        targetBaseDir.Create();

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(target, "Log.txt"), LogEventLevel.Information)
            .CreateLogger();

        using var Provider = new DefaultFileProvider(Path.GetFullPath(flags.PakPath), SearchOption.AllDirectories, false, new VersionContainer(flags.Game, flags.Platform));
        flags.Mappings ??= Directory.GetFiles(flags.PakPath, "*.usmap", SearchOption.AllDirectories).SingleOrDefault();
        if (File.Exists(flags.Mappings)) {
            Provider.MappingsContainer = new FileUsmapTypeMappingsProvider(flags.Mappings);
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
                    if (foundKeys.ContainsKey(reader.EncryptionKeyGuid)) continue;

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
            var keys = lines.Where(x => x.Trim().Length >= 32).Select(x => {
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
            }).Where(x => !string.IsNullOrEmpty(x.Value.KeyString));

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
            }
            else {
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
                    default: {
                        if (flags.NoUnknown) {
                            break;
                        }

                        if (Provider.TrySaveAsset(path, out var data)) {
                            targetGameFile.EnsureDirectoryExists();
                            await using var stream = new FileStream(targetGameFile, FileMode.Create, FileAccess.Write);
                            stream.Write(data, 0, data.Length);
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
                                    }
                                    catch (Exception e) {
                                        Log.Error(e, "Failed to convert UObject exports to JSON");
                                    }
                                }
                            }

                            for (var exportIndex = 0; exportIndex < exports.Length; exportIndex++) {
                                var export = exports[exportIndex];
                                if (export.ExportType == "AkAudioEvent") {
                                    wwiseNames.Add(export.Name);
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
                                }
                                catch (Exception e) {
                                    Log.Error(e, "Failed to convert UObject export #{Export}", exportIndex);
                                }
                            }
                        }
                        catch (Exception e) {
                            Log.Error(e, "Failed to process file");
                        }

                        break;
                    }
                }
            }
            catch (Exception e) {
                Log.Error(e, "Unknown error");
            }
        }

        if (!flags.StubHistory && history != null) {
            var targetHistoryPath = Path.Combine(target, Path.GetFileName(target) + ".history");
            history.Save(targetHistoryPath);
        }

        if (flags.WwiseEvents) {
            await File.WriteAllTextAsync(Path.Combine(target, "wwnames.txt"), string.Join('\n', wwiseNames));
        }
    }
}
