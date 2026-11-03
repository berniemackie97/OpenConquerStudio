using System.Buffers.Binary;
using System.Text;

namespace OpenConquer.Content.Mesh;

internal readonly record struct C3RolePartSource(string MeshDatabase, string MotionDatabase);

internal readonly record struct C3PartDescriptor(
    uint ObjectId, uint PrimaryTextureId, uint SecondaryTextureId,
    byte State, byte SourceBlend, byte DestinationBlend, byte Material);

internal sealed class C3RolePartTables
{
    internal Dictionary<string, C3RolePartSource> Sources
    {
        get;
    } = new(StringComparer.Ordinal);

    internal HashSet<string> VirtualMeshNames
    {
        get;
    } = new(StringComparer.OrdinalIgnoreCase);

    internal static C3RolePartTables Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x54504F52)
        {
            throw new InvalidDataException("Invalid role-part configuration database.");
        }

        uint configCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        uint customCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]);
        if (configCount > 256 || customCount > 256 ||
            12L + configCount * 0x220L + customCount * 0x24L != bytes.Length)
        {
            throw new InvalidDataException("Role-part configuration record count exceeds its payload.");
        }

        C3RolePartTables result = new();
        int offset = 12;
        for (int i = 0; i < configCount; i++)
        {
            ReadOnlySpan<byte> entry = bytes.Slice(offset, 0x220);
            string name = ReadName(entry[..0x20]);
            string mesh = ReadName(entry.Slice(0x20, 0x100));
            string motion = ReadName(entry.Slice(0x120, 0x100));
            if (!result.Sources.TryAdd(name, new C3RolePartSource(mesh, motion)))
            {
                throw new InvalidDataException("Duplicate role-part configuration name.");
            }

            offset += 0x220;
        }

        for (int i = 0; i < customCount; i++)
        {
            ReadOnlySpan<byte> entry = bytes.Slice(offset, 0x24);
            string name = ReadName(entry[4..]);
            if (!result.VirtualMeshNames.Add(name))
            {
                throw new InvalidDataException("Duplicate virtual mesh name.");
            }

            offset += 0x24;
        }

        return result;
    }

    internal static Dictionary<uint, C3PartDescriptor[]> ReadMeshes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 8 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x4853454D)
        {
            throw new InvalidDataException("Invalid role-part mesh database.");
        }

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        if (count > 65_536)
        {
            throw new InvalidDataException("Role-part model count exceeds its budget.");
        }

        Dictionary<uint, C3PartDescriptor[]> models = new((int)count);
        int offset = 8;
        for (int i = 0; i < count; i++)
        {
            if (bytes.Length - offset < 8)
            {
                throw new InvalidDataException("Role-part model record is truncated.");
            }

            uint model = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
            uint parts = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 4)..]);
            offset += 8;
            if (parts > 4 || parts * 16 > bytes.Length - offset)
            {
                throw new InvalidDataException("Role-part model parts exceed the database.");
            }

            C3PartDescriptor[] descriptors = new C3PartDescriptor[parts];
            for (int part = 0; part < parts; part++)
            {
                ReadOnlySpan<byte> entry = bytes.Slice(offset, 16);
                descriptors[part] = new C3PartDescriptor(
                    BinaryPrimitives.ReadUInt32LittleEndian(entry),
                    BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]),
                    BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]),
                    entry[12], entry[13], entry[14], entry[15]);
                offset += 16;
            }

            models[model] = descriptors;
        }

        if (offset != bytes.Length)
        {
            throw new InvalidDataException("Role-part mesh database contains trailing records.");
        }

        return models;
    }

    private static string ReadName(ReadOnlySpan<byte> input)
    {
        int terminator = input.IndexOf((byte)0);
        if (terminator <= 0)
        {
            throw new InvalidDataException("Role-part name has no terminator.");
        }

        return Encoding.ASCII.GetString(input[..terminator]);
    }
}
