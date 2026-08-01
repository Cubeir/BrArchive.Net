namespace BrArchive;

/// <summary>Options controlling how a <c>.brarchive</c> stream is parsed.</summary>
public sealed class BrArchiveReadOptions
{
    /// <summary>
    /// When <see langword="true"/> (the default), the 8-byte magic number is validated and a
    /// <see cref="BrArchiveFormatException"/> is thrown if it doesn't match. Set to
    /// <see langword="false"/> only if you need to force-parse something that isn't a standard
    /// archive and know what you're doing.
    /// </summary>
    public bool ValidateMagic { get; set; } = true;

    /// <summary>
    /// When <see langword="true"/>, throws a <see cref="BrArchiveFormatException"/> if the
    /// archive's format version field is anything other than <see cref="BrArchiveFormat.KnownFormatVersion"/>.
    /// Defaults to <see langword="false"/> so this library keeps working if Mojang ever ships a
    /// new version with backward-compatible layout; the version is always available afterwards
    /// via <see cref="BrArchiveFile.FormatVersion"/>.
    /// </summary>
    public bool RequireKnownVersion { get; set; } = false;

    /// <summary>A ready-to-use instance with every option at its default value.</summary>
    public static BrArchiveReadOptions Default { get; } = new();
}

/// <summary>Options controlling how a <c>.brarchive</c> stream is produced.</summary>
public sealed class BrArchiveWriteOptions
{
    /// <summary>
    /// The format version to write into the header. Defaults to <see cref="BrArchiveFormat.KnownFormatVersion"/>
    /// (1), the only value observed in vanilla archives - you should not normally need to change this.
    /// </summary>
    public uint FormatVersion { get; set; } = BrArchiveFormat.KnownFormatVersion;

    /// <summary>
    /// When <see langword="true"/> (the default), entries are written in ordinal name order,
    /// matching the ordering used by vanilla Bedrock archives. Set to <see langword="false"/> to
    /// preserve insertion order instead.
    /// </summary>
    public bool SortEntries { get; set; } = true;

    /// <summary>A ready-to-use instance with every option at its default value.</summary>
    public static BrArchiveWriteOptions Default { get; } = new();
}
