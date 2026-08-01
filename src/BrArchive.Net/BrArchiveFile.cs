using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BrArchive;

/// <summary>
/// An in-memory, read-only representation of a parsed <c>.brarchive</c> file: its format
/// version and the ordered list of entries it contains.
/// </summary>
/// <remarks>
/// Use one of the <c>Read*</c> static methods to parse an existing archive, and
/// <see cref="ToBuilder"/> if you want to modify it and write out a new archive. To build an
/// archive from scratch, start with <see cref="BrArchiveBuilder.Create"/> instead.
/// </remarks>
public sealed class BrArchiveFile
{
    private readonly List<BrArchiveEntry> _entries;
    private readonly Dictionary<string, int> _index;

    /// <summary>The format version stored in the archive's header (only known value is 1).</summary>
    public uint FormatVersion { get; }

    /// <summary>All entries in the archive, in the order they appear in the entry table.</summary>
    public IReadOnlyList<BrArchiveEntry> Entries => _entries;

    /// <summary>The number of entries in the archive.</summary>
    public int Count => _entries.Count;

    /// <summary>The name of every entry, in archive order.</summary>
    public IEnumerable<string> Names => _entries.Select(e => e.Name);

    /// <summary>Creates a <see cref="BrArchiveFile"/> directly from a set of entries and a format version.</summary>
    /// <exception cref="ArgumentException">Thrown if two entries share the same name.</exception>
    public BrArchiveFile(IEnumerable<BrArchiveEntry> entries, uint formatVersion = BrArchiveFormat.KnownFormatVersion)
    {
        if (entries is null) throw new ArgumentNullException(nameof(entries));

        _entries = new List<BrArchiveEntry>(entries);
        _index = new Dictionary<string, int>(_entries.Count, StringComparer.Ordinal);
        for (int i = 0; i < _entries.Count; i++)
        {
            string name = _entries[i].Name;
            if (_index.ContainsKey(name))
                throw new ArgumentException($"Duplicate entry name '{name}'.", nameof(entries));
            _index[name] = i;
        }

        FormatVersion = formatVersion;
    }

    /// <summary>Gets the entry with the given name, or throws <see cref="KeyNotFoundException"/> if it doesn't exist.</summary>
    public BrArchiveEntry this[string name]
    {
        get
        {
            if (TryGetEntry(name, out var entry))
                return entry!;
            throw new KeyNotFoundException($"No entry named '{name}' in this archive.");
        }
    }

    /// <summary>Returns <see langword="true"/> if the archive contains an entry with the given name.</summary>
    public bool Contains(string name) => _index.ContainsKey(name);

    /// <summary>Attempts to get the entry with the given name.</summary>
    public bool TryGetEntry(string name, out BrArchiveEntry? entry)
    {
        if (_index.TryGetValue(name, out int i))
        {
            entry = _entries[i];
            return true;
        }
        entry = null;
        return false;
    }

    /// <summary>Returns a mutable <see cref="BrArchiveBuilder"/> seeded with this archive's current entries, for editing.</summary>
    public BrArchiveBuilder ToBuilder()
    {
        var builder = BrArchiveBuilder.Create();
        foreach (var entry in _entries)
            builder.Add(entry.Name, entry.Data);
        return builder;
    }

    /// <summary>Returns a snapshot dictionary of entry name to content bytes.</summary>
    public IReadOnlyDictionary<string, byte[]> ToDictionary() =>
        _entries.ToDictionary(e => e.Name, e => e.Data, StringComparer.Ordinal);

    // ============================================================
    // Reading
    // ============================================================

    /// <summary>Reads and parses a <c>.brarchive</c> file from disk.</summary>
    public static BrArchiveFile ReadFile(string path, BrArchiveReadOptions? options = null)
    {
        using var stream = File.OpenRead(path);
        return Read(stream, options);
    }

