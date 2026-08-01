using System;
using System.Text;

namespace BrArchive;

/// <summary>
/// A single named entry within a <see cref="BrArchiveFile"/>, mirroring one file inside the
/// directory the archive represents.
/// </summary>
public sealed class BrArchiveEntry
{
    /// <summary>
    /// The entry's name, exactly as stored in the archive (e.g. <c>"banner_flow.png"</c> or
    /// <c>"terrain_texture.json"</c>). Vanilla archives store a single path segment - the file
    /// name relative to the directory the archive represents, not a full path.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The entry's raw content bytes. Empty when the entry has no embedded data (see
    /// <see cref="HasData"/>).
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// <see langword="true"/> if this entry has embedded content in the archive's data block.
    /// Some archives contain entries with a content length of zero - observed in the wild for
    /// certain non-JSON files - which this library surfaces as an entry with empty
    /// <see cref="Data"/> and <see cref="HasData"/> set to <see langword="false"/>, rather than
    /// silently guessing at a file that isn't actually embedded.
    /// </summary>
    public bool HasData { get; }

    /// <summary>The length, in bytes, of <see cref="Data"/>.</summary>
    public int Length => Data.Length;

    /// <summary>
    /// Creates a new entry. Most consumers should use <see cref="BrArchiveBuilder"/> to construct
    /// archives rather than constructing entries directly.
    /// </summary>
    /// <param name="name">The entry name. Must be at most <see cref="BrArchiveFormat.MaxNameLengthBytes"/> UTF-8 bytes and must not contain a NUL character.</param>
    /// <param name="data">The entry's content bytes. Pass an empty array for a data-less entry.</param>
    public BrArchiveEntry(string name, byte[] data)
        : this(name, data, hasData: data.Length > 0)
    {
    }

    internal BrArchiveEntry(string name, byte[] data, bool hasData)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        if (data is null) throw new ArgumentNullException(nameof(data));

        if (name.IndexOf('\0') >= 0)
            throw new ArgumentException("Entry names may not contain a NUL character.", nameof(name));

        int nameByteCount = Encoding.UTF8.GetByteCount(name);
        if (nameByteCount > BrArchiveFormat.MaxNameLengthBytes)
        {
            throw new ArgumentException(
                $"Entry name '{name}' is {nameByteCount} UTF-8 bytes long, which exceeds the format's " +
                $"{BrArchiveFormat.MaxNameLengthBytes}-byte limit.",
                nameof(name));
        }

        Name = name;
        Data = data;
        HasData = hasData;
    }

    /// <summary>Decodes <see cref="Data"/> as text using the given encoding (UTF-8 by default).</summary>
    public string GetText(Encoding? encoding = null) => (encoding ?? Encoding.UTF8).GetString(Data);

    /// <inheritdoc />
    public override string ToString() => $"{Name} ({Data.Length:N0} bytes)";
}
