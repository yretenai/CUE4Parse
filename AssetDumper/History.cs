using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.FileProvider.Vfs;
using DragonLib.Hash;
using DragonLib.Hash.Algorithms;
using DragonLib.Hash.Basis;

namespace AssetDumper;

public class History {
    public enum HistoryType {
        Same,
        New,
        Updated,
    }

    private readonly HashAlgorithm? HashAlgorithm;
    private readonly bool ReadOnly;

    public Dictionary<string, HistoryEntry> Entries = new();

    public History() {
        if (CRC32CAlgorithm.IsSupported) {
            HashAlgorithm = CRC32CAlgorithm.Create();
        } else {
            HashAlgorithm = CRC.Create(CRC32Variants.Castagnoli);
        }

        ReadOnly = false;
    }

    public History(string? path) {
        ReadOnly = true;
        if (!File.Exists(path)) {
            return;
        }

        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[5];
        if (stream.Read(header) != 5) {
            throw new InvalidDataException();
        }

        var version = header[0];
        if (version > 1) {
            throw new NotSupportedException();
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(header[1..]);
        Entries.EnsureCapacity(count);
        Span<byte> entryBuffer = stackalloc byte[24];
        for (var index = 0; index < count; index++) {
            if (stream.Read(entryBuffer) != 24) {
                throw new InvalidDataException();
            }

            var size = BinaryPrimitives.ReadInt64LittleEndian(entryBuffer);
            var hash = BinaryPrimitives.ReadUInt32LittleEndian(entryBuffer[8..]);
            var expHash = BinaryPrimitives.ReadUInt32LittleEndian(entryBuffer[12..]);
            var bulkHash = BinaryPrimitives.ReadUInt32LittleEndian(entryBuffer[16..]);
            var textLength = BinaryPrimitives.ReadInt32LittleEndian(entryBuffer[20..]);
            var text = new byte[textLength].AsSpan();
            if (stream.Read(text) != textLength) {
                throw new InvalidDataException();
            }

            var entry = new HistoryEntry {
                Size = size,
                Hash = hash,
                ExportHash = expHash,
                BulkHash = bulkHash,
                Path = Encoding.UTF8.GetString(text),
            };
            Entries[entry.Path] = entry;
        }
    }

    public void Save(string path) {
        using var stream = File.OpenWrite(path);
        Span<byte> header = stackalloc byte[5];
        header[0] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(header[1..], Entries.Count);
        stream.Write(header);
        Span<byte> entryBuffer = stackalloc byte[24];
        foreach (var entry in Entries.Values) {
            BinaryPrimitives.WriteInt64LittleEndian(entryBuffer, entry.Size);
            BinaryPrimitives.WriteUInt32LittleEndian(entryBuffer[8..], entry.Hash);
            BinaryPrimitives.WriteUInt32LittleEndian(entryBuffer[12..], entry.ExportHash);
            BinaryPrimitives.WriteUInt32LittleEndian(entryBuffer[16..], entry.BulkHash);
            var text = Encoding.UTF8.GetBytes(entry.Path);
            BinaryPrimitives.WriteInt32LittleEndian(entryBuffer[20..], text.Length);
            stream.Write(entryBuffer);
            stream.Write(text);
        }
    }

    public async Task<HistoryEntry> Add(AbstractVfsFileProvider provider, GameFile gameFile) {
        if (ReadOnly) {
            return new HistoryEntry();
        }

        var entry = new HistoryEntry {
            Size = gameFile.Size,
            Path = gameFile.Path,
            Hash = await CalculateHashForFile(gameFile),
            ExportHash = await CalculateHashForFile(provider, gameFile.PathWithoutExtension + ".uexp"),
            BulkHash = await CalculateHashForFile(provider, gameFile.PathWithoutExtension + ".ubulk"),
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

    public HistoryType Has(HistoryEntry entry) {
        if (!Entries.TryGetValue(entry.Path, out var localEntry)) {
            return HistoryType.New;
        }

        // don't compare ubulk for volatility.
        if (localEntry.Hash != entry.Hash || localEntry.ExportHash != entry.ExportHash) {
            return HistoryType.Updated;
        }

        return HistoryType.Same;
    }

    public record struct HistoryEntry {
        public long Size { get; init; }
        public uint Hash { get; init; }
        public uint ExportHash { get; init; }
        public uint BulkHash { get; init; }
        public string Path { get; init; }
    }
}
