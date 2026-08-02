namespace BrArchive;

/// <summary>
/// Low-level constants describing the on-disk layout of the Minecraft Bedrock Edition
/// <c>.brarchive</c> format, plus endian-safe primitive helpers used by the reader/writer.
/// </summary>
/// <remarks>
/// <para>
/// <c>.brarchive</c> was introduced by Mojang in Minecraft Bedrock Edition 1.21.40 to bundle the many
/// small JSON files inside built-in resource/behavior packs (e.g. <c>textures.brarchive</c>,
/// <c>sounds.brarchive</c>) into a single file per directory, reducing filesystem I/O on platforms
/// where opening many small files is slow. Archives live under a <c>__brarchive</c> directory at the
/// pack root and are excluded from the pack's <c>contents.json</c>.
/// </para>
/// <para>
/// This is not an official/documented Mojang format - there is no public specification. This
/// implementation was written independently, based purely on the observable byte layout as
/// described by community reverse-engineering write-ups, and cross-checked against the behavior of
/// two independent open-source reference implementations (one in Rust, one in C - see
/// THIRD-PARTY-NOTICES.md in the repository root for full credit and links). No code from either
/// project was copied; only the file-format knowledge was reused, which is not itself protected by
/// either project's copyright/license.
/// </para>
/// <para>
/// On-disk layout:
/// <code>
/// Offset  Size   Field
/// ------  -----  -----------------------------------------------------------
/// 0       8      Magic number, fixed bytes: 7D 27 25 B1 A0 52 70 26
/// 8       4      Entry count (uint32, little-endian)
/// 12      4      Format version (uint32, little-endian) - only known value is 1
/// 16      N*256  Entry table, N = entry count, see below
/// 16+N*256 ...   Data block: raw concatenated bytes of every entry's content
/// </code>
/// Each 256-byte entry record:
/// <code>
/// Offset  Size  Field
/// ------  ----  -----------------------------------------------------------
/// 0       1     Name length in bytes (0-247)
/// 1       247   Name, UTF-8, zero-padded after NameLength bytes
/// 248     4     Content offset (uint32, little-endian) - relative to the start of the data block
/// 252     4     Content length in bytes (uint32, little-endian)
/// </code>
/// </para>
/// <para>
/// A content length of 0 means the entry has no embedded bytes in this archive. This has been
/// observed in some archives for some entries - historically, non-JSON files were a common case,
/// though that isn't a rule the format enforces, and doesn't necessarily hold for every game
/// version (Mojang appears to have expanded what gets embedded over time). Reading such an entry
/// yields an empty <see cref="BrArchiveEntry.Data"/>; anything with a non-zero length, JSON or
/// otherwise, is read back exactly as stored.
/// </para>
/// </remarks>
public static class BrArchiveFormat
{
    /// <summary>The fixed 8-byte magic number every valid .brarchive file begins with.</summary>
    public static readonly byte[] MagicNumber =
    {
        0x7D, 0x27, 0x25, 0xB1, 0xA0, 0x52, 0x70, 0x26
    };

    /// <summary>Size, in bytes, of the fixed archive header (magic + entry count + version).</summary>
    public const int HeaderSize = 16;

    /// <summary>Size, in bytes, of a single entry record within the entry table.</summary>
    public const int EntryRecordSize = 256;

    /// <summary>Number of bytes reserved for an entry's name buffer (including zero padding).</summary>
    public const int NameBufferSize = 247;

    /// <summary>The maximum length, in UTF-8 bytes, that an entry name may have.</summary>
    public const int MaxNameLengthBytes = NameBufferSize;

    /// <summary>The only format version known to exist in the wild.</summary>
    public const uint KnownFormatVersion = 1;

    internal static uint ReadUInt32LE(byte[] buffer, int offset)
    {
        return (uint)(buffer[offset]
            | (buffer[offset + 1] << 8)
            | (buffer[offset + 2] << 16)
            | (buffer[offset + 3] << 24));
    }

    internal static void WriteUInt32LE(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    internal static bool MagicMatches(byte[] buffer, int offset)
    {
        for (int i = 0; i < MagicNumber.Length; i++)
        {
            if (buffer[offset + i] != MagicNumber[i])
                return false;
        }
        return true;
    }
}