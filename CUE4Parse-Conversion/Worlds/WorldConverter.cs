using System;
using System.Collections.Generic;
using CUE4Parse_Conversion.Textures;
using CUE4Parse_Conversion.Worlds.PSW;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.Utils;

namespace CUE4Parse_Conversion.Worlds;

public static class WorldConverter {
    public static (List<WorldActor> Actors, List<WorldLight> Lights, List<(int ActorId, int MaterialIndex, string MaterialName)>
        OverrideMaterials, List<WorldLandscape> Landscapes) ConvertWorld(UWorld world, ETexturePlatform platform) {
        var actors = new List<WorldActor>();
        var lights = new List<WorldLight>();
        var overrideMaterials = new List<(int, int, string)>();
        var landscapes = new List<WorldLandscape>();

        if (!world.PersistentLevel.TryLoad<ULevel>(out var level)) {
            return (actors, lights, overrideMaterials, landscapes);
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

            if (actorObject.ExportType == "Landscape") {
                var root = actorObject.TemplatedGetOrDefault<UObject?>("RootComponent");
                var actorId = CreateActor(root, name, actorIds, actors, lights, overrideMaterials);
                CreateLandscape(actorId, actorObject, landscapes, platform);
                continue;
            }

            var component = actorObject.TemplatedGetOrDefault<UStaticMeshComponent?>("StaticMeshComponent");
            if (component != null) {
                CreateActor(component, name, actorIds, actors, lights, overrideMaterials);
                continue;
            }
            
            var handled = new HashSet<string>(); 
            UObject? targetObj = actorObject;
            while(targetObj != null) {
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
                        CreateActor(componentObject!.Load(), name + "_" + property.Name.Text, actorIds, actors, lights, overrideMaterials, true);
                    }
                }

                targetObj = targetObj.Template?.Object?.Value;
            }
        }

        return (actors, lights, overrideMaterials, landscapes);
    }

    private static void CreateLandscape(int actorId, UObject actorObject, List<WorldLandscape> landscapes, ETexturePlatform platform) {
        var landscapeComponents = actorObject.TemplatedGetOrDefault("LandscapeComponents", Array.Empty<UObject?>());

        foreach (var landscapeComponent in landscapeComponents) {
            if (landscapeComponent == null) {
                continue;
            }

            var size = landscapeComponent.TemplatedGetOrDefault("ComponentSizeQuads", 8);

            var x = landscapeComponent.TemplatedGetOrDefault<int>("SectionBaseX");
            var y = landscapeComponent.TemplatedGetOrDefault<int>("SectionBaseY");

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
                if (skHeightData == null)
                {
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

    private static int CreateActor(UObject? component, string? name, List<string> actorIds, List<WorldActor> actors,
        List<WorldLight> lights, List<(int, int, string)> overrideMaterials, bool strict = false) {
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
        if (meshIndex == null || meshIndex.IsNull) {
             meshIndex = component.TemplatedGetOrDefault<FPackageIndex?>("SkeletalMesh");
        }

        var isLight = component.ExportType.EndsWith("LightComponent");
        if (strict && meshIndex == null && !isLight) {
            return -1;
        }

        if (meshIndex is { IsNull: false }) {
            var outerMost = meshIndex.ResolvedObject;
            if (outerMost != null) {
                while (outerMost.Outer != null) {
                    outerMost = outerMost.Outer;
                }

                mesh = outerMost.Name.Text + $".{meshIndex.ResolvedObject!.ExportIndex}";
            }
        }

        var actor = new WorldActor {
            Name = name,
            Parent = -1,
            AssetPath = mesh,
        };

        if (parent is { IsNull: false }) {
            actor.Parent = actorIds.IndexOf(parent.ResolvedObject?.GetFullName()!);

            if (actor.Parent == -1) {
                actor.Parent = CreateActor(parent.ResolvedObject?.Load(), parent.ResolvedObject?.Name.Text, actorIds, actors, lights, overrideMaterials);
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

        var actorMaterials = component.TemplatedGetOrDefault("OverrideMaterials", Array.Empty<FPackageIndex?>());
        for (var materialIndex = 0; materialIndex < actorMaterials.Length; materialIndex++) {
            var actorMaterialIndex = actorMaterials[materialIndex];
            var actorMaterial = actorMaterialIndex?.ResolvedObject?.Load();
            if (actorMaterial == null) {
                continue;
            }

            overrideMaterials.Add((actorId, materialIndex,
                                      (actorMaterial.Owner?.Name ?? actor.Name).SubstringAfterLast('/')));
        }

        actors.Add(actor);
        actorIds.Add(componentName);

        return actorId;
    }
}
