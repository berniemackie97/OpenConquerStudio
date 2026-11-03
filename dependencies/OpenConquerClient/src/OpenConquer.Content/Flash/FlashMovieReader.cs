using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenConquer.Content.Flash;

/// <summary>
/// Parses the baked movie format (format 1) strictly: every reference must resolve, every value must be in range,
/// and anything unknown is rejected rather than ignored.
/// </summary>
public static partial class FlashMovieReader
{
    private const int MaximumRasterExtent = 4096;
    private const int MaximumDepth = 65535;

    public static FlashMovie Read(ReadOnlySpan<byte> json, string assetDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetDirectory);

        using JsonDocument document = JsonDocument.Parse(json.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
        JsonElement root = document.RootElement;

        if (Int(root, "format") != 1)
        {
            throw Invalid("unsupported format");
        }

        string name = String(root, "movie");
        float frameRate = Float(root, "frameRate");
        int width = checked((int)Float(root, "width"));
        int height = checked((int)Float(root, "height"));

        if (!(frameRate > 0f) || width <= 0 || height <= 0)
        {
            throw Invalid("stage");
        }

        List<FlashRaster> rasters = [];
        foreach (JsonElement raster in Array(root, "rasters"))
        {
            string path = String(raster, "path");
            int rasterWidth = Int(raster, "width");
            int rasterHeight = Int(raster, "height");
            string sha256 = String(raster, "sha256");

            if (!RasterPath().IsMatch(path) || rasterWidth is <= 0 or > MaximumRasterExtent ||
                rasterHeight is <= 0 or > MaximumRasterExtent || !Sha256().IsMatch(sha256))
            {
                throw Invalid($"raster {rasters.Count}");
            }

            rasters.Add(new FlashRaster(path, rasterWidth, rasterHeight, sha256));
        }

        JsonElement timelines = Object(root, "timelines");
        Dictionary<int, FlashCharacter> characters = [];

        foreach (JsonProperty property in Object(root, "characters").EnumerateObject())
        {
            int id = ParseId(property.Name);
            characters.Add(id, ReadCharacter(id, property.Value, timelines, rasters.Count));
        }

        foreach (JsonProperty timeline in timelines.EnumerateObject())
        {
            if (timeline.Name != "root" && !(characters.TryGetValue(ParseId(timeline.Name), out FlashCharacter? owner) && owner is FlashSpriteCharacter))
            {
                throw Invalid($"timeline '{timeline.Name}' has no sprite");
            }
        }

        FlashTimeline rootTimeline = ReadTimeline(Object(timelines, "root"));

        Validate(characters, rootTimeline, "root");
        foreach (FlashCharacter character in characters.Values)
        {
            switch (character)
            {
                case FlashSpriteCharacter sprite:
                    Validate(characters, sprite.Timeline, sprite.Id.ToString(CultureInfo.InvariantCulture));
                    break;
                case FlashButtonCharacter button:
                    foreach (FlashButtonRecord record in button.Records)
                    {
                        _ = Resolve(characters, record.CharacterId, $"button {button.Id}");
                    }

                    break;
            }
        }

        return new FlashMovie(name, assetDirectory, frameRate, width, height, rasters, characters, rootTimeline);
    }

    private static FlashCharacter ReadCharacter(int id, JsonElement element, JsonElement timelines, int rasterCount)
    {
        string kind = String(element, "kind");

        return kind switch
        {
            "shape" or "text" => new FlashRasterCharacter(id, ReadPieces(id, element, rasterCount), kind == "text"),
            "morph" => new FlashMorphCharacter(id, Float(element, "x"), Float(element, "y"), ReadMorphRasters(element, rasterCount)),
            "video" => ReadVideo(id, element, rasterCount),
            "sprite" => new FlashSpriteCharacter(id, ReadTimeline(Object(timelines, id.ToString(CultureInfo.InvariantCulture)))),
            "edittext" => ReadEditText(id, element),
            "button" => new FlashButtonCharacter(id, ReadButtonRecords(element)),
            _ => throw Invalid($"character {id} kind '{kind}'"),
        };
    }

    private static FlashRasterPiece[] ReadPieces(int id, JsonElement element, int rasterCount)
    {
        if (!element.TryGetProperty("pieces", out JsonElement pieces) || pieces.ValueKind != JsonValueKind.Array ||
            pieces.GetArrayLength() is < 1 or > 16)
        {
            throw Invalid($"character {id} pieces");
        }

        return [.. pieces.EnumerateArray().Select(piece => new FlashRasterPiece(RasterIndex(piece, "raster", rasterCount),
            Float(piece, "x"), Float(piece, "y"), String(piece, "snap") switch
            {
                "center" => FlashPixelSnap.PixelCenter,
                "corner" => FlashPixelSnap.PixelCorner,
                string snap => throw Invalid($"character {id} snap '{snap}'"),
            }))];
    }

