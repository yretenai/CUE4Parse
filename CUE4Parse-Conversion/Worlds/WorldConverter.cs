using System;
using System.Collections.Generic;
using CUE4Parse_Conversion.Textures;
using CUE4Parse_Conversion.Worlds.PSW;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.Utils;

namespace CUE4Parse_Conversion.Worlds;

public static class WorldConverter {
    public static (List<WorldActor> Actors, List<WorldLight> Lights, List<WorldLandscape> Landscapes) ConvertWorld(UWorld world, ETexturePlatform platform) {
        var actors = new List<WorldActor>();
        var lights = new List<WorldLight>();
        var landscapes = new List<WorldLandscape>();

        if (!world.PersistentLevel.TryLoad<ULevel>(out var level)) {
            return (actors, lights, landscapes);
        }

        var actorIds = new List<string>();

        foreach (var actorIndex in level.Actors) {
            if (actorIndex.IsNull) {
                continue;
            }

            var actorObject = actorIndex.Load();
            if (actorObject == null) {
                continue;
            }

            var name = actorObject.Name.SubstringAfterLast('/');

            var root = actorObject.TemplatedGetOrDefault<UObject?>("RootComponent");
            if (actorObject.ExportType is "Landscape" or "LandscapeStreamingProxy" or "LandscapeProxy") {
                var actorId = CreateActor(root, name, actorIds, actors, lights);
                CreateLandscape(actorId, actorObject, landscapes, platform);
                continue;
            }

            var staticComponent = actorObject.TemplatedGetOrDefault<UStaticMeshComponent?>("StaticMeshComponent");
            if (staticComponent != null) {
                CreateActor(staticComponent, name, actorIds, actors, lights);
            }

            var skelComponent = actorObject.TemplatedGetOrDefault<USkeletalMeshComponent?>("SkeletalMeshComponent");
            if (skelComponent != null) {
                CreateActor(skelComponent, name, actorIds, actors, lights);
            }

            if (staticComponent is null && skelComponent is null && root is not null) {
                CreateActor(root, name, actorIds, actors, lights);
            }

            var handled = new HashSet<string>();
            UObject? targetObj = actorObject;
            while (targetObj != null) {
                foreach (var property in targetObj.Properties) {
                    if (property.Tag is not ObjectProperty objectProperty) {
                        continue;
                    }

                    var componentObject = objectProperty.Value?.ResolvedObject;

                    var componentName = componentObject?.Class?.Name.Text;
                    if (componentName == null || !handled.Add(componentName)) {
                        continue;
                    }

                    if (componentName.EndsWith("Component")) {
                        CreateActor(componentObject!.Load(), name + "_" + property.Name.Text, actorIds, actors, lights, true);
                    }
                }

                targetObj = targetObj.Template?.Object?.Value;
            }
        }

        return (actors, lights, landscapes);
    }

