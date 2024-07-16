using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.Texture;
using Newtonsoft.Json;

namespace CUE4Parse_Conversion.Materials
{
    public struct MaterialData
    {
        public Dictionary<string, string> Textures;
        public CMaterialParams2 Parameters;
    }

    public class MaterialExporter2 : ExporterBase
    {
        private readonly string _internalFilePath;
        private readonly MaterialData _materialData;

        public MaterialExporter2(ExporterOptions options, string? suffix = null)
        {
            Options = options;
            _internalFilePath = string.Empty;
            Suffix = suffix ?? string.Empty;
            _materialData = new MaterialData
            {
                Textures = new Dictionary<string, string>(),
                Parameters = new CMaterialParams2()
            };
        }

        public MaterialExporter2(UUnrealMaterial? unrealMaterial, ExporterOptions options, string? suffix = null) : this(options, suffix)
        {
            if (unrealMaterial == null) return;
            _internalFilePath = unrealMaterial.Owner?.Name ?? unrealMaterial.Name;

            unrealMaterial.GetParams(_materialData.Parameters, Options.MaterialFormat);
            foreach ((string key, UUnrealMaterial value) in _materialData.Parameters.Textures)
            {
                _materialData.Textures[key] = value.GetPathName();
            }
        }

        private readonly object _texture = new ();
        public override bool TryWriteToDir(DirectoryInfo baseDirectory, out string label, out string savedFilePath)
        {
            label = string.Empty;
            savedFilePath = string.Empty;
            if (!baseDirectory.Exists) return false;

            savedFilePath = FixAndCreatePath(baseDirectory, _internalFilePath + Suffix, "json");
            File.WriteAllText(savedFilePath, JsonConvert.SerializeObject(_materialData, Formatting.Indented));
            label = Path.GetFileName(savedFilePath);

            if (!Options.ExportTextures) return true;

            Parallel.ForEach(_materialData.Parameters.Textures.Values, texture =>
            {
                if (texture is not UTexture2D t) return;

                using var bitmap = t.Decode(Options.Platform);

                if (bitmap == null) return;

                lock (_texture)
                {
                    var ext = Options.TextureFormat switch
                    {
                        ETextureFormat.Png => "png",
                        ETextureFormat.Tga => "tga",
                        ETextureFormat.Dds => "dds",
                        _ => "png"
                    };
                    
                    var texturePath = FixAndCreatePath(baseDirectory, t.Owner?.Name ?? t.Name, ext);
                    using var fs = new FileStream(texturePath, FileMode.Create, FileAccess.Write);
                    using var data = bitmap.Encode(Options.TextureFormat, 100);
                    using var stream = data.AsStream();
                    stream.CopyTo(fs);
                }
            });

            return true;
        }

        public override bool TryWriteToZip(out byte[] zipFile)
        {
            throw new System.NotImplementedException();
        }

        public override void AppendToZip()
        {
            throw new System.NotImplementedException();
        }
    }
}