    private static FlashVideoCharacter ReadVideo(int id, JsonElement element, int rasterCount)
    {
        int width = Int(element, "width");
        int height = Int(element, "height");
        Dictionary<int, int> frames = ReadRatioRasters(Object(element, "frames"), rasterCount, "video frame");
        if (width is < 1 or > 4096 || height is < 1 or > 4096 || !frames.ContainsKey(0))
        {
            throw Invalid($"video {id}");
        }

        return new FlashVideoCharacter(id, width, height, frames);
    }

    private static Dictionary<int, int> ReadMorphRasters(JsonElement element, int rasterCount)
    {
        Dictionary<int, int> rasters = ReadRatioRasters(Object(element, "rasters"), rasterCount, "morph raster");
        return rasters.Count != 0 ? rasters : throw Invalid("morph without rasters");
    }

    private static Dictionary<int, int> ReadRatioRasters(JsonElement map, int rasterCount, string what)
    {
        Dictionary<int, int> rasters = [];
        foreach (JsonProperty property in map.EnumerateObject())
        {
            if (!int.TryParse(property.Name, NumberStyles.None, CultureInfo.InvariantCulture, out int ratio) ||
                ratio > ushort.MaxValue || property.Value.ValueKind != JsonValueKind.Number ||
                !property.Value.TryGetInt32(out int raster) || raster < 0 || raster >= rasterCount)
            {
                throw Invalid(what);
            }

            rasters.Add(ratio, raster);
        }

        return rasters;
    }

    private static FlashEditTextCharacter ReadEditText(int id, JsonElement element)
    {
        JsonElement bounds = ArrayElement(element, "bounds");
        JsonElement color = ArrayElement(element, "color");

        if (bounds.GetArrayLength() != 4 || color.GetArrayLength() != 4)
        {
            throw Invalid($"edit text {id}");
        }

        int alignment = Int(element, "align");
        if (!Enum.IsDefined((FlashTextAlignment)alignment))
        {
            throw Invalid($"edit text {id} alignment");
        }

        return new FlashEditTextCharacter(id,
            new FlashBounds(bounds[0].GetSingle(), bounds[1].GetSingle(), bounds[2].GetSingle(), bounds[3].GetSingle()),
            String(element, "font"), Float(element, "fontHeight"),
            color[0].GetByte(), color[1].GetByte(), color[2].GetByte(), color[3].GetByte(), (FlashTextAlignment)alignment,
            Float(element, "leftMargin"), Float(element, "rightMargin"), Float(element, "indent"), Float(element, "leading"),
            Int(element, "maxLength"), String(element, "variable"), String(element, "text"),
            Bool(element, "readOnly"), Bool(element, "password"), Bool(element, "multiline"), Bool(element, "wordWrap"),
            Bool(element, "html"), Bool(element, "noSelect"), Bool(element, "border"), Bool(element, "autoSize"));
    }

    private static List<FlashButtonRecord> ReadButtonRecords(JsonElement element)
    {
        List<FlashButtonRecord> records = [];
        foreach (JsonElement record in Array(element, "records"))
        {
            FlashButtonStates states = FlashButtonStates.None;
            foreach (JsonElement state in Array(record, "states"))
            {
                states |= state.GetString() switch
                {
                    "up" => FlashButtonStates.Up,
                    "over" => FlashButtonStates.Over,
                    "down" => FlashButtonStates.Down,
                    "hit" => FlashButtonStates.Hit,
                    _ => throw Invalid("button state"),
                };
            }

            records.Add(new FlashButtonRecord(Int(record, "c"), Depth(record, "d"), states, Matrix(record), ColorTransform(record)));
        }

        return records;
    }

    private static FlashTimeline ReadTimeline(JsonElement element)
    {
        List<IReadOnlyList<FlashPlacement>> frames = [];
        foreach (JsonElement frame in Array(element, "frames"))
        {
            List<FlashPlacement> placements = [];
            int previousDepth = 0;

            foreach (JsonElement placement in frame.EnumerateArray())
            {
                int depth = Depth(placement, "d");
                if (depth <= previousDepth)
                {
                    throw Invalid("display list depths must ascend");
                }

                int? clip = placement.TryGetProperty("clip", out JsonElement clipElement) ? clipElement.GetInt32() : null;
                int? ratio = placement.TryGetProperty("r", out JsonElement ratioElement) ? ratioElement.GetInt32() : null;
                string? name = placement.TryGetProperty("n", out JsonElement nameElement) ? nameElement.GetString() : null;
                int placedFrame = Int(placement, "p");

                if (clip is { } clipDepth && (clipDepth < depth || clipDepth > MaximumDepth) || ratio is < 0 or > ushort.MaxValue ||
                    placedFrame < 1 || placedFrame > frames.Count + 1 || name is "")
                {
                    throw Invalid($"placement at depth {depth}");
                }

                placements.Add(new FlashPlacement(depth, Int(placement, "c"), name, Matrix(placement), ColorTransform(placement), clip, ratio, placedFrame));
                previousDepth = depth;
            }

            frames.Add(placements);
        }

        Dictionary<string, int> labels = [];
        foreach (JsonProperty label in Object(element, "labels").EnumerateObject())
        {
            int frame = label.Value.GetInt32();
            if (frame < 1 || frame > frames.Count)
            {
                throw Invalid($"label '{label.Name}'");
            }

            labels.Add(label.Name, frame);
        }

        return new FlashTimeline(frames, labels);
    }

