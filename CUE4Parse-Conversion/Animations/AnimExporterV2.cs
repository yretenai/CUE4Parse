using CUE4Parse_Conversion.ActorX;
using CUE4Parse_Conversion.Animations.PSA;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Writers;

namespace CUE4Parse_Conversion.Animations;

public class AnimExporterV2 : AnimExporter {
    public AnimExporterV2(UAnimSequence animSequence, ExporterOptions options, int exportIndex) : base(animSequence, options, exportIndex) { }


    private static void FixRotationKeys(CAnimSequence anim)
    {
        for (var trackIndex = 0; trackIndex < anim.Tracks.Count; trackIndex++)
        {
            if (trackIndex == 0) continue; // don't fix root track

            var track = anim.Tracks[trackIndex];
            for (var keyQuatIndex = 0; keyQuatIndex < track.KeyQuat.Length; keyQuatIndex++)
            {
                track.KeyQuat[keyQuatIndex].Conjugate();
            }
        }
    }

    protected override void DoExportPsa(CAnimSet anim, int seqIdx) {
        var Ar = new FArchiveWriter();

        var mainHdr = new VChunkHeader();
        var boneHdr = new VChunkHeader();
        var sequenceHdr = new VChunkHeader();

        int i;
        Ar.SerializeChunkHeader(mainHdr, "ANIXHEAD");

        var numBones = anim.Skeleton.BoneCount;
        var numAnims = anim.Sequences.Count;

        boneHdr.DataCount = numBones;
        boneHdr.DataSize = Constants.FNamedBoneBinary_SIZE;
        Ar.SerializeChunkHeader(boneHdr, "BONENAMES");
        for (i = 0; i < numBones; i++) {
            var boneInfo = anim.Skeleton.ReferenceSkeleton.FinalRefBoneInfo[i];
            var boneTransform = anim.Skeleton.ReferenceSkeleton.FinalRefBonePose[i];
            var bone = new FNamedBoneBinary {
                Name = boneInfo.Name.Text,
                Flags = 0, // reserved
                NumChildren = 0, // unknown here
                ParentIndex = boneInfo.ParentIndex, // unknown for UAnimSet?? edit 2023: no
                BonePos = {
                    Orientation = boneTransform.Rotation,
                    Position = boneTransform.Translation,
                    Size = boneTransform.Scale3D,
                    Length = 1.0f,
                },
            };

            bone.Serialize(Ar);
        }

        sequenceHdr.DataCount = numAnims;
        sequenceHdr.DataSize = 72;
        Ar.SerializeChunkHeader(sequenceHdr, "SEQUENCES");
        for (i = 0; i < numAnims; i++) {
            var sequence = anim.Sequences[i];
            Ar.Write(sequence.Name, 64);
            Ar.Write(sequence.FramesPerSecond);
            Ar.Write(sequence.IsAdditive ? 1 : 0);
        }

        for (i = 0; i < numAnims; i++) {
            var sequence = anim.Sequences[i];
            FixRotationKeys(sequence);

            for (var boneIndex = 0; boneIndex < numBones; boneIndex++) {
                var posTrackHdr = new VChunkHeader();
                var sclTrackHdr = new VChunkHeader();
                var rotTrackHdr = new VChunkHeader();
                var track = sequence.Tracks[boneIndex];

                posTrackHdr.DataSize = 16; // float, FVector
                posTrackHdr.DataCount = track.KeyPos.Length;
                Ar.SerializeChunkHeader(posTrackHdr, $"POSTRACK{i}:{boneIndex}");
                for (var j = 0; j < track.KeyPos.Length; ++j) {
                    Ar.Write(track.KeyPosTime.Length == 0 ? j : track.KeyPosTime[j]);
                    var pos = track.KeyPos[j];
                    pos.Y *= -1;
                    pos.Serialize(Ar);
                }

                sclTrackHdr.DataSize = 16; // float, FVector
                sclTrackHdr.DataCount = track.KeyScale.Length;
                Ar.SerializeChunkHeader(sclTrackHdr, $"SCLTRACK{i}:{boneIndex}");
                for (var j = 0; j < track.KeyScale.Length; ++j) {
                    Ar.Write(track.KeyScaleTime.Length == 0 ? j : track.KeyScaleTime[j]);
                    track.KeyScale[j].Serialize(Ar);
                }

                rotTrackHdr.DataSize = 20; // float, FQuat
                rotTrackHdr.DataCount = track.KeyQuat.Length;
                Ar.SerializeChunkHeader(rotTrackHdr, $"ROTTRACK{i}:{boneIndex}");
                for (var j = 0; j < track.KeyQuat.Length; ++j) {
                    Ar.Write(track.KeyQuatTime.Length == 0 ? j : track.KeyQuatTime[j]);
                    var rot = track.KeyQuat[j];
                    rot.Y *= -1;
                    rot.W *= -1;
                    rot.Serialize(Ar);
                }
            }
        }

        AnimSequences.Add(new Anim($"{PackagePath}_SEQ{seqIdx}.psax", Ar.GetBuffer()));
        Ar.Dispose();
    }
}
