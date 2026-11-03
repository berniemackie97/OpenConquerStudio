using System.Buffers.Binary;
using OpenConquer.Content.Images;
using OpenConquer.Content.Media;

namespace OpenConquer.Content.Login;

/// <summary>
/// start.dat sprite 22, the 602-frame flight of the sprite 21 birds, packed offline into one atlas with a
/// per-frame source rectangle and stage position.
/// </summary>
public static class LoginBirdArtwork
{
    public const int FrameCount = 602;
    public const int FramesPerSecond = 30;

    private const int RecordLength = 24;
    private static readonly EmbeddedImage s_atlas =
        new("Login/Opening/BirdAtlas.rgba.br", 1024, 1024, "30E0EF1BD8685015360C4827CC5DAA4A5560117E06A8596A064AD17557FAD624");
    private static readonly BirdFrame[] s_frames = LoadFrames();

    /// <summary>One frame of the flight; a frame with no visible bird has an empty rectangle.</summary>
    public readonly record struct BirdFrame(int AtlasX, int AtlasY, int Width, int Height, int StageX, int StageY)
    {
        public bool IsVisible => Width > 0 && Height > 0;
    }

    public static BirdFrame FrameAt(int frame) =>
        (uint)frame < FrameCount ? s_frames[frame] : throw new ArgumentOutOfRangeException(nameof(frame));

    public static RgbaImage LoadAtlas() => EmbeddedMedia.Load(s_atlas);

    private static BirdFrame[] LoadFrames()
    {
        byte[] table = EmbeddedMedia.Read("Login/Opening/BirdFrames.bin", FrameCount * RecordLength,
            "DDE10F86E3C27EEBEF114E31387DF835B789438B4A730A9BD72C176F61AF596F", compressed: false);
        BirdFrame[] frames = new BirdFrame[FrameCount];

        for (int index = 0; index < frames.Length; index++)
        {
            ReadOnlySpan<byte> record = table.AsSpan(index * RecordLength, RecordLength);
            BirdFrame frame = new(
                BinaryPrimitives.ReadInt32LittleEndian(record),
                BinaryPrimitives.ReadInt32LittleEndian(record[4..]),
                BinaryPrimitives.ReadInt32LittleEndian(record[8..]),
                BinaryPrimitives.ReadInt32LittleEndian(record[12..]),
                BinaryPrimitives.ReadInt32LittleEndian(record[16..]),
                BinaryPrimitives.ReadInt32LittleEndian(record[20..]));

            bool empty = frame.Width == 0 && frame.Height == 0;
            bool insideAtlas = frame.Width > 0 && frame.Height > 0 && frame.AtlasX >= 0 && frame.AtlasY >= 0 &&
                (long)frame.AtlasX + frame.Width <= s_atlas.Width && (long)frame.AtlasY + frame.Height <= s_atlas.Height;
            if (!empty && !insideAtlas)
            {
                throw new InvalidDataException($"Bird frame {index} lies outside its atlas.");
            }

            frames[index] = frame;
        }

        return frames;
    }
}
