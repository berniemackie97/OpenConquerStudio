using System.Buffers.Binary;
using System.Numerics;

namespace OpenConquer.Content.Mesh;

public static class C3MotionReader
{
    private const uint MotionIdentifier = 0x49544F4D;
    private const uint KKey = 0x59454B4B;
    private const uint ZKey = 0x59454B5A;
    private const uint XKey = 0x59454B58;
    private const int MaximumBones = 1000;

    public static C3Motion Read(C3MeshSection section)
    {
        if (section.Identifier != MotionIdentifier)
        {
            throw new InvalidDataException("The section is not a MOTI motion.");
        }

        return Read(section.Payload.Span);
    }

    public static C3Motion Read(ReadOnlySpan<byte> payload)
    {
        if (payload.Length > C3MeshFile.MaximumFileBytes)
        {
            throw new InvalidDataException("MOTI payload exceeds the content size limit.");
        }

        PayloadReader reader = new(payload);
        uint boneCount = reader.ReadUInt32();
        if (boneCount > MaximumBones)
        {
            throw new InvalidDataException("MOTI bone count exceeds the supported limit.");
        }

        uint frameCount = reader.ReadUInt32();
        uint encoding = reader.ReadUInt32();
        bool keyed = encoding is KKey or ZKey or XKey;
        uint count = keyed ? reader.ReadUInt32() : frameCount;
        if (!keyed)
        {
            reader.Seek(-sizeof(uint));
        }

        int indexBytes = encoding == KKey ? sizeof(uint) : keyed ? sizeof(ushort) : 0;
        int matrixBytes = encoding == ZKey ? 28 : encoding == XKey ? 48 : 64;
        long required = (long)count * (indexBytes + boneCount * matrixBytes) + sizeof(int);
        long totalMatrices = (long)count * boneCount;
        if (required > reader.Remaining || count > int.MaxValue || totalMatrices > int.MaxValue)
        {
            throw new InvalidDataException("MOTI keyframe data exceeds the section boundary.");
        }

        int keyCount = (int)count;
        int bones = (int)boneCount;
        uint[] frameIndices = new uint[keyCount];
        Matrix4x4[] matrices = new Matrix4x4[(int)totalMatrices];

        if (keyed)
        {
            ReadKeyedFrames(ref reader, encoding, bones, frameIndices, matrices);
        }
        else
        {
            ReadImplicitFrames(ref reader, bones, frameIndices, matrices);
        }

        SkipExtraSection(ref reader, frameCount);

        if (reader.Remaining != 0)
        {
            throw new InvalidDataException("MOTI payload has an unexpected trailing region.");
        }

        return new C3Motion(boneCount, frameCount, encoding, frameIndices, matrices);
    }

    private static void ReadKeyedFrames(ref PayloadReader reader, uint encoding, int bones,
        uint[] indices, Matrix4x4[] matrices)
    {
        for (int key = 0; key < indices.Length; key++)
        {
            indices[key] = encoding == KKey ? reader.ReadUInt32() : reader.ReadUInt16();
            for (int bone = 0; bone < bones; bone++)
            {
                matrices[key * bones + bone] = encoding switch
                {
                    KKey => ReadMatrix(reader.Read(64)),
                    ZKey => ReadQuaternionMatrix(reader.Read(28)),
                    XKey => ReadAffineMatrix(reader.Read(48)),
                    _ => throw new InvalidDataException("Unsupported MOTI key encoding.")
                };
            }
        }
    }

    private static void ReadImplicitFrames(ref PayloadReader reader, int bones,
        uint[] indices, Matrix4x4[] matrices)
    {
        for (int key = 0; key < indices.Length; key++)
        {
            indices[key] = (uint)key;
        }

        for (int bone = 0; bone < bones; bone++)
        {
            for (int key = 0; key < indices.Length; key++)
            {
                matrices[key * bones + bone] = ReadMatrix(reader.Read(64));
            }
        }
    }

    private static void SkipExtraSection(ref PayloadReader reader, uint frameCount)
    {
        int extraSectionCount = reader.ReadInt32();
        if (extraSectionCount != 0)
        {
            int delta = unchecked(unchecked((int)frameCount) * extraSectionCount << 2);
            reader.Seek(delta);
        }
    }

    private static Matrix4x4 ReadQuaternionMatrix(ReadOnlySpan<byte> bytes)
    {
        float x = Float(bytes, 0);
        float y = Float(bytes, 4);
        float z = Float(bytes, 8);
        float w = Float(bytes, 12);
        float xx = x * x;
        float yy = y * y;
        float zz = z * z;
        float xy = x * y;
        float xz = x * z;
        float yz = y * z;
        float xw = x * w;
        float yw = y * w;
        float zw = z * w;
        return new Matrix4x4(
            1f - 2f * (yy + zz), 2f * (xy + zw), 2f * (xz - yw), 0f,
            2f * (xy - zw), 1f - 2f * (xx + zz), 2f * (yz + xw), 0f,
            2f * (xz + yw), 2f * (yz - xw), 1f - 2f * (xx + yy), 0f,
            Float(bytes, 16), Float(bytes, 20), Float(bytes, 24), 1f);
    }

    private static Matrix4x4 ReadAffineMatrix(ReadOnlySpan<byte> bytes) => new(
        Float(bytes, 0), Float(bytes, 4), Float(bytes, 8), 0f,
        Float(bytes, 12), Float(bytes, 16), Float(bytes, 20), 0f,
        Float(bytes, 24), Float(bytes, 28), Float(bytes, 32), 0f,
        Float(bytes, 36), Float(bytes, 40), Float(bytes, 44), 1f);

    private static Matrix4x4 ReadMatrix(ReadOnlySpan<byte> bytes) => new(
        Float(bytes, 0), Float(bytes, 4), Float(bytes, 8), Float(bytes, 12),
        Float(bytes, 16), Float(bytes, 20), Float(bytes, 24), Float(bytes, 28),
        Float(bytes, 32), Float(bytes, 36), Float(bytes, 40), Float(bytes, 44),
        Float(bytes, 48), Float(bytes, 52), Float(bytes, 56), Float(bytes, 60));

    private static float Float(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadSingleLittleEndian(bytes[offset..]);

    private ref struct PayloadReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _offset;

        internal PayloadReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _offset = 0;
        }

        internal int Remaining => _data.Length - _offset;

        internal ReadOnlySpan<byte> Read(int length)
        {
            if (length < 0 || length > Remaining)
            {
                throw new InvalidDataException("MOTI payload ends inside a required field.");
            }

            ReadOnlySpan<byte> slice = _data.Slice(_offset, length);
            _offset += length;
            return slice;
        }

        internal uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Read(4));

        internal ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(Read(2));

        internal int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(Read(4));

        internal void Seek(int delta)
        {
            long next = (long)_offset + delta;
            if (next < 0 || next > _data.Length)
            {
                throw new InvalidDataException("MOTI extra-section seek exceeds its payload boundary.");
            }

            _offset = (int)next;
        }
    }
}
