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
    public static (List<WorldActor> Actors, List<(int ActorId, int MaterialIndex, string MaterialName)>
        OverrideMaterials, List<WorldLandscape> Landscapes) ConvertWorld(UWorld world, ETexturePlatform platform) {
        var actors = new List<WorldActor>();
        var overrideMaterials = new List<(int, int, string)>();
        var landscapes = new List<WorldLandscape>();

        if (!world.PersistentLevel.TryLoad<ULevel>(out var level)) {
            return (actors, overrideMaterials, landscapes);
        }

        var actorIds = new List<string>();

        foreach (var actorIndex in level.Actors) {
            if (actorIndex == null || actorIndex.IsNull) {
                continue;
            }

            var actorObject = actorIndex.Load();
            if (actorObject == null) {
                continue;
            }

            var name = actorObject.Name.SubstringAfterLast('/');

            if (actorObject.ExportType == "Landscape") {
                var root = actorObject.GetOrDefault<UObject>("RootComponent");
                var actorId = CreateActor(root, name, actorIds, actors, overrideMaterials);
                CreateLandscape(actorId, actorObject, landscapes, platform);
                continue;
            }

            var component = actorObject?.GetOrDefault<UStaticMeshComponent>("StaticMeshComponent");
            if (component != null) {
                CreateActor(component!, name, actorIds, actors, overrideMaterials);
                continue;
            }

            if (actorObject!.Class is UBlueprintGeneratedClass) {
                foreach (var property in actorObject.Properties) {
                    if (property.Tag is ObjectProperty objectProperty &&
                        objectProperty.Value.ResolvedObject?.Class?.Name.Text.EndsWith("Component") == true) {
                        CreateActor(objectProperty.Value.ResolvedObject.Load()!, name + "_" + property.Name.Text, actorIds, actors, overrideMaterials, true);
                    }
                }
            }
        }

        return (actors, overrideMaterials, landscapes);
    }

    private static void CreateLandscape(int actorId, UObject actorObject, List<WorldLandscape> landscapes,
                                        ETexturePlatform platform) {
        var landscapeComponents = actorObject.GetOrDefault("LandscapeComponents", Array.Empty<UObject>());

        foreach (var landscapeComponent in landscapeComponents) {
            if (landscapeComponent == null) {
                continue;
            }

            // TODO: Create ULandscapeComponent
            var size = landscapeComponent.GetOrDefault("ComponentSizeQuads", 8);

            var x = landscapeComponent.GetOrDefault<int>("SectionBaseX");
            var y = landscapeComponent.GetOrDefault<int>("SectionBaseY");

            var heightScale = landscapeComponent.GetOrDefault("HeightmapScaleBias", new FVector4(1, 1, 1, 1));
            var weightScale = landscapeComponent.GetOrDefault("WeightmapScaleBias", new FVector4(1, 1, 1, 1));

            var heightmap = landscapeComponent.GetOrDefault<FPackageIndex>("HeightmapTexture");
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

            var weightmaps = landscapeComponent.GetOrDefault("WeightmapTextures", Array.Empty<FPackageIndex>());
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
                                   List<(int, int, string)> overrideMaterials, bool strict = false) {
        if (component == null) {
            return -1;
        }

        name ??= "None";

        // TODO: Create USceneComponent, USkeletalMeshComponent, and UStaticMeshComponent
        // TODO: Support UDirectionalLightComponent
        var componentName = component!.GetFullName();
        if (actorIds.Contains(componentName)) {
            return actorIds.IndexOf(componentName);
        }

        var parent = component.GetOrDefault<FPackageIndex>("AttachParent");
        var meshIndex = component.GetOrDefault<FPackageIndex>("StaticMesh");
        var mesh = "None";
        // if (meshIndex == null || meshIndex.IsNull)
        // {
        //     meshIndex = component.GetOrDefault<FPackageIndex>("SkeletalMesh");
        // }

        if (strict && meshIndex == null) {
            return -1;
        }

        if (meshIndex != null && !meshIndex.IsNull) {
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

        if (parent != null && !parent.IsNull) {
            actor.Parent = actorIds.IndexOf(parent!.ResolvedObject?.GetFullName()!);

            if (actor.Parent == -1) {
                actor.Parent = CreateActor(parent?.ResolvedObject?.Load(), parent?.ResolvedObject?.Name.Text, actorIds,
                                           actors, overrideMaterials);
            }

            if (actor.Parent == -1) {
                return -1;
            }
        }

        if (component.GetOrDefault<bool>("bHidden")) {
            actor.Flags |= WorldActorFlags.Hidden;
        }

        if (!component.GetOrDefault("bVisible", true)) {
            actor.Flags |= WorldActorFlags.Hidden;
        }

        if (!component.GetOrDefault("CastShadow", true)) {
            actor.Flags |= WorldActorFlags.NoCastShadow;
        }

        actor.Position = component.GetOrDefault("RelativeLocation", FVector.ZeroVector);
        actor.Position.Y *= -1;
        actor.Rotation = component.GetOrDefault("RelativeRotation", FRotator.ZeroRotator).Quaternion();
        actor.Rotation.Y *= -1;
        actor.Rotation.W *= -1;
        actor.Scale = component.GetOrDefault("RelativeScale3D", FVector.OneVector);

        var actorId = actors.Count;
        var actorMaterials = component.GetOrDefault<List<FPackageIndex>>("OverrideMaterials");
        if (actorMaterials != null) {
            for (var materialIndex = 0; materialIndex < actorMaterials.Count; materialIndex++) {
                var actorMaterialIndex = actorMaterials[materialIndex];
                var actorMaterial = actorMaterialIndex?.ResolvedObject?.Load();
                if (actorMaterial == null) {
                    continue;
                }

                overrideMaterials.Add((actorId, materialIndex,
                                          (actorMaterial.Owner?.Name ?? actor.Name).SubstringAfterLast('/')));
            }
        }

        actors.Add(actor);
        actorIds.Add(componentName);

        return actorId;
    }
}
