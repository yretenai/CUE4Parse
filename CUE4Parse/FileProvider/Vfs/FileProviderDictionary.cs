using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.VirtualFileSystem;
using CUE4Parse.Utils;
using DragonLib.Text;

namespace CUE4Parse.FileProvider.Vfs
{
    public class FileProviderDictionary : IReadOnlyDictionary<string, GameFile>
    {
        private readonly ConcurrentDictionary<FPackageId, GameFile> _byId = new ();
        public IReadOnlyDictionary<FPackageId, GameFile> byId => _byId;

        private readonly KeyEnumerable _keys;
        private readonly ValueEnumerable _values;
        private readonly ConcurrentBag<IReadOnlyDictionary<string, GameFile>> _indicesBag = new ();

        public readonly bool IsCaseInsensitive;
        public IEnumerable<string> Keys => _keys;
        public IEnumerable<GameFile> Values => _values;

        public FileProviderDictionary(bool isCaseInsensitive)
        {
            IsCaseInsensitive = isCaseInsensitive;
            _keys = new KeyEnumerable(this);
            _values = new ValueEnumerable(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddFiles(IReadOnlyDictionary<string, GameFile> newFiles)
        {
            foreach (var file in newFiles.Values)
            {
                if (file is FIoStoreEntry {IsUE4Package: true} ioEntry)
                {
                    _byId[ioEntry.ChunkId.AsPackageId()] = file;
                }
            }
            _indicesBag.Add(newFiles);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _indicesBag.Clear();
            _byId.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(string key)
        {
            if (IsCaseInsensitive)
                key = key.ToLowerInvariant();
            foreach (var files in _indicesBag)
            {
                if (files.ContainsKey(key))
                    return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out GameFile value)
        {
            if (IsCaseInsensitive)
                key = key.ToLowerInvariant();
            var found = new List<GameFile>();
            foreach (var files in _indicesBag)
            {
                if (files.TryGetValue(key, out var val)) {
                    found.Add(val);
                }
            }

            if (found.Count > 0) {
                value = found.OrderBy(x => x is not VfsEntry vfs ? string.Empty : Path.GetFileName(vfs.Vfs.Path), new NaturalStringComparer(StringComparison.Ordinal)).Reverse().First();
                return true;
            }

            value = default;
            return false;
        }


        public GameFile this[string path]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (TryGetValue(path, out var file))
                    return file;
                if (TryGetValue(path.SubstringBeforeWithLast('.') + GameFile.Ue4PackageExtensions[1], out file))
                    return file;

                throw new KeyNotFoundException($"There is no game file with the path \"{path}\"");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<KeyValuePair<string, GameFile>> GetEnumerator()
        {
            foreach (var index in _indicesBag)
            {
                foreach (var entry in index)
                {
                    yield return entry;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count => _indicesBag.Sum(it => it.Count);

        private class KeyEnumerable : IEnumerable<string>
        {
            private readonly FileProviderDictionary _orig;

            internal KeyEnumerable(FileProviderDictionary orig)
            {
                _orig = orig;
            }

            public IEnumerator<string> GetEnumerator()
            {
                foreach (var index in _orig._indicesBag)
                {
                    foreach (var key in index.Keys)
                    {
                        yield return key;
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private class ValueEnumerable : IEnumerable<GameFile>
        {
            private readonly FileProviderDictionary _orig;

            internal ValueEnumerable(FileProviderDictionary orig)
            {
                _orig = orig;
            }

            public IEnumerator<GameFile> GetEnumerator()
            {
                foreach (var index in _orig._indicesBag)
                {
                    foreach (var key in index.Values)
                    {
                        yield return key;
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
