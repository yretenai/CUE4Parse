using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.FileProvider.Vfs;
using DragonLib;
using DragonLib.Hash;
using DragonLib.Hash.Algorithms;
using DragonLib.Hash.Basis;

namespace AssetDumper;

public readonly record struct HistoryOptions {
    public bool HashExport { get; init; }
    public bool HashBulk { get; init; }
    public bool HashOptional { get; init; }
    public HistoryChecksumType ChecksumType { get; init; }

    public HistoryFlags Flags => (HashExport ? HistoryFlags.HashExport : 0) |
                                 (HashBulk ? HistoryFlags.HashBulk : 0) |
                                 (HashOptional ? HistoryFlags.HashOptional : 0);

    public static HistoryOptions Default { get; } = new() {
        HashExport = true,
        HashBulk = true,
        HashOptional = true,
        ChecksumType = HistoryChecksumType.CRC32C
    };
}

[Flags]
public enum HistoryFlags {
    HashExport = 1 << 1,
    HashBulk = 1 << 2,
    HashOptional = 1 << 3
};

public enum HistoryChecksumType {
    CRC32C = 0,
    DJB2 = 1,
    DJB2a = 2,
    FNV1 = 3,
    FNV1a = 4
}

public enum HistoryVersion : byte {
    InvalidVersion = 0,
    InitialVersion = 1,
    AddedOptionsAndUptnl = 2,
    
    Latest = AddedOptionsAndUptnl
}

public class History {
    public enum HistoryType {
        Undetermined,
        Same,
        New,
        Updated,
    }
    
    private readonly HashAlgorithm? HashAlgorithm;
    private readonly bool ReadOnly;

    private readonly Dictionary<string, HistoryEntry> Entries = new();

    public HistoryOptions Options { get; }
    
    public History(HistoryOptions options) {
        Options = options;

        HashAlgorithm = options.ChecksumType switch {
            HistoryChecksumType.CRC32C when CRC32CAlgorithm.IsSupported => CRC32CAlgorithm.Create(),
            HistoryChecksumType.CRC32C => CRC.Create(CRC32Variants.Castagnoli),
            HistoryChecksumType.DJB2 => DJB2.Create(),
            HistoryChecksumType.DJB2a => DJB2.CreateAlternate(),
            HistoryChecksumType.FNV1 => FNV.Create(FNV32Basis.FNV1),
            HistoryChecksumType.FNV1a => FNV.CreateInverse(FNV32Basis.FNV1),
            _ => HashAlgorithm
        };

        ReadOnly = false;
    }

    public History(string? path) {
        ReadOnly = true;
        if (!File.Exists(path)) {
            Options = HistoryOptions.Default;
            return;
        }

        using var stream = File.OpenRead(path);
        var header = new HistoryHeader();
        stream.ReadExactly(new Span<HistoryHeader>(ref header).AsBytes());

        var entrySize = Unsafe.SizeOf<HistoryEntryHeader>();
        switch (header.Version) {
            case > HistoryVersion.Latest or <= HistoryVersion.InvalidVersion:
                throw new NotSupportedException();
            case HistoryVersion.InitialVersion:
                entrySize = Unsafe.SizeOf<HistoryEntryHeaderV1>();
                header = header with { ChecksumType = HistoryChecksumType.CRC32C, Flags = HistoryFlags.HashExport | HistoryFlags.HashBulk | HistoryFlags.HashOptional };
                stream.Position = 5;
                break;
        }

        Options = new HistoryOptions {
            HashExport = header.Flags.HasFlag(HistoryFlags.HashExport),
            HashBulk = header.Flags.HasFlag(HistoryFlags.HashBulk),
            HashOptional = header.Flags.HasFlag(HistoryFlags.HashOptional),
            ChecksumType = header.ChecksumType
        };

        Entries.EnsureCapacity(header.Count);
        Span<byte> entryBuffer = stackalloc byte[entrySize];
        for (var index = 0; index < header.Count; index++) {
            stream.ReadExactly(entryBuffer.AsBytes());

            var entryHeader = header.Version switch {
                HistoryVersion.InitialVersion => MemoryMarshal.Read<HistoryEntryHeaderV1>(entryBuffer).Upgrade(),
                _ => MemoryMarshal.Read<HistoryEntryHeader>(entryBuffer)
            };

            var text = new byte[entryHeader.TextLength].AsSpan();
            stream.ReadExactly(text);

            var entry = new HistoryEntry {
                Header = entryHeader,
                Path = Encoding.UTF8.GetString(text),
            };
            Entries[entry.Path] = entry;
        }
    }

