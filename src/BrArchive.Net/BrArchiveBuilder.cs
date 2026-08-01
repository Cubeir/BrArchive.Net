using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BrArchive;

/// <summary>
/// A mutable, in-memory builder for creating a new <c>.brarchive</c> file or editing an existing
/// one before writing it back out.
/// </summary>
/// <remarks>
/// <code>
/// // Create from scratch:
/// var bytes = BrArchiveBuilder.Create()
///     .Add("terrain_texture.json", jsonBytes)
///     .Add("flipbook_textures.json", "{}", Encoding.UTF8)
///     .ToArray();
///
/// // Edit an existing archive:
/// var archive = BrArchiveFile.ReadFile("textures.brarchive");
/// archive.ToBuilder()
///     .Remove("old_entry.json")
///     .Add("new_entry.json", newBytes)
///     .SaveFile("textures.brarchive");
/// </code>
/// </remarks>
public sealed class BrArchiveBuilder
{
    private readonly Dictionary<string, byte[]> _entries = new(StringComparer.Ordinal);
    private readonly List<string> _order = new();

    /// <summary>
    /// When <see langword="true"/> (the default), entries are written in ordinal name order when
    /// this builder is built/saved, matching vanilla archive ordering. When <see langword="false"/>,
    /// entries are written in the order they were added.
    /// </summary>
    public bool SortEntries { get; set; } = true;

    /// <summary>The names of every entry currently staged in this builder.</summary>
    public IReadOnlyCollection<string> EntryNames => _order;

    /// <summary>The number of entries currently staged in this builder.</summary>
    public int Count => _order.Count;

    private BrArchiveBuilder()
    {
    }

    /// <summary>Creates a new, empty builder.</summary>
    public static BrArchiveBuilder Create() => new();

    /// <summary>
    /// Creates a builder pre-populated with every file found directly inside <paramref name="directoryPath"/>,
    /// mirroring how a vanilla <c>.brarchive</c> corresponds to a single pack directory (e.g. <c>textures/</c>).
    /// </summary>
    /// <param name="directoryPath">The directory to pack.</param>
    /// <param name="recursive">
    /// When <see langword="true"/>, files in subdirectories are included too, with entry names using
    /// <c>/</c> as a path separator relative to <paramref name="directoryPath"/>. Vanilla archives are
    /// not recursive (each subdirectory gets its own sibling <c>.brarchive</c>), so this defaults to
    /// <see langword="false"/>.
    /// </param>
    /// <param name="searchPattern">An optional glob pattern (as used by <see cref="Directory.GetFiles(string, string)"/>) to filter which files are included. Defaults to all files.</param>
    public static BrArchiveBuilder FromDirectory(string directoryPath, bool recursive = false, string searchPattern = "*")
    {
        return Create().AddDirectory(directoryPath, recursive, searchPattern);
    }

    /// <summary>Adds or replaces an entry with the given raw content bytes.</summary>
    public BrArchiveBuilder Add(string name, byte[] data)
    {
        // Route through BrArchiveEntry's constructor purely for its validation (name length, no NUL, etc.)
        // without needing to keep the wrapper object around.
        _ = new BrArchiveEntry(name, data);

        if (!_entries.ContainsKey(name))
            _order.Add(name);
        _entries[name] = data;
        return this;
    }

