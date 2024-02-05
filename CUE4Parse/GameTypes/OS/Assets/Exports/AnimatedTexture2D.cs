using System;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.UObject;
using Newtonsoft.Json;

namespace CUE4Parse.GameTypes.OS.Assets.Exports;

public class AnimatedTexture2D : UTexture
{
    public AnimatedTextureType FileType { get; private set; }
    public float AnimationLength { get; private set; }
    [JsonIgnore]
    public byte[] FileBlob { get; private set; }
    public uint Unknown { get; private set; }

    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);

        Unknown = Ar.Read<uint>();

        FileType = GetOrDefault<FName>(nameof(FileType)).PlainText switch
        {
            "EAnimatedTextureType::Gif" => AnimatedTextureType.Gif,
            "EAnimatedTextureType::Webp" => AnimatedTextureType.Webp,
            _ => AnimatedTextureType.None
        };
        
        AnimationLength = GetOrDefault<float>(nameof(AnimationLength), 1);
        FileBlob = GetOrDefault(nameof(FileBlob), Array.Empty<byte>());
    }
}