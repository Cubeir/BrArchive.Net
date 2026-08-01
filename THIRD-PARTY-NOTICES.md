# Third-Party Notices

BrArchive.Net contains no code copied from any other project. It is an independent,
clean-room implementation of the Minecraft Bedrock Edition `.brarchive` file format,
written entirely in C# for this repository.

However, `.brarchive` has no official public specification. Understanding of the
on-disk layout used to write this library was gained by studying two existing
open-source projects that had already reverse-engineered the format, and by
cross-referencing their observed behavior against community write-ups. In the
spirit of open-source credit (and because it's simply the right thing to do),
both projects are acknowledged here:

## bedrock-crustaceans/brarchive (Rust)

- Repository: https://github.com/bedrock-crustaceans/brarchive
- License: Apache License 2.0

## Torrekie/br-ar (C)

- Repository: https://github.com/Torrekie/br-ar
- License: GNU General Public License v3.0 (or later), per the license headers in
  the project's source files at the time this library was written.

## What was learned from each project

Both projects independently confirmed the same on-disk layout (fixed 8-byte
magic number, a 16-byte header, a 256-byte fixed-size entry record containing a
length-prefixed name plus a relative data offset/length, followed by a
concatenated data block). Reading two independent implementations that agree
with each other was valuable for building confidence in the format description
documented in `BrArchiveFormat.cs`, particularly around edge cases like
zero-length ("no embedded data") entries and how relative offsets are anchored.

If you're the maintainer of either project and would like this notice worded
differently, credited differently, or removed, please open an issue.