    /// <summary>Adds or replaces an entry with the given text content, encoded as UTF-8 by default.</summary>
    public BrArchiveBuilder Add(string name, string text, Encoding? encoding = null)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
        return Add(name, (encoding ?? Encoding.UTF8).GetBytes(text));
    }

    /// <summary>Adds or replaces an entry by reading the contents of a file from disk.</summary>
    /// <param name="entryName">The name the entry should have inside the archive.</param>
    /// <param name="sourceFilePath">The path of the file on disk to read content from.</param>
    public BrArchiveBuilder AddFile(string entryName, string sourceFilePath)
    {
        byte[] data = File.ReadAllBytes(sourceFilePath);
        return Add(entryName, data);
    }

    /// <summary>Adds every file directly inside <paramref name="directoryPath"/> (or recursively) as entries.</summary>
    /// <param name="directoryPath">The directory to pack.</param>
    /// <param name="recursive">Whether to include files in subdirectories, using <c>/</c>-separated relative names.</param>
    /// <param name="searchPattern">An optional glob pattern to filter which files are included.</param>
    public BrArchiveBuilder AddDirectory(string directoryPath, bool recursive = false, string searchPattern = "*")
    {
        if (directoryPath is null) throw new ArgumentNullException(nameof(directoryPath));

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (string filePath in Directory.GetFiles(directoryPath, searchPattern, option).OrderBy(p => p, StringComparer.Ordinal))
        {
            string relative = ToRelativeEntryName(directoryPath, filePath);
            AddFile(relative, filePath);
        }
        return this;
    }

    // Deliberately hand-rolled instead of Path.GetRelativePath, which isn't available on
    // netstandard2.0 (it needs netstandard2.1+). filePath is always produced by
    // Directory.GetFiles(directoryPath, ...) above, so the prefix relationship always holds.
    private static string ToRelativeEntryName(string root, string fullPath)
    {
        string normalizedRoot = Path.GetFullPath(root);
        if (normalizedRoot.Length > 0 &&
            normalizedRoot[normalizedRoot.Length - 1] != Path.DirectorySeparatorChar &&
            normalizedRoot[normalizedRoot.Length - 1] != Path.AltDirectorySeparatorChar)
        {
            normalizedRoot += Path.DirectorySeparatorChar;
        }

        string normalizedFull = Path.GetFullPath(fullPath);
        string relative = normalizedFull.StartsWith(normalizedRoot, StringComparison.Ordinal)
            ? normalizedFull.Substring(normalizedRoot.Length)
            : normalizedFull;

        return relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    /// <summary>Removes the entry with the given name, if present. A no-op if no such entry is staged.</summary>
    public BrArchiveBuilder Remove(string name)
    {
        if (_entries.Remove(name))
            _order.Remove(name);
        return this;
    }

    /// <summary>Removes the entry with the given name, if present, and reports whether it existed.</summary>
    public BrArchiveBuilder Remove(string name, out bool removed)
    {
        removed = _entries.Remove(name);
        if (removed)
            _order.Remove(name);
        return this;
    }

    /// <summary>Returns <see langword="true"/> if an entry with the given name is currently staged.</summary>
    public bool Contains(string name) => _entries.ContainsKey(name);

    /// <summary>Gets the currently staged content bytes for the given entry name.</summary>
    public bool TryGetData(string name, out byte[]? data) => _entries.TryGetValue(name, out data);

    /// <summary>Removes every staged entry.</summary>
    public BrArchiveBuilder Clear()
    {
        _entries.Clear();
        _order.Clear();
        return this;
    }

    /// <summary>Builds an immutable <see cref="BrArchiveFile"/> from the currently staged entries.</summary>
    public BrArchiveFile Build(uint formatVersion = BrArchiveFormat.KnownFormatVersion)
    {
        IEnumerable<string> names = SortEntries ? _order.OrderBy(n => n, StringComparer.Ordinal) : _order;
        var entries = names.Select(n => new BrArchiveEntry(n, _entries[n]));
        return new BrArchiveFile(entries, formatVersion);
    }

    /// <summary>Serializes the currently staged entries directly to a byte array.</summary>
    public byte[] ToArray(BrArchiveWriteOptions? options = null) => Build().ToArray(options);

    /// <summary>Serializes the currently staged entries directly to a stream.</summary>
    public void Save(Stream stream, BrArchiveWriteOptions? options = null) => Build().Write(stream, options);

    /// <summary>Serializes the currently staged entries directly to a file on disk, overwriting it if it already exists.</summary>
    public void SaveFile(string path, BrArchiveWriteOptions? options = null) => Build().WriteFile(path, options);

    /// <summary>Asynchronously serializes the currently staged entries directly to a file on disk.</summary>
    public Task SaveFileAsync(string path, BrArchiveWriteOptions? options = null, CancellationToken cancellationToken = default) =>
        Build().WriteFileAsync(path, options, cancellationToken);
}
