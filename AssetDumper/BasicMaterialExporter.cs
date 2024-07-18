using CUE4Parse_Conversion;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;
using Newtonsoft.Json;

namespace AssetDumper;

public record BasicTexture(string Path) {
    public float SamplingScale { get; set; } = 1.0f;
    public int UVChannelIndex { get; set; }
}

public class BasicMaterialData {
    public Dictionary<string, BasicTexture> Textures { get; } = [];
    public Dictionary<string, float> Scalars { get; } = [];
    public Dictionary<string, FLinearColor> Vectors { get; set; } = [];
    public Dictionary<string, bool> Switches { get; set; } = [];
    public Dictionary<string, FLinearColor> Masks { get; set; } = [];
    public FStructFallback? SubsurfaceProfile { get; set; }
    public List<string> Hierarchy { get; set; } = [];
    public string Name { get; set; } = "None";

    public void MergeTexture(string name, string? path, bool purge = true) {
        if (string.IsNullOrEmpty(path)) {
            return;
        }

        var dot = path.LastIndexOf('.');
        if (dot > -1) {
            path = path[..dot];
        }

        if (purge) {
            var removeKeys = new HashSet<string>();
            foreach (var (key, value) in Textures) {
                if (key == name) {
                    continue;
                }

                if (value.Path.Equals(path, StringComparison.Ordinal)) {
                    removeKeys.Add(key);
                }
            }

            foreach (var key in removeKeys) {
                Textures.Remove(key);
            }
        }

        if (!Textures.TryGetValue(name, out var basicTexture)) {
            Textures[name] = new BasicTexture(path);
            return;
        }

        Textures[name] = basicTexture with {
            Path = path,
        };
    }
}

public class BasicMaterialExporter : ExporterBase {
    private BasicMaterialData MaterialData { get; }
    private string InternalPath { get; } = string.Empty;

    public BasicMaterialExporter(ExporterOptions options, string? suffix = null) {
        Options = options;
        Suffix = suffix ?? string.Empty;
        MaterialData = new BasicMaterialData();
    }

    public BasicMaterialExporter(UMaterialInterface? unrealMaterial, ExporterOptions options, string? suffix = null) : this(options, suffix) {
        if (unrealMaterial == null) return;

        InternalPath = unrealMaterial.Owner?.Name ?? unrealMaterial.Name;

        ProcessMaterial(unrealMaterial);

        MaterialData.Name = unrealMaterial.Name;
    }

    private void ProcessMaterial(UMaterialInterface unrealMaterial) {
        if (unrealMaterial is UMaterialInstance { Parent: UMaterialInterface parent }) {
            ProcessMaterial(parent);
        }

        MaterialData.Hierarchy.Add(unrealMaterial.Name);

        ProcessCachedExpressionData(unrealMaterial.CachedExpressionData);
        ProcessCachedExpressionData(unrealMaterial.MaterialCachedExpressionData);

        switch (unrealMaterial) {
            case UMaterialInstance materialInstance: {
                if (materialInstance.StaticParameters != null) {
                    foreach (var switchParameter in materialInstance.StaticParameters.StaticSwitchParameters) {
                        MaterialData.Switches[switchParameter.Name] = switchParameter.Value;
                    }

                    foreach (var switchParameter in materialInstance.StaticParameters.StaticComponentMaskParameters) {
                        MaterialData.Masks[switchParameter.Name] = new FLinearColor(switchParameter.R ? 1f : 0f, switchParameter.G ? 1f : 0f, switchParameter.B ? 1f : 0f, switchParameter.A ? 1f : 0f);
                    }
                }

                if (materialInstance is UMaterialInstanceConstant materialInstanceConstant) {
                    foreach (var scalarParameterValue in materialInstanceConstant.ScalarParameterValues) {
                        MaterialData.Scalars[scalarParameterValue.ParameterInfo.Name.Text] = scalarParameterValue.ParameterValue;
                    }

                    foreach (var textureParameterValue in materialInstanceConstant.TextureParameterValues) {
                        MaterialData.MergeTexture(textureParameterValue.ParameterInfo.Name.Text, textureParameterValue.ParameterValue.ResolvedObject?.GetPathName());
                    }

                    foreach (var vectorParameterValue in materialInstanceConstant.VectorParameterValues) {
                        MaterialData.Vectors[vectorParameterValue.ParameterInfo.Name.Text] = vectorParameterValue.ParameterValue ?? new FLinearColor();
                    }
                }

                break;
            }
            case UMaterial { MaterialCachedExpressionData: null } material: {
                foreach (var texture in material.ReferencedTextures) {
                    MaterialData.MergeTexture(texture.Name, texture.GetPathName(), false);
                }

                break;
            }
        }

        foreach (var materialInfo in unrealMaterial.TextureStreamingData) {
            if (MaterialData.Textures.TryGetValue(materialInfo.TextureName.Text, out var texture)) {
                texture.SamplingScale = materialInfo.SamplingScale;
                texture.UVChannelIndex = materialInfo.UVChannelIndex;
            }
        }

        if (unrealMaterial.TryGetValue<FPackageIndex>(out var subsurfImport, "SubsurfaceProfile") &&
            subsurfImport.TryLoad(out var subsurfObj) &&
            subsurfObj.TryGetValue<FStructFallback>(out var subsurfProfile, "Settings")) {
            MaterialData.SubsurfaceProfile = subsurfProfile;
        }
    }

