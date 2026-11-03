using System.Text.Json;

namespace OpenConquer.Content.Login;

public sealed class LoginLoadingTimeline
{
    private const int FirstBarFrame = 18;
    private const int LastFrame = 100;
    private readonly double[] _fillX = new double[LastFrame + 1];
    private readonly double[] _headX = new double[LastFrame + 1];

    private LoginLoadingTimeline()
    {
    }

    public static LoginLoadingTimeline Load(IClientContentSource source)
    {
        byte[] data = ContentReader.ReadRequiredBytes(source, LoginLoadingContentPaths.Timeline,
            ContentLookupMode.LooseOnly, 128 * 1024);
        return Parse(data);
    }

    public static LoginLoadingTimeline Parse(ReadOnlyMemory<byte> data)
    {
        using JsonDocument document = JsonDocument.Parse(data);
        JsonElement clip = document.RootElement.GetProperty("394");
        if (clip.GetProperty("frames").GetInt32() != LastFrame)
        {
            throw new InvalidDataException("The loading animation must have 100 frames.");
        }

        LoginLoadingTimeline timeline = new();
        bool[] fill = new bool[LastFrame + 1];
        bool[] head = new bool[LastFrame + 1];
        foreach (JsonElement evt in clip.GetProperty("events").EnumerateArray())
        {
            if (evt.GetArrayLength() != 2 || evt[1].ValueKind != JsonValueKind.Object ||
                !evt[1].TryGetProperty("depth", out JsonElement depth) ||
                !evt[1].TryGetProperty("m", out JsonElement matrix) ||
                (depth.GetInt32() != 4 && depth.GetInt32() != 9))
            {
                continue;
            }

            int frame = evt[0].GetInt32();
            if (frame < FirstBarFrame || frame > LastFrame || matrix.GetArrayLength() != 6)
            {
                throw new InvalidDataException("The loading bar contains an invalid placement.");
            }

            double x = matrix[4].GetDouble();
            if (!double.IsFinite(x))
            {
                throw new InvalidDataException("The loading bar contains a non-finite position.");
            }

            bool isFill = depth.GetInt32() == 4;
            bool[] found = isFill ? fill : head;
            if (found[frame])
            {
                throw new InvalidDataException("The loading bar contains a repeated placement.");
            }

            found[frame] = true;
            (isFill ? timeline._fillX : timeline._headX)[frame] = x;
        }

        for (int frame = FirstBarFrame; frame <= LastFrame; frame++)
        {
            if (!fill[frame] || !head[frame])
            {
                throw new InvalidDataException($"The loading bar is missing a placement at frame {frame}.");
            }
        }

        return timeline;
    }

    public (double FillX, double HeadX) PositionAt(int frame)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(frame, FirstBarFrame);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(frame, LastFrame);
        return (_fillX[frame], _headX[frame]);
    }
}
