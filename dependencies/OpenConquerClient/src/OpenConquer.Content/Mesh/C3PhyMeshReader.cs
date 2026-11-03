using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace OpenConquer.Content.Mesh;

public static class C3PhyMeshReader
{
    public static C3PhyMesh Read(C3MeshSection section)
    {
        if ((section.Identifier & 0x00FFFFFF) != 0x00594850)
        {
            throw new InvalidDataException("The section is not a PHY mesh.");
        }

        byte variant = (byte)(section.Identifier >> 24);
        int stride = variant switch
        {
            0x20 => 76,
            0x32 => 88,
            0x33 => 52,
            0x34 => 40,
            _ => throw new InvalidDataException("Unsupported PHY vertex layout.")
        };

        PayloadReader reader = new(section.Payload);
        int nameLength = reader.ReadCount();
        string name = Encoding.Latin1.GetString(reader.Read(nameLength).Span);
        int terminator = name.IndexOf('\0');
        if (terminator >= 0)
        {
            name = name[..terminator];
        }
        uint flags = reader.ReadUInt32();
        C3PhyVertex[] vertices = ReadVertices(ref reader, variant, stride);
        ushort[] indices = ReadIndices(ref reader, vertices.Length);

        reader.Read(reader.ReadCount());
        Vector3 boundsMin = ReadVector3(reader.Read(12).Span);
        Vector3 boundsMax = ReadVector3(reader.Read(12).Span);
        Matrix4x4 matrix = ReadMatrix(reader.Read(64).Span);
        TransformVertices(vertices, matrix);

        uint stepFrameCount = reader.ReadUInt32();
        ReadOnlyMemory<byte> alphaKeys = reader.ReadItems(reader.ReadCount(), 16);
        ReadOnlyMemory<byte> drawKeys = reader.ReadItems(reader.ReadCount(), 16);
        ReadOnlyMemory<byte> textureKeys = reader.ReadItems(reader.ReadCount(), 16);
        ReadOnlyMemory<byte> trailing = reader.Read(reader.Remaining);

        if (variant is 0x20 or 0x34)
        {
            C3PhyNormals.Generate(vertices, indices);
        }

        return new C3PhyMesh(name, flags, vertices, indices, matrix,
            Vector3.Min(boundsMin, boundsMax), Vector3.Max(boundsMin, boundsMax),
            stepFrameCount, alphaKeys, drawKeys, textureKeys, trailing);
    }

    private static C3PhyVertex[] ReadVertices(ref PayloadReader reader, byte variant, int stride)
    {
        int count = SumCounts(reader.ReadCount(), reader.ReadCount());
        ReadOnlyMemory<byte> records = reader.ReadItems(count, stride);
        C3PhyVertex[] vertices = new C3PhyVertex[count];
        int uvOffset = variant is 0x20 or 0x32 ? 48 : 12;
        int boneOffset = uvOffset + 8;
        bool generatedNormals = variant is 0x20 or 0x34;

        for (int i = 0; i < vertices.Length; i++)
        {
            ReadOnlySpan<byte> input = records.Span.Slice(i * stride, stride);
            Vector3 position = ReadVector3(input);
            Vector2 uv = new(ReadFloat(input, uvOffset), ReadFloat(input, uvOffset + 4));
            float reserved = ReadFloat(input, boneOffset);
            uint boneIndex2 = BinaryPrimitives.ReadUInt32LittleEndian(input[(boneOffset + 4)..]);
            uint boneIndex1 = BinaryPrimitives.ReadUInt32LittleEndian(input[(boneOffset + 8)..]);
            float boneWeight2 = ReadFloat(input, boneOffset + 12);
            float boneWeight1 = ReadFloat(input, boneOffset + 16);
            Vector3 normal = generatedNormals ? Vector3.Zero : ReadVector3(input[(boneOffset + 20)..]);
            vertices[i] = new C3PhyVertex(position, normal, uv, reserved,
                boneIndex2, boneIndex1, boneWeight2, boneWeight1);
        }

        return vertices;
    }

    private static ushort[] ReadIndices(ref PayloadReader reader, int vertexCount)
    {
        int faceCount = SumCounts(reader.ReadCount(), reader.ReadCount());
        ReadOnlySpan<byte> bytes = reader.ReadItems(faceCount, 6).Span;
        ushort[] indices = new ushort[faceCount * 3];

        for (int i = 0; i < indices.Length; i++)
        {
            ushort index = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(i * 2)..]);
            if (index >= vertexCount)
            {
                throw new InvalidDataException("PHY triangle index exceeds its vertex count.");
            }

            indices[i] = index;
        }

        return indices;
    }

    private static void TransformVertices(C3PhyVertex[] vertices, Matrix4x4 matrix)
    {
        for (int i = 0; i < vertices.Length; i++)
        {
            C3PhyVertex vertex = vertices[i];
            Vector4 transformed = Vector4.Transform(new Vector4(vertex.Position, 1), matrix);
            if (MathF.Abs(transformed.W - 1f) > 1.1920929e-7f)
            {
                transformed /= transformed.W;
            }

            if (!float.IsFinite(transformed.X) || !float.IsFinite(transformed.Y) ||
                !float.IsFinite(transformed.Z))
            {
                throw new InvalidDataException("PHY vertex transform produces invalid coordinates.");
            }

            vertices[i] = vertex with
            {
                Position = new Vector3(transformed.X, transformed.Y, transformed.Z)
            };
        }
    }

    private static Matrix4x4 ReadMatrix(ReadOnlySpan<byte> bytes) => new(
        ReadFloat(bytes, 0), ReadFloat(bytes, 4), ReadFloat(bytes, 8), ReadFloat(bytes, 12),
        ReadFloat(bytes, 16), ReadFloat(bytes, 20), ReadFloat(bytes, 24), ReadFloat(bytes, 28),
        ReadFloat(bytes, 32), ReadFloat(bytes, 36), ReadFloat(bytes, 40), ReadFloat(bytes, 44),
        ReadFloat(bytes, 48), ReadFloat(bytes, 52), ReadFloat(bytes, 56), ReadFloat(bytes, 60));

    private static int SumCounts(int first, int second) =>
        (long)first + second <= int.MaxValue
            ? first + second
            : throw new InvalidDataException("PHY element count exceeds supported bounds.");

    private static Vector3 ReadVector3(ReadOnlySpan<byte> input) =>
        new(ReadFloat(input, 0), ReadFloat(input, 4), ReadFloat(input, 8));

    private static float ReadFloat(ReadOnlySpan<byte> input, int offset) =>
        BinaryPrimitives.ReadSingleLittleEndian(input[offset..]);

    private struct PayloadReader(ReadOnlyMemory<byte> data)
    {
        private int _offset;

        internal int Remaining => data.Length - _offset;

        internal ReadOnlyMemory<byte> Read(int length)
        {
            if (length < 0 || length > Remaining)
            {
                throw new InvalidDataException("PHY section ends inside a required field.");
            }

            ReadOnlyMemory<byte> result = data.Slice(_offset, length);
            _offset += length;
            return result;
        }

        internal uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Read(4).Span);

        internal int ReadCount()
        {
            uint count = ReadUInt32();
            if (count > int.MaxValue)
            {
                throw new InvalidDataException("PHY element count exceeds supported bounds.");
            }

            return (int)count;
        }

        internal ReadOnlyMemory<byte> ReadItems(int count, int stride)
        {
            if (count > Remaining / stride)
            {
                throw new InvalidDataException("PHY element array exceeds its section boundary.");
            }

            return Read(count * stride);
        }
    }
}
