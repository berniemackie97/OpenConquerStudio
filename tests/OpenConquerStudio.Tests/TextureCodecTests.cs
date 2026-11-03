using System.Buffers.Binary;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Tests;

public sealed class TextureCodecTests
{
    [Fact]
    public void Transform_FlipsBeforeRotatingNonSquareImage()
    {
        byte[] pixels = Enumerable.Range(1, 6).SelectMany(value => new byte[] { (byte)value, 0, 0, 255 }).ToArray();
        TextureData image = new(3, 2, pixels);
        TextureData edited = image.Transform(flipHorizontal: true, clockwiseQuarterTurns: 1, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, edited.Width);
        Assert.Equal(3, edited.Height);
        Assert.Equal(new byte[] { 6, 3, 5, 2, 4, 1 }, edited.Pixels.ToArray().Where((_, index) => index % 4 == 0).ToArray());
    }

    [Theory]
    [InlineData(0, false, 2, 1, 1, 2)]
    [InlineData(0, true, 2, 1, 2, 1)]
    [InlineData(1, false, 1, 2, 1, 2)]
    [InlineData(2, false, 2, 1, 2, 1)]
    [InlineData(3, false, 1, 2, 2, 1)]
    public void Transform_PreservesKnownPixelOrder(int turns, bool flip, int width, int height, byte first, byte second)
    {
        TextureData original = new(2, 1, new byte[] { 1, 0, 0, 255, 2, 0, 0, 255 });
        TextureData edited = original.Transform(flip, turns, TestContext.Current.CancellationToken);
        Assert.Equal(width, edited.Width);
        Assert.Equal(height, edited.Height);
        Assert.Equal(first, edited.Pixels.Span[0]);
        Assert.Equal(second, edited.Pixels.Span[4]);
        Assert.Equal(1, original.Pixels.Span[0]);
    }

    [Theory]
    [InlineData("magic")]
    [InlineData("version")]
    [InlineData("length")]
    [InlineData("dimension")]
    [InlineData("identity")]
    [InlineData("digest")]
    [InlineData("trailing")]
    [InlineData("truncate")]
    public void Read_RejectsCorruptTransport(string corruption)
    {
        using MemoryStream output = new();
        TextureCodec.Write(output, Guid.NewGuid(), new TextureData(1, 1, new byte[] { 10, 20, 30, 255 }));
        byte[] bytes = output.ToArray();
        switch (corruption)
        {
            case "magic":
                bytes[0] ^= 1;
                break;
            case "version":
                bytes[4] = 2;
                break;
            case "length":
                bytes[32] = 5;
                break;
            case "dimension":
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), int.MaxValue);
                break;
            case "identity":
                bytes.AsSpan(8, 16).Clear();
                break;
            case "digest":
                bytes[^1] ^= 1;
                break;
            case "trailing":
                bytes = [.. bytes, 0];
                break;
            default:
                bytes = bytes[..^1];
                break;
        }

        using MemoryStream input = new(bytes);
        Exception failure = Assert.ThrowsAny<Exception>(() => TextureCodec.Read(input));
        Assert.True(failure is IOException or InvalidDataException, failure.ToString());
    }

    [Fact]
    public void Decode_HasNoEditorOrCompatibilityDependency()
    {
        string[] dependencies = typeof(TextureCodec).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
        Assert.DoesNotContain(dependencies, name => name.StartsWith("Avalonia", StringComparison.Ordinal) || name.StartsWith("OpenConquer.", StringComparison.Ordinal) || name.Contains("Authoring", StringComparison.Ordinal));
    }
}