    private void ProcessCachedExpressionData(FStructFallback? cachedExpressionData) {
        if (cachedExpressionData != null &&
            cachedExpressionData.TryGetValue(out FStructFallback materialParameters, "Parameters") &&
            materialParameters.TryGetAllValues(out FStructFallback[] runtimeEntries, "RuntimeEntries")) {
            if (materialParameters.TryGetValue(out float[] scalarValues, "ScalarValues") &&
                runtimeEntries[0].TryGetValue(out FMaterialParameterInfo[] scalarParameterInfos, "ParameterInfos")) {
                for (var index = 0; index < scalarParameterInfos.Length; index++) {
                    var scalarParameter = scalarParameterInfos[index];
                    MaterialData.Scalars[scalarParameter.Name.Text] = scalarValues[index];
                }
            }

            if (materialParameters.TryGetValue(out FLinearColor[] vectorValues, "VectorValues") &&
                runtimeEntries[1].TryGetValue(out FMaterialParameterInfo[] vectorParameterInfos, "ParameterInfos")) {
                for (var index = 0; index < vectorParameterInfos.Length; index++) {
                    var vectorParameter = vectorParameterInfos[index];
                    MaterialData.Vectors[vectorParameter.Name.Text] = vectorValues[index];
                }
            }

            if (materialParameters.TryGetValue(out FPackageIndex[] textureValues, "TextureValues") &&
                runtimeEntries[2].TryGetValue(out FMaterialParameterInfo[] textureParameterInfos, "ParameterInfos")) {
                for (var index = 0; index < textureParameterInfos.Length; index++) {
                    var textureParameter = textureParameterInfos[index];
                    MaterialData.MergeTexture(textureParameter.Name.Text, textureValues[index].ResolvedObject?.GetPathName());
                }
            }
        }
    }

    public override bool TryWriteToDir(DirectoryInfo baseDirectory, out string label, out string savedFilePath) {
        label = string.Empty;
        savedFilePath = string.Empty;
        if (!baseDirectory.Exists) return false;

        savedFilePath = FixAndCreatePath(baseDirectory, InternalPath + Suffix, "json");
        File.WriteAllText(savedFilePath, JsonConvert.SerializeObject(MaterialData, Formatting.Indented));
        label = Path.GetFileName(savedFilePath);
        return true;
    }

    public override bool TryWriteToZip(out byte[] zipFile) => throw new NotImplementedException();

    public override void AppendToZip() {
        throw new NotImplementedException();
    }
}
