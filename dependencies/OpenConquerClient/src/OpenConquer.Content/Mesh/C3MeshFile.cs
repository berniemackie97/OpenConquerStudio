using System.Buffers.Binary;

namespace OpenConquer.Content.Mesh;

public readonly record struct C3MeshSection(uint Identifier, int Offset, ReadOnlyMemory<byte> Payload);

public sealed class C3MeshFile
{
    public const int MaximumFileBytes = 32 * 1024 * 1024;

    private const int FileHeaderLength = 16;
    private const int SectionHeaderLength = 8;
    private const int MaximumPhyCount = 16;
    private const int MaximumSectionCount = 65_536;
    private const uint PhyPrefix = 0x00594850;

    private C3MeshFile(C3MeshSection[] sections, int phyCount)
    {
        Sections = Array.AsReadOnly(sections);
        PhyCount = phyCount;
    }

    public IReadOnlyList<C3MeshSection> Sections
    {
        get;
    }

    public int PhyCount
    {
        get;
    }

    public static C3MeshFile Load(IClientContentSource source, string path)
    {
        byte[] bytes = ContentReader.ReadRequiredBytes(
            source, path, ContentLookupMode.PackageOnly, MaximumFileBytes);
        return Parse(bytes);
    }

    public static C3MeshFile Parse(ReadOnlyMemory<byte> data)
    {
        if (data.Length < FileHeaderLength || data.Length > MaximumFileBytes)
        {
            throw new InvalidDataException("C3 file size is outside supported bounds.");
        }

        ReadOnlySpan<byte> bytes = data.Span;
        List<C3MeshSection> sections = [];
        int phyCount = 0;
        int offset = FileHeaderLength;

        while (offset < bytes.Length)
        {
            if (bytes.Length - offset < SectionHeaderLength || sections.Count >= MaximumSectionCount)
            {
                throw new InvalidDataException("C3 section header is truncated or the section limit was exceeded.");
            }

            uint identifier = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 4)..]);
            int dataOffset = offset + SectionHeaderLength;
            if (length > bytes.Length - dataOffset)
            {
                throw new InvalidDataException("C3 section exceeds the file boundary.");
            }

            if ((identifier & 0x00FFFFFF) == PhyPrefix && IsPhyVariant((byte)(identifier >> 24)))
            {
                if (++phyCount > MaximumPhyCount)
                {
                    throw new InvalidDataException("C3 mesh exceeds the supported PHY count.");
                }
            }

            sections.Add(new C3MeshSection(identifier, dataOffset, data.Slice(dataOffset, (int)length)));
            offset = dataOffset + (int)length;
        }

        return new C3MeshFile([.. sections], phyCount);
    }

    private static bool IsPhyVariant(byte variant) =>
        variant is 0x20 or 0x32 or 0x33 or 0x34;
}
