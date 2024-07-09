using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.UObject;

namespace AssetDumper;

public static class BlueprintConstructor {
    public static FStructFallback GetMergedStruct(UObject blueprint) {
        var fallback = new FStructFallback();
        while (true) {
            MergeStruct(fallback, blueprint);

            if (blueprint.Class != null) {
                if (blueprint.Class.TryGetValue(out UObject simpleConstructionScript, "SimpleConstructionScript")) {
                    var records = simpleConstructionScript.GetOrDefault("AllNodes", Array.Empty<UObject>());
                    foreach (var record in records) {
                        var componentTemplate = record.Get<FPackageIndex>("ComponentTemplate");
                        var scsVariableName = record.Get<string>("InternalVariableName");
                        var localProperty = fallback.Properties.FirstOrDefault(x => x.Name.Text == scsVariableName);
                        if (localProperty == null) {
                            fallback.Properties.Add(new FPropertyTag(scsVariableName, new ObjectProperty(componentTemplate)));
                        }
                    }
                }

                if (blueprint.Class.TryGetValue(out UObject inheritableComponentHandler, "InheritableComponentHandler")) {
                    var records = inheritableComponentHandler.GetOrDefault("Records", Array.Empty<FStructFallback>());
                    foreach (var record in records) {
                        var componentTemplate = record.Get<FPackageIndex>("ComponentTemplate");
                        var componentKey = record.Get<FStructFallback>("ComponentKey");
                        var scsVariableName = componentKey.Get<string>("SCSVariableName");
                        var localProperty = fallback.Properties.FirstOrDefault(x => x.Name.Text == scsVariableName);
                        if (localProperty == null) {
                            fallback.Properties.Add(new FPropertyTag(scsVariableName, new ObjectProperty(componentTemplate)));
                        }
                    }
                }
            }

            var templateBlueprint = blueprint.Template?.Load();
            if (templateBlueprint == null) {
                break;
            }

            blueprint = templateBlueprint;
        }

        return fallback;
    }

    public static void MergeStruct(this FStructFallback fallback, IPropertyHolder mergee) {
        foreach (var property in mergee.Properties) {
            var localProperty = fallback.Properties.FirstOrDefault(x => x.Name.Text == property.Name.Text);

            if (localProperty == null) {
                fallback.Properties.Add(property);
                continue;
            }

            if (property.Tag is not StructProperty) {
                continue;
            }

            if (localProperty.Tag is not StructProperty) {
                continue;
            }

            if (property.Tag is StructProperty { Value.StructType: FStructFallback substruct } && localProperty.Tag is StructProperty { Value.StructType: FStructFallback localSubstruct }) {
                MergeStruct(localSubstruct, substruct);
            }
        }
    }
}