    private static void CreateLandscape(int actorId, UObject actorObject, List<WorldLandscape> landscapes, ETexturePlatform platform) {
        var landscapeComponents = actorObject.TemplatedGetOrDefault("LandscapeComponents", Array.Empty<UObject?>());
        var landscapeSectionOffset = actorObject.TemplatedGetOrDefault<FIntPoint>("LandscapeSectionOffset");
        foreach (var landscapeComponent in landscapeComponents) {
            if (landscapeComponent == null) {
                continue;
            }

            var size = landscapeComponent.TemplatedGetOrDefault("ComponentSizeQuads", 8);
            var x = landscapeComponent.TemplatedGetOrDefault<int>("SectionBaseX") - (int) landscapeSectionOffset.X;
            var y = landscapeComponent.TemplatedGetOrDefault<int>("SectionBaseY") - (int) landscapeSectionOffset.Y;

            var heightScale = landscapeComponent.TemplatedGetOrDefault("HeightmapScaleBias", new FVector4(1, 1, 1, 1));
            var weightScale = landscapeComponent.TemplatedGetOrDefault("WeightmapScaleBias", new FVector4(1, 1, 1, 1));

            var heightmap = landscapeComponent.TemplatedGetOrDefault<FPackageIndex?>("HeightmapTexture");
            if (heightmap == null || heightmap.IsNull) {
                continue;
            }

            var outerMost = heightmap.ResolvedObject;
            if (outerMost == null) {
                continue;
            }

            while (outerMost.Outer != null) {
                outerMost = outerMost.Outer;
            }

            var landscapeId = landscapes.Count;

            var estDim = size + 1;
            landscapes.Add(new WorldLandscape {
                ActorId = actorId,
                TileX = x,
                TileY = y,
                Type = 0,
                DimX = 1,
                DimY = 1,
                OrigX = estDim,
                OrigY = estDim,
                ComponentSize = size,
                Scale = heightScale,
                Path = outerMost.Name.Text + $".{heightmap.ResolvedObject!.ExportIndex}",
            });

            var heightTex = heightmap.Load<UTexture2D>();
            if (heightTex != null) {
                landscapes[landscapeId].DimX = heightTex.PlatformData.SizeX / estDim;
                landscapes[landscapeId].DimY = heightTex.PlatformData.SizeY / estDim;
                landscapes[landscapeId].OrigX = heightTex.PlatformData.SizeX;
                landscapes[landscapeId].OrigY = heightTex.PlatformData.SizeY;

                using var skHeightData = heightTex.Decode(platform);
                if (skHeightData == null) {
                    continue;
                }

                var pixels = skHeightData.Pixels;
                var heightData = new float[pixels.Length];
                for (var i = 0; i < pixels.Length; i++) {
                    var pixel = pixels[i];
                    var rgb = (uint) pixel;

                    heightData[i] = Math.Clamp((rgb & 0xFFFFFF) / 16777216f, 0f, 1f);
                }

                landscapes[landscapeId].Heightmap = heightData;
                landscapes[landscapeId].Path += "_HEIGHT";
            }

            var weightmaps = landscapeComponent.TemplatedGetOrDefault("WeightmapTextures", Array.Empty<FPackageIndex?>());
            for (var index = 0; index < weightmaps.Length; index++) {
                var weightmap = weightmaps[index];
                if (weightmap == null || weightmap.IsNull) {
                    continue;
                }

                outerMost = weightmap.ResolvedObject;
                if (outerMost == null) {
                    continue;
                }

                while (outerMost.Outer != null) {
                    outerMost = outerMost.Outer;
                }

                landscapes.Add(new WorldLandscape {
                    ActorId = actorId,
                    TileX = x,
                    TileY = y,
                    Type = index + 1,
                    ComponentSize = size,
                    Scale = weightScale,
                    Path = outerMost.Name.Text + $".{weightmap.ResolvedObject!.ExportIndex}",
                });
            }
        }
    }