    public void Save(string path) {
        using var stream = File.OpenWrite(path);
        var header = new HistoryHeader {
            Version = HistoryVersion.Latest,
            Count = Entries.Count,
            ChecksumType = Options.ChecksumType,
            Flags = Options.Flags
        };
        stream.Write(new Span<HistoryHeader>(ref header).AsBytes());
        foreach (var _entry in Entries.Values) {
            var text = Encoding.UTF8.GetBytes(_entry.Path);
            var entry = _entry.Header with { TextLength = text.Length };
            stream.Write(new Span<HistoryEntryHeader>(ref entry).AsBytes());
            stream.Write(text);
        }
    }

    public async Task<HistoryEntry> Add(AbstractVfsFileProvider provider, GameFile gameFile) {
        if (ReadOnly) {
            return new HistoryEntry();
        }

        var entry = new HistoryEntry {
            Header = new HistoryEntryHeader {
                Size = gameFile.Size,
                Hash = await CalculateHashForFile(gameFile),
                ExportHash = Options.HashExport ? await CalculateHashForFile(provider, gameFile.PathWithoutExtension + ".uexp") : uint.MaxValue,
                BulkHash = Options.HashBulk ? await CalculateHashForFile(provider, gameFile.PathWithoutExtension + ".ubulk") : uint.MaxValue,
                UptnlHash = Options.HashOptional ? await CalculateHashForFile(provider, gameFile.PathWithoutExtension + ".uptnl") : uint.MaxValue,
            },
            Path = gameFile.Path,
        };

        Entries[entry.Path] = entry;
        return entry;
    }

    private async Task<uint> CalculateHashForFile(GameFile gameFile) {
        if (ReadOnly) {
            return 0;
        }

        var data = await gameFile.TryReadAsync();
        return data != null ? BinaryPrimitives.ReadUInt32LittleEndian(HashAlgorithm!.ComputeHash(data)) : 0;
    }

    private async Task<uint> CalculateHashForFile(IFileProvider provider, string path) {
        if (ReadOnly) {
            return 0;
        }

        return !provider.TryFindGameFile(path, out var gameFile) ? 0 : await CalculateHashForFile(gameFile);
    }

    public HistoryType Has(History other, HistoryEntry entry) {
        if (other.Options.ChecksumType != Options.ChecksumType) {
            return HistoryType.Undetermined;
        }
        
        if (!Entries.TryGetValue(entry.Path, out var localEntry)) {
            return HistoryType.New;
        }

        // don't compare ubulk/uptnl for volatility.
        var header = entry.Header;
        var localHeader = localEntry.Header;
        if (localHeader.Hash != header.Hash) {
            return HistoryType.Updated;
        }

        if (Options.HashExport && other.Options.HashExport && localHeader.ExportHash != header.ExportHash) {
            return HistoryType.Updated;
        }

        return HistoryType.Same;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly record struct HistoryHeader {
        public HistoryVersion Version { get; init; }
        public int Count { get; init; }
        public HistoryChecksumType ChecksumType { get; init; }
        public HistoryFlags Flags { get; init; }
    }
    

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly record struct HistoryEntryHeaderV1 {
        public long Size { get; init; }
        public uint Hash { get; init; }
        public uint ExportHash { get; init; }
        public uint BulkHash { get; init; }
        public int TextLength { get; init; }

        public HistoryEntryHeader Upgrade() {
            return new HistoryEntryHeader {
                Size = Size,
                Hash = Hash,
                ExportHash = ExportHash,
                BulkHash = BulkHash,
                UptnlHash = uint.MaxValue,
                TextLength = TextLength
            };
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public record struct HistoryEntryHeader {
        public long Size { get; init; }
        public uint Hash { get; init; }
        public uint ExportHash { get; init; }
        public uint BulkHash { get; init; }
        public uint UptnlHash { get; init; }
        public int TextLength { get; init; }
    }

    public record struct HistoryEntry {
        public HistoryEntryHeader Header { get; init; }
        public string Path { get; init; }
    }
}
