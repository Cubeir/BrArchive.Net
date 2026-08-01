using System;

namespace BrArchive;

/// <summary>
/// Thrown when data being parsed does not conform to the expected <c>.brarchive</c> layout -
/// for example a bad magic number, a truncated header/entry table, or an entry whose declared
/// content range falls outside the file.
/// </summary>
public sealed class BrArchiveFormatException : Exception
{
    /// <summary>Creates a new <see cref="BrArchiveFormatException"/>.</summary>
    public BrArchiveFormatException(string message) : base(message)
    {
    }

    /// <summary>Creates a new <see cref="BrArchiveFormatException"/> with an inner exception.</summary>
    public BrArchiveFormatException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
