using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CUE4Parse_Conversion.ActorX;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Writers;
using CUE4Parse.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace CUE4Parse_Conversion.Worlds;

public class WorldExporter : ExporterBase {
    public readonly string WorldName;

    public WorldExporter(UWorld world, ETexturePlatform platform, ExporterOptions options, string? suffix = null) : base(world, options, suffix) {
        WorldName = world.Owner?.Name ?? world.Name;
        WorldName += Suffix;

        var (actors, lights, overrideMaterials, landscapes) = WorldConverter.ConvertWorld(world, platform);

        using var Ar = new FArchiveWriter();
        Ar.SerializeChunkHeader(new VChunkHeader(), "WRLDHEAD");

        var actorsHdr = new VChunkHeader {
            DataCount = actors.Count,
            DataSize = 560,
        };

        Ar.SerializeChunkHeader(actorsHdr, "WORLDACTORS::2");

        foreach (var actor in actors) {
            if (actor.Name is { Length: > 256 }) {
                actor.Name = actor.Name[..256];
            }

            actor.Serialize(Ar);
        }

        var lightsHdr = new VChunkHeader {
            DataCount = lights.Count,
            DataSize = 48,
        };

        Ar.SerializeChunkHeader(lightsHdr, "WORLDLIGHTS");

        foreach (var light in lights) {
            light.Serialize(Ar);
        }

        var overrideHdr = new VChunkHeader {
            DataCount = overrideMaterials.Count,
            DataSize = 72,
        };
        Ar.SerializeChunkHeader(overrideHdr, "INSTMATERIAL");

        foreach (var (actorId, materialId, overrideMaterial) in overrideMaterials) {
            Ar.Write(actorId);
            Ar.Write(materialId);
            Ar.Write(overrideMaterial, 64);
        }

        var landscapeHdr = new VChunkHeader {
            DataCount = landscapes.Count,
            DataSize = 296,
        };
        Ar.SerializeChunkHeader(landscapeHdr, "LANDSCAPE");

        foreach (var landscape in landscapes) {
            landscape.Serialize(Ar);
        }

        FileData = Ar.GetBuffer();
        LandscapeHeights = landscapes.Where(x => x.Heightmap.Any()).DistinctBy(x => x.Path)
                                     .ToDictionary(x => x.Path!, x => (x.Heightmap, x.OrigX, x.OrigY));
    }

    public byte[] FileData { get; }
    public Dictionary<string, (float[], int, int)> LandscapeHeights { get; }

    public override bool TryWriteToDir(DirectoryInfo baseDirectory, out string label, out string savedFileName) {
        savedFileName = WorldName.SubstringAfterLast('/');
        label = WorldName;
        if (!baseDirectory.Exists || FileData.Length <= 0) {
            return false;
        }

        var filePath = FixAndCreatePath(baseDirectory, WorldName + ".psw");
        File.WriteAllBytes(filePath, FileData);
        savedFileName = Path.GetFileName(filePath);
        if (!File.Exists(filePath)) {
            return false;
        }

        foreach (var (path, (height, x, y)) in LandscapeHeights) {
            filePath = FixAndCreatePath(baseDirectory, path);
            // unfortunately SKColorType.Rgba16161616 is "To be added", so we have to introduce another dependency.
            using var image = Image.LoadPixelData<RgbaVector>(height.Select(heightPoint => new RgbaVector(heightPoint, heightPoint, heightPoint, heightPoint)).ToArray(), x, y);
            image.SaveAsPng(filePath + ".png");
        }

        return true;
    }

    public override bool TryWriteToZip(out byte[] zipFile) => throw new NotImplementedException();

    public override void AppendToZip() {
        throw new NotImplementedException();
    }
}