    /// <summary>Asynchronously reads and parses a <c>.brarchive</c> file from disk.</summary>
    public static async Task<BrArchiveFile> ReadFileAsync(string path, BrArchiveReadOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var stream = File.OpenRead(path);
        return await ReadAsync(stream, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Parses a <c>.brarchive</c> file already loaded into memory.</summary>
    public static BrArchiveFile Read(byte[] data, BrArchiveReadOptions? options = null)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        using var stream = new MemoryStream(data, writable: false);
        return Read(stream, options);
    }

    /// <summary>Parses a <c>.brarchive</c> file from a stream. The stream must support seeking.</summary>
    public static BrArchiveFile Read(Stream stream, BrArchiveReadOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanSeek)
            throw new ArgumentException("The stream must support seeking to parse a .brarchive file.", nameof(stream));

        options ??= BrArchiveReadOptions.Default;

        long streamLength = stream.Length;
        if (streamLength < BrArchiveFormat.HeaderSize)
            throw new BrArchiveFormatException($"File is only {streamLength} bytes long, too short for a .brarchive header ({BrArchiveFormat.HeaderSize} bytes).");

        var header = new byte[BrArchiveFormat.HeaderSize];
        ReadExactly(stream, header, 0, header.Length);

        if (options.ValidateMagic && !BrArchiveFormat.MagicMatches(header, 0))
        {
            throw new BrArchiveFormatException(
                "Magic number does not match the expected .brarchive signature. This file is either not " +
                "a .brarchive, or it is corrupted.");
        }

        uint entryCount = BrArchiveFormat.ReadUInt32LE(header, 8);
        uint formatVersion = BrArchiveFormat.ReadUInt32LE(header, 12);

        if (options.RequireKnownVersion && formatVersion != BrArchiveFormat.KnownFormatVersion)
        {
            throw new BrArchiveFormatException(
                $"Unrecognized .brarchive format version {formatVersion} (expected {BrArchiveFormat.KnownFormatVersion}).");
        }

        // Guard against a corrupt/malicious entry count causing an absurd allocation.
        long entryTableSize = checked((long)entryCount * BrArchiveFormat.EntryRecordSize);
        long dataBlockStart = BrArchiveFormat.HeaderSize + entryTableSize;
        if (dataBlockStart > streamLength)
        {
            throw new BrArchiveFormatException(
                $"Entry table claims {entryCount} entries ({entryTableSize} bytes), which extends past the " +
                $"end of the file ({streamLength} bytes). The file is likely truncated or corrupted.");
        }

        var rawEntries = new List<(string Name, uint Offset, uint Size)>((int)Math.Min(entryCount, int.MaxValue));
        var recordBuffer = new byte[BrArchiveFormat.EntryRecordSize];

        for (uint i = 0; i < entryCount; i++)
        {
            ReadExactly(stream, recordBuffer, 0, recordBuffer.Length);

            byte nameLength = recordBuffer[0];
            if (nameLength > BrArchiveFormat.NameBufferSize)
            {
                throw new BrArchiveFormatException(
                    $"Entry #{i} declares a name length of {nameLength}, which exceeds the maximum of " +
                    $"{BrArchiveFormat.NameBufferSize}.");
            }

            string name = Encoding.UTF8.GetString(recordBuffer, 1, nameLength);
            uint offset = BrArchiveFormat.ReadUInt32LE(recordBuffer, 1 + BrArchiveFormat.NameBufferSize);
            uint size = BrArchiveFormat.ReadUInt32LE(recordBuffer, 1 + BrArchiveFormat.NameBufferSize + 4);

            rawEntries.Add((name, offset, size));
        }

        var entries = new List<BrArchiveEntry>(rawEntries.Count);
        foreach (var raw in rawEntries)
        {
            byte[] content;
            bool hasData = raw.Size > 0;

            if (!hasData)
            {
                content = Array.Empty<byte>();
            }
            else
            {
                long absoluteOffset = dataBlockStart + raw.Offset;
                long absoluteEnd = absoluteOffset + raw.Size;
                if (absoluteOffset < dataBlockStart || absoluteEnd > streamLength)
                {
                    throw new BrArchiveFormatException(
                        $"Entry '{raw.Name}' declares a content range [{absoluteOffset}, {absoluteEnd}) that " +
                        $"falls outside the file (length {streamLength}). The file is likely truncated or corrupted.");
                }

                content = new byte[raw.Size];
                stream.Position = absoluteOffset;
                ReadExactly(stream, content, 0, content.Length);
            }

            entries.Add(new BrArchiveEntry(raw.Name, content, hasData));
        }

        return new BrArchiveFile(entries, formatVersion);
    }

