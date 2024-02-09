using System;
using System.Collections.Generic;
using System.IO;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Writers;
using CUE4Parse.Utils;
using CUE4Parse_Conversion.ActorX;
using CUE4Parse_Conversion.Animations.PSA;
using CUE4Parse_Conversion.Animations.UEFormat;
using CUE4Parse.UE4.Assets.Exports;

namespace CUE4Parse_Conversion.Animations
{
    public class AnimExporter : ExporterBase
    {
        public readonly List<Anim> AnimSequences;

        private AnimExporter(UObject export, ExporterOptions options, string? suffix = null) : base(export, options, suffix)
        {
            AnimSequences = new List<Anim>();
        }

        private AnimExporter(ExporterOptions options, UObject export, CAnimSet animSet, string? suffix = null)
            : this(export, options, suffix)
        {
            for (var sequenceIndex = 0; sequenceIndex < animSet.Sequences.Count; sequenceIndex++)
            {
                using var Ar = new FArchiveWriter();
                string ext;
                switch (Options.AnimFormat)
                {
                    case EAnimFormat.ActorX:
                        ext = "psa";
                        new ActorXAnim(animSet, sequenceIndex, Options).Save(Ar);
                        break;
                    case EAnimFormat.UEFormat:
                        ext = "ueanim";
                        new UEAnim(export.Name, animSet, sequenceIndex, Options).Save(Ar);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(Options.MeshFormat), Options.MeshFormat, null);
                }

                AnimSequences.Add(sequenceIndex > 0
                    ? new Anim($"{PackagePath}{Suffix}.seq{sequenceIndex}.{ext}", Ar.GetBuffer())
                    : new Anim($"{PackagePath}{Suffix}.{ext}", Ar.GetBuffer()));
            }
        }

        private AnimExporter(ExporterOptions options, USkeleton skeleton, UAnimSequence? animSequence = null, string? suffix = null)
            : this(options, animSequence != null ? animSequence : skeleton, skeleton.ConvertAnims(animSequence), suffix)
        {

        }

        private AnimExporter(ExporterOptions options, USkeleton skeleton, UAnimMontage? animMontage = null, string? suffix = null)
            : this(options, animMontage != null ? animMontage : skeleton, skeleton.ConvertAnims(animMontage), suffix)
        {

        }

        private AnimExporter(ExporterOptions options, USkeleton skeleton, UAnimComposite? animComposite = null, string? suffix = null)
            : this(options, animComposite != null ? animComposite : skeleton, skeleton.ConvertAnims(animComposite), suffix)
        {

        }

        public AnimExporter(UAnimSequence animSequence, ExporterOptions options, string? suffix = null) : this(options, animSequence.Skeleton.Load<USkeleton>()!, animSequence, suffix) { }
        public AnimExporter(UAnimMontage animMontage, ExporterOptions options, string? suffix = null) : this(options, animMontage.Skeleton.Load<USkeleton>()!, animMontage, suffix) { }
        public AnimExporter(UAnimComposite animComposite, ExporterOptions options, string? suffix = null) : this(options, animComposite.Skeleton.Load<USkeleton>()!, animComposite, suffix) { }


        public override bool TryWriteToDir(DirectoryInfo baseDirectory, out string label, out string savedFilePath)
        {
            var b = false;
            label = string.Empty;
            savedFilePath = PackagePath;
            if (AnimSequences.Count == 0) return b;

            var outText = "SEQ ";
            for (var i = 0; i < AnimSequences.Count; i++)
            {
                b |= AnimSequences[i].TryWriteToDir(baseDirectory, out label, out savedFilePath);
                outText += $"{i} ";
            }

            label = outText + $"as '{savedFilePath.SubstringAfterWithLast('.')}' for '{ExportName}'";
            return b;
        }

        public override bool TryWriteToZip(out byte[] zipFile)
        {
            throw new NotImplementedException();
        }

        public override void AppendToZip()
        {
            throw new NotImplementedException();
        }
    }
}
