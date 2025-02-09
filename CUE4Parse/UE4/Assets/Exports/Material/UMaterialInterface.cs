using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.Material
{
    [SkipObjectRegistration]
    public class UMaterialInterface : UUnrealMaterial
    {
        //I think those aren't used in UE4 but who knows
        //to delete
        public bool bUseMobileSpecular;
        public float MobileSpecularPower = 16.0f;
        public EMobileSpecularMask MobileSpecularMask = EMobileSpecularMask.MSM_Constant;
        public UTexture? FlattenedTexture;
        public UTexture? MobileBaseTexture;
        public UTexture? MobileNormalTexture;
        public UTexture? MobileMaskTexture;

        public FStructFallback? CachedExpressionData;
        public FStructFallback? MaterialCachedExpressionData;
        public FMaterialTextureInfo[] TextureStreamingData = Array.Empty<FMaterialTextureInfo>();
        public List<FMaterialResource> LoadedMaterialResources = new();

        public override void Deserialize(FAssetArchive Ar, long validPos)
        {
            base.Deserialize(Ar, validPos);
            bUseMobileSpecular = GetOrDefault<bool>(nameof(bUseMobileSpecular));
            MobileSpecularPower = GetOrDefault<float>(nameof(MobileSpecularPower));
            MobileSpecularMask = GetOrDefault<EMobileSpecularMask>(nameof(MobileSpecularMask));
            FlattenedTexture = GetOrDefault<UTexture>(nameof(FlattenedTexture));
            MobileBaseTexture = GetOrDefault<UTexture>(nameof(MobileBaseTexture));
            MobileNormalTexture = GetOrDefault<UTexture>(nameof(MobileNormalTexture));
            MobileMaskTexture = GetOrDefault<UTexture>(nameof(MobileMaskTexture));

            TextureStreamingData = GetOrDefault(nameof(TextureStreamingData), Array.Empty<FMaterialTextureInfo>());

            var bSavedCachedExpressionData = FUE5ReleaseStreamObjectVersion.Get(Ar) >= FUE5ReleaseStreamObjectVersion.Type.MaterialInterfaceSavedCachedData && Ar.ReadBoolean();
            if (bSavedCachedExpressionData)
            {
                MaterialCachedExpressionData = new FStructFallback(Ar, "MaterialCachedExpressionData");
            }

            if (Ar.Game == EGame.GAME_HogwartsLegacy) Ar.Position +=20; // FSHAHash
        }

        protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
        {
            base.WriteJson(writer, serializer);

            if (LoadedMaterialResources is not null)
            {
                writer.WritePropertyName("LoadedMaterialResources");
                serializer.Serialize(writer, LoadedMaterialResources);
            }

            if (CachedExpressionData is not null)
            {
                writer.WritePropertyName("CachedExpressionData");
                serializer.Serialize(writer, CachedExpressionData);
            }

        }

        public override void GetParams(CMaterialParams parameters)
        {
            if (FlattenedTexture != null) parameters.Diffuse = FlattenedTexture;
            if (MobileBaseTexture != null) parameters.Diffuse = MobileBaseTexture;
            if (MobileNormalTexture != null) parameters.Normal = MobileNormalTexture;
            if (MobileMaskTexture != null) parameters.Opacity = MobileMaskTexture;
            parameters.UseMobileSpecular = bUseMobileSpecular;
            parameters.MobileSpecularPower = MobileSpecularPower;
            parameters.MobileSpecularMask = MobileSpecularMask;
        }

        public override void GetParams(CMaterialParams2 parameters, EMaterialFormat format)
        {
            for (int i = 0; i < TextureStreamingData.Length; i++)
            {
                var name = TextureStreamingData[i].TextureName.Text;
                if (!parameters.TryGetTexture2d(out var texture, name))
                    continue;

                parameters.VerifyTexture(name, texture, false);
            }

            ProcessMaterialParameters(parameters, MaterialCachedExpressionData);
            if (CachedExpressionData is not null && CachedExpressionData.TryGetValue(out FStructFallback materialParameters, "Parameters"))
                ProcessMaterialParameters(parameters, materialParameters);
        }

        private void ProcessMaterialParameters(CMaterialParams2 parameters, FStructFallback? materialParameters) {
            if (materialParameters == null ||
                !materialParameters.TryGetAllValues(out FStructFallback[] runtimeEntries, "RuntimeEntries"))
                return;

            if (materialParameters.TryGetValue(out float[] scalarValues, "ScalarValues") &&
                runtimeEntries[0].TryGetValue(out FMaterialParameterInfo[] scalarParameterInfos, "ParameterInfos", "ParameterInfoSet"))
                for (int i = 0; i < scalarParameterInfos.Length; i++)
                    parameters.Scalars[scalarParameterInfos[i].Name.Text] = scalarValues[i];

            if (materialParameters.TryGetValue(out FLinearColor[] vectorValues, "VectorValues") &&
                runtimeEntries[1].TryGetValue(out FMaterialParameterInfo[] vectorParameterInfos, "ParameterInfos", "ParameterInfoSet"))
                for (int i = 0; i < vectorParameterInfos.Length; i++)
                    parameters.Colors[vectorParameterInfos[i].Name.Text] = vectorValues[i];

            if (materialParameters.TryGetValue(out object[] textureValues, "TextureValues") &&
                runtimeEntries[Owner?.Provider?.Versions.Game > EGame.GAME_UE5_0 ? 3 : 2].TryGetValue(out FMaterialParameterInfo[] textureParameterInfos, "ParameterInfos", "ParameterInfoSet")) {
                for (int i = 0; i < textureParameterInfos.Length; i++) {
                    var name = textureParameterInfos[i].Name.Text;
                    var textureRef = textureValues[i];
                    UTexture? texture = default;
                    switch (textureRef) {
                        case FPackageIndex packageIndex when !packageIndex.TryLoad(out texture):
                        case FSoftObjectPath softObjectPath when !softObjectPath.TryLoad(out texture):
                            continue;
                        default:
                            parameters.VerifyTexture(name, texture);
                            break;
                    }
                }
            }
        }

        public void DeserializeInlineShaderMaps(FArchive Ar, ICollection<FMaterialResource> loadedResources)
        {
            var numLoadedResources = Ar.Read<int>();
            if (numLoadedResources > 0)
            {
                var resourceAr = new FMaterialResourceProxyReader(Ar);
                if (!Globals.ReadShaderMaps) {
                    Ar.Position += resourceAr.NumBytes;
                    return;
                }

                for (var resourceIndex = 0; resourceIndex < numLoadedResources; ++resourceIndex)
                {
                    var loadedResource = new FMaterialResource();
                    loadedResource.DeserializeInlineShaderMap(resourceAr);
                    loadedResources.Add(loadedResource);
                }
            }
        }
    }
}
