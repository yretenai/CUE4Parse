using System;
using System.Linq;
using CUE4Parse.UE4.Assets.Exports;

namespace CUE4Parse_Conversion;

public static class StructHelper {
    public static T TemplatedGetOrDefault<T>(this IPropertyHolder holder, string name, T defaultValue = default, StringComparison comparisonType = StringComparison.Ordinal) {
        var tag = holder.Properties.FirstOrDefault(it => it.Name.Text.Equals(name, comparisonType))?.Tag;
        if (tag == null) {
            if (holder is UObject { Template.Object: not null } obj) {
                return obj.Template.Object.Value.TemplatedGetOrDefault(name, defaultValue, comparisonType);
            }

            return defaultValue;
        }
        
        var value = tag.GetValue(typeof(T));
        if (value is T cast) {
            return cast;
        }

        return defaultValue;
    }
}