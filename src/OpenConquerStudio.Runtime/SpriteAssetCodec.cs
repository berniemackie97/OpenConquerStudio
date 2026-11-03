using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace OpenConquerStudio.Runtime;

/// <summary>Versioned sprite resources. Digests detect corruption, not publisher authenticity.</summary>
public static class SpriteAssetCodec
{
    public const int MaximumLength = 2 * 1024 * 1024;
    private static readonly UTF8Encoding s_utf8 = new(false, true);

    public static void WriteAtlas(Stream output, SpriteAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        Write(output, 1, atlas.Id, writer =>
        {
            WriteId(writer, atlas.TextureId);
            writer.Write(atlas.Sprites.Length);
            foreach (SpriteRegion region in atlas.Sprites)
            {
                WriteId(writer, region.Id);
                byte[] name = s_utf8.GetBytes(region.Name);
                writer.Write((ushort)name.Length);
                writer.Write(name);
                writer.Write(region.X);
                writer.Write(region.Y);
                writer.Write(region.Width);
                writer.Write(region.Height);
                writer.Write(region.PivotX);
                writer.Write(region.PivotY);
            }
        });
    }

    public static SpriteAtlas ReadAtlas(Stream input)
    {
        (Guid id, byte[] payload) = Read(input, 1);
        using BinaryReader reader = new(new MemoryStream(payload, writable: false), s_utf8);
        Guid texture = ReadId(reader);
        int count = ReadCount(reader, SpriteAtlas.MaximumSprites);
        ImmutableArray<SpriteRegion>.Builder sprites = ImmutableArray.CreateBuilder<SpriteRegion>(count);
        for (int i = 0; i < count; i++)
        {
            Guid sprite = ReadId(reader);
            int length = reader.ReadUInt16();
            if (length is < 1 or > 512)
            {
                throw new InvalidDataException("Compiled sprite name exceeds its UTF-8 limit.");
            }

            byte[] encoded = new byte[length];
            reader.BaseStream.ReadExactly(encoded);
            string name = s_utf8.GetString(encoded);
            sprites.Add(new SpriteRegion(sprite, name, reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));
        }

        RequireEnd(reader);
        return new SpriteAtlas(id, texture, sprites.MoveToImmutable());
    }

    public static void WriteAnimation(Stream output, SpriteAnimation animation)
    {
        ArgumentNullException.ThrowIfNull(animation);
        Write(output, 2, animation.Id, writer =>
        {
            writer.Write(animation.TicksPerSecond);
            writer.Write(animation.Repeat ? 1 : 0);
            writer.Write(animation.Frames.Length);
            foreach (AnimationFrame frame in animation.Frames)
            {
                WriteId(writer, frame.Id);
                WriteId(writer, frame.AtlasId);
                WriteId(writer, frame.SpriteId);
                writer.Write(frame.DurationTicks);
            }
        });
    }

    public static SpriteAnimation ReadAnimation(Stream input)
    {
        (Guid id, byte[] payload) = Read(input, 2);
        using BinaryReader reader = new(new MemoryStream(payload, writable: false), s_utf8);
        int timebase = reader.ReadInt32();
        int loop = reader.ReadInt32();
        if (loop is not (0 or 1))
        {
            throw new InvalidDataException("Compiled animation loop mode is invalid.");
        }

        int count = ReadCount(reader, SpriteAnimation.MaximumFrames);
        ImmutableArray<AnimationFrame>.Builder frames = ImmutableArray.CreateBuilder<AnimationFrame>(count);
        for (int i = 0; i < count; i++)
        {
            frames.Add(new AnimationFrame(ReadId(reader), ReadId(reader), ReadId(reader), reader.ReadInt32()));
        }

        RequireEnd(reader);
        return new SpriteAnimation(id, timebase, loop == 1, frames.MoveToImmutable());
    }

    private static void Write(Stream output, int kind, Guid id, Action<BinaryWriter> encode)
    {
        ArgumentNullException.ThrowIfNull(output);
        using MemoryStream payload = new();
        using (BinaryWriter writer = new(payload, s_utf8, leaveOpen: true))
        {
            encode(writer);
        }

        if (payload.Length > MaximumLength - 64)
        {
            throw new InvalidDataException("Compiled sprite resource exceeds its byte limit.");
        }

        Span<byte> header = stackalloc byte[64];
        "OCAS"u8.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], 1);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], kind);
        id.TryWriteBytes(header[12..28], bigEndian: true, out _);
        BinaryPrimitives.WriteInt32LittleEndian(header[28..], (int)payload.Length);
        SHA256.HashData(payload.GetBuffer().AsSpan(0, (int)payload.Length), header[32..]);
        output.Write(header);
        payload.Position = 0;
        payload.CopyTo(output);
    }

    private static (Guid Id, byte[] Payload) Read(Stream input, int kind)
    {
        ArgumentNullException.ThrowIfNull(input);
        Span<byte> header = stackalloc byte[64];
        input.ReadExactly(header);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header[28..]);
        Guid id = new(header[12..28], bigEndian: true);
        if (!header[..4].SequenceEqual("OCAS"u8) || BinaryPrimitives.ReadInt32LittleEndian(header[4..]) != 1 ||
            BinaryPrimitives.ReadInt32LittleEndian(header[8..]) != kind || id == Guid.Empty || length is < 1 or > MaximumLength - 64)
        {
            throw new InvalidDataException("Compiled sprite resource header is invalid or unsupported.");
        }

        byte[] payload = new byte[length];
        input.ReadExactly(payload);
        if (input.ReadByte() != -1 || !SHA256.HashData(payload).AsSpan().SequenceEqual(header[32..]))
        {
            throw new InvalidDataException("Compiled sprite resource digest or byte coverage is invalid.");
        }

        return (id, payload);
    }

    private static void WriteId(BinaryWriter writer, Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes, bigEndian: true, out _);
        writer.Write(bytes);
    }

    private static Guid ReadId(BinaryReader reader)
    {
        Span<byte> bytes = stackalloc byte[16];
        reader.BaseStream.ReadExactly(bytes);
        return new Guid(bytes, bigEndian: true);
    }

    private static int ReadCount(BinaryReader reader, int maximum)
    {
        int count = reader.ReadInt32();
        if (count < 1 || count > maximum)
        {
            throw new InvalidDataException("Compiled sprite resource count exceeds its limit.");
        }

        return count;
    }

    private static void RequireEnd(BinaryReader reader)
    {
        if (reader.BaseStream.Position != reader.BaseStream.Length)
        {
            throw new InvalidDataException("Compiled sprite resource contains trailing fields.");
        }
    }
}