    /// <summary>Asynchronously parses a <c>.brarchive</c> file from a stream. The stream must support seeking.</summary>
    public static async Task<BrArchiveFile> ReadAsync(Stream stream, BrArchiveReadOptions? options = null, CancellationToken cancellationToken = default)
    {
        // The parsing logic requires random access to compute/validate offsets. To keep a single
        // source of truth for the format rules, we buffer the whole stream into memory
        // asynchronously and then delegate to the synchronous parser, which never itself blocks
        // on I/O once the buffer is filled.
        if (stream is null) throw new ArgumentNullException(nameof(stream));

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        return Read(buffer, options);
    }

    private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (read == 0)
                throw new BrArchiveFormatException("Unexpected end of stream while reading .brarchive data - the file is likely truncated.");
            totalRead += read;
        }
    }

    // ============================================================
    // Writing
    // ============================================================

    /// <summary>Serializes this archive to a byte array.</summary>
    public byte[] ToArray(BrArchiveWriteOptions? options = null)
    {
        using var stream = new MemoryStream();
        Write(stream, options);
        return stream.ToArray();
    }

    /// <summary>Serializes this archive to a file on disk, overwriting it if it already exists.</summary>
    public void WriteFile(string path, BrArchiveWriteOptions? options = null)
    {
        using var stream = File.Create(path);
        Write(stream, options);
    }

    /// <summary>Asynchronously serializes this archive to a file on disk, overwriting it if it already exists.</summary>
    public async Task WriteFileAsync(string path, BrArchiveWriteOptions? options = null, CancellationToken cancellationToken = default)
    {
        var bytes = ToArray(options);
        using var stream = File.Create(path);
        await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Serializes this archive to a stream.</summary>
    public void Write(Stream stream, BrArchiveWriteOptions? options = null)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        options ??= BrArchiveWriteOptions.Default;

        IEnumerable<BrArchiveEntry> ordered = options.SortEntries
            ? _entries.OrderBy(e => e.Name, StringComparer.Ordinal)
            : _entries;
        var orderedList = ordered.ToList();

        var header = new byte[BrArchiveFormat.HeaderSize];
        Array.Copy(BrArchiveFormat.MagicNumber, header, BrArchiveFormat.MagicNumber.Length);
        BrArchiveFormat.WriteUInt32LE(header, 8, (uint)orderedList.Count);
        BrArchiveFormat.WriteUInt32LE(header, 12, options.FormatVersion);
        stream.Write(header, 0, header.Length);

        // First pass: write the entry table, computing each entry's relative data offset.
        uint runningOffset = 0;
        var recordBuffer = new byte[BrArchiveFormat.EntryRecordSize];
        foreach (var entry in orderedList)
        {
            Array.Clear(recordBuffer, 0, recordBuffer.Length);

            byte[] nameBytes = Encoding.UTF8.GetBytes(entry.Name);
            // Constructor-time validation already guarantees this fits, but assert defensively.
            if (nameBytes.Length > BrArchiveFormat.NameBufferSize)
                throw new BrArchiveFormatException($"Entry name '{entry.Name}' is too long to serialize.");

            recordBuffer[0] = (byte)nameBytes.Length;
            Array.Copy(nameBytes, 0, recordBuffer, 1, nameBytes.Length);

            BrArchiveFormat.WriteUInt32LE(recordBuffer, 1 + BrArchiveFormat.NameBufferSize, entry.Data.Length > 0 ? runningOffset : 0);
            BrArchiveFormat.WriteUInt32LE(recordBuffer, 1 + BrArchiveFormat.NameBufferSize + 4, (uint)entry.Data.Length);

            stream.Write(recordBuffer, 0, recordBuffer.Length);

            runningOffset += (uint)entry.Data.Length;
        }

        // Second pass: the data block itself, in the same order.
        foreach (var entry in orderedList)
        {
            if (entry.Data.Length > 0)
                stream.Write(entry.Data, 0, entry.Data.Length);
        }
    }
}