    private static int CreateActor(UObject? component, string? name, List<string> actorIds, List<WorldActor> actors, List<WorldLight> lights, bool strict = false) {
        if (component == null) {
            return -1;
        }

        name ??= "None";

        var componentName = component.GetFullName();
        if (actorIds.Contains(componentName)) {
            return actorIds.IndexOf(componentName);
        }

        var parent = component.TemplatedGetOrDefault<FPackageIndex?>("AttachParent");
        var meshIndex = component.TemplatedGetOrDefault<FPackageIndex?>("StaticMesh");
        var mesh = "None";
        var isSkeleton = component is USkeletalMeshComponent;
        if (meshIndex == null || meshIndex.IsNull) {
            meshIndex = component.TemplatedGetOrDefault<FPackageIndex?>("SkeletalMesh") ?? component.TemplatedGetOrDefault<FPackageIndex?>("SkinnedAsset");
            isSkeleton = meshIndex != null;
        }

        var isLight = component.ExportType.EndsWith("LightComponent");
        if (strict && meshIndex == null && !isLight) {
            return -1;
        }

        var materials = new List<(string Name, string Path)>();
        if (meshIndex is { IsNull: false }) {
            var outerMost = meshIndex.ResolvedObject;
            if (outerMost != null) {
                while (outerMost.Outer != null) {
                    outerMost = outerMost.Outer;
                }

                mesh = outerMost.Name.Text + $".{meshIndex.ResolvedObject!.ExportIndex}";
            }

            if (meshIndex.TryLoad(out var meshObj)) {
                var meshMaterials = default(ResolvedObject?[]);
                if (meshObj is UStaticMesh staticMesh) {
                    meshMaterials = staticMesh.Materials;
                } else if (meshObj is USkeletalMesh skeletalMesh) {
                    meshMaterials = skeletalMesh.Materials;
                }

                foreach (var material in meshMaterials ?? []) {
                    var outerMostMaterial = material;
                    if (outerMostMaterial != null) {
                        while (outerMostMaterial.Outer != null) {
                            outerMostMaterial = outerMostMaterial.Outer;
                        }

                        materials.Add((material!.Name.Text, outerMostMaterial.Name.Text + $".{material!.ExportIndex}"));
                    }
                }

                var actorMaterials = component.TemplatedGetOrDefault("OverrideMaterials", Array.Empty<FPackageIndex?>());
                for (var materialIndex = 0; materialIndex < actorMaterials.Length; materialIndex++) {
                    var actorMaterialIndex = actorMaterials[materialIndex];

                    var outerMostMaterial = actorMaterialIndex?.ResolvedObject;
                    if (outerMostMaterial != null) {
                        while (outerMostMaterial.Outer != null) {
                            outerMostMaterial = outerMostMaterial.Outer;
                        }

                        materials[materialIndex] = (actorMaterialIndex!.Name, outerMostMaterial.Name.Text + $".{actorMaterialIndex!.ResolvedObject!.ExportIndex}");
                    }
                }
            }
        }

        var actor = new WorldActor {
            Name = name,
            Parent = -1,
            AssetPath = mesh,
            Materials = materials,
        };

        if (parent is { IsNull: false }) {
            actor.Parent = actorIds.IndexOf(parent.ResolvedObject?.GetFullName()!);

            if (actor.Parent == -1) {
                actor.Parent = CreateActor(parent.ResolvedObject?.Load(), parent.ResolvedObject?.Name.Text, actorIds, actors, lights);
            }

            if (actor.Parent == -1) {
                return -1;
            }
        }

        if (component.TemplatedGetOrDefault<bool>("bHidden")) {
            actor.Flags |= WorldActorFlags.Hidden;
        }

        if (!component.TemplatedGetOrDefault("bVisible", true)) {
            actor.Flags |= WorldActorFlags.Hidden;
        }

        if (!component.TemplatedGetOrDefault("CastShadow", true)) {
            actor.Flags |= WorldActorFlags.NoCastShadow;
        }

        if (component.TemplatedGetOrDefault("bUseTemperature", false)) {
            actor.Flags |= WorldActorFlags.UseTempature;
        }

        if (isSkeleton) {
            actor.Flags |= WorldActorFlags.IsSkeleton;
        }

        actor.Position = component.TemplatedGetOrDefault("RelativeLocation", FVector.ZeroVector);
        actor.Position.Y *= -1;
        actor.Rotation = component.TemplatedGetOrDefault("RelativeRotation", FRotator.ZeroRotator).Quaternion();
        actor.Rotation.Y *= -1;
        actor.Rotation.W *= -1;
        actor.Scale = component.TemplatedGetOrDefault("RelativeScale3D", FVector.OneVector);

        var actorId = actors.Count;
        if (isLight) {
            var light = new WorldLight {
                Parent = actorId,
                Color = component.TemplatedGetOrDefault("LightColor", new FColor(255, 255, 255, 255)),
                Width = component.TemplatedGetOrDefault("Intensity", 1.0f),
                Height = component.TemplatedGetOrDefault("Intensity", 1.0f),
                Length = component.TemplatedGetOrDefault("SourceLength", 1.0f),
                AttenuationRadius = component.TemplatedGetOrDefault("AttenuationRadius", 1.0f),
                Radius = component.TemplatedGetOrDefault("SourceRadius", 1.0f),
                Temperature = component.TemplatedGetOrDefault("Temperature", 7000.0f),
                ShadowBias = component.TemplatedGetOrDefault("ShadowBias", 0.5f),
                Intensity = component.TemplatedGetOrDefault("Intensity", 1.0f),
                LightSourceAngle = component.TemplatedGetOrDefault("LightSourceAngle", 2.0f)
            };

            if (component.ExportType.EndsWith("RectLightComponent")) {
                light.Type = WorldLightType.Rect;
            } else if (component.ExportType.EndsWith("PointLightComponent")) {
                light.Type = WorldLightType.Point;
            } else if (component.ExportType.EndsWith("SpotLightComponent")) {
                light.Type = WorldLightType.Spot;
            } else if (component.ExportType.EndsWith("DirectionalLightComponent")) {
                light.Type = WorldLightType.Directional;
            }

            var units = component.TemplatedGetOrDefault<FName>("IntensityUnits", "ELightUnits::Candelas");
            if (units != "ELightUnits::Lumen") {
                light.Intensity *= 12.57f;
            }

            lights.Add(light);
        }

        actors.Add(actor);
        actorIds.Add(componentName);

        return actorId;
    }
}
