using System;
using System.Collections.Generic;
using System.IO;

namespace CUE4Parse.MappingsProvider
{
    public class TypeMappings
    {
        public readonly Dictionary<string, Struct> Types;
        public readonly Dictionary<string, List<(long, string)>> Enums;

        public TypeMappings()
        {
            Types = new Dictionary<string, Struct>();
            Enums = new Dictionary<string, List<(long, string)>>();
        }

        public string PropertyToType(PropertyType? prop)
        {
            if (prop == null)
            {
                return "void";
            }

            return prop.Type switch
            {
                "ArrayProperty" => $"List<{PropertyToType(prop.InnerType)}>",
                "BoolProperty" => "bool",
                "ByteProperty" => "byte",
                "ClassProperty" => "class",
                "DoubleProperty" => "double",
                "EnumProperty" => (prop.EnumName ?? "enum") + $"<{PropertyToType(prop.InnerType)}>",
                "FloatProperty" => "float",
                "Int16Property" => "short",
                "Int64Property" => "long",
                "Int8Property" => "sbyte",
                "IntProperty" => "int",
                "MapProperty" => $"Dictionary<{PropertyToType(prop.InnerType)}, {PropertyToType(prop.ValueType)}>",
                "NameProperty" => "name",
                "ObjectProperty" => "object",
                "SetProperty" => $"HashSet<{PropertyToType(prop.InnerType)}>",
                "StrProperty" => "string",
                "StructProperty" => prop.StructType ?? "struct",
                "UInt16Property" => "ushort",
                "UInt32Property" => "uint",
                "UInt64Property" => "ulong",
                _ => prop.Type
            };
        }

        public void DumpDummyClasses(TextWriter writer)
        {
            writer.WriteLine($"ENUMS {Enums.Count}");
            foreach (var (name, values) in Enums)
            {
                writer.WriteLine($"\tenum {name} {{");
                foreach (var (value, enumName) in values)
                {
                    writer.WriteLine($"\t\t{enumName} = {value}");
                }
                writer.WriteLine("\t}");
            }

            writer.WriteLine($"STRUCTS {Types.Count}");
            foreach (var (name, s) in Types)
            {
                writer.WriteLine($"\tpublic class {name} : {s.SuperType ?? "UObject"} {{");
                foreach (var (_, prop) in s.Properties)
                {
                    writer.Write($"\t\tpublic {PropertyToType(prop.MappingType)} {prop.Name}");

                    if (prop.ArraySize > 1)
                    {
                        writer.Write("[]");
                    }

                    writer.WriteLine($" {{ get; set; }}; // ArraySize: {prop.ArraySize - 1}, Index: {prop.Index}");
                }

                writer.WriteLine("\t}");
            }
        }
    }
}