    private static FlashCharacter Resolve(Dictionary<int, FlashCharacter> characters, int id, string owner) =>
        characters.TryGetValue(id, out FlashCharacter? character) ? character : throw Invalid($"{owner}: character {id} is missing");

    private static void Validate(Dictionary<int, FlashCharacter> characters, FlashTimeline timeline, string owner)
    {
        foreach (IReadOnlyList<FlashPlacement> frame in timeline.Frames)
        {
            foreach (FlashPlacement placement in frame)
            {
                FlashCharacter character = Resolve(characters, placement.CharacterId, $"timeline {owner}");
                if (placement.Ratio is { } ratio && character is not FlashVideoCharacter &&
                    !(character is FlashMorphCharacter morph && morph.RasterByRatio.ContainsKey(ratio)))
                {
                    throw Invalid($"timeline {owner}: ratio {ratio} of character {placement.CharacterId} has no raster");
                }

                if (character is FlashMorphCharacter && placement.Ratio is null)
                {
                    throw Invalid($"timeline {owner}: morph {placement.CharacterId} without a ratio");
                }
            }
        }
    }

    private static FlashMatrix Matrix(JsonElement element)
    {
        if (!element.TryGetProperty("m", out JsonElement matrix))
        {
            return FlashMatrix.Identity;
        }

        if (matrix.GetArrayLength() != 6)
        {
            throw Invalid("matrix");
        }

        FlashMatrix value = new(matrix[0].GetSingle(), matrix[1].GetSingle(), matrix[2].GetSingle(), matrix[3].GetSingle(),
            matrix[4].GetSingle(), matrix[5].GetSingle());
        return float.IsFinite(value.A) && float.IsFinite(value.B) && float.IsFinite(value.C) && float.IsFinite(value.D) &&
               float.IsFinite(value.Tx) && float.IsFinite(value.Ty) ? value : throw Invalid("matrix");
    }

    private static FlashColorTransform ColorTransform(JsonElement element)
    {
        if (!element.TryGetProperty("cx", out JsonElement terms))
        {
            return FlashColorTransform.Identity;
        }

        if (terms.GetArrayLength() != 8)
        {
            throw Invalid("color transform");
        }

        Span<int> values = stackalloc int[8];
        for (int index = 0; index < 8; index++)
        {
            values[index] = terms[index].GetInt32();
            if (values[index] is < short.MinValue or > short.MaxValue)
            {
                throw Invalid("color transform");
            }
        }

        return new FlashColorTransform(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7]);
    }

    private static int RasterIndex(JsonElement element, string name, int rasterCount)
    {
        int index = Int(element, name);
        return index >= 0 && index < rasterCount ? index : throw Invalid($"raster index {index}");
    }

    private static int Depth(JsonElement element, string name)
    {
        int depth = Int(element, name);
        return depth is >= 1 and <= MaximumDepth ? depth : throw Invalid($"depth {depth}");
    }

    private static int ParseId(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int id) && id is > 0 and <= ushort.MaxValue
            ? id
            : throw Invalid($"character id '{text}'");

    private static JsonElement Object(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Object ? value : throw Invalid($"'{name}'");

    private static JsonElement.ArrayEnumerator Array(JsonElement element, string name) => ArrayElement(element, name).EnumerateArray();

    private static JsonElement ArrayElement(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Array ? value : throw Invalid($"'{name}'");

    private static string String(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Invalid($"'{name}'");

    private static int Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)
            ? number
            : throw Invalid($"'{name}'");

    private static float Float(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && float.IsFinite(value.GetSingle())
            ? value.GetSingle()
            : throw Invalid($"'{name}'");

    private static bool Bool(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : throw Invalid($"'{name}'");

    private static InvalidDataException Invalid(string what) => new($"Baked Flash movie is invalid: {what}.");

    [GeneratedRegex("^r[0-9]{1,5}\\.rgba\\.br$", RegexOptions.CultureInvariant)]
    private static partial Regex RasterPath();

    [GeneratedRegex("^[0-9A-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256();
}
