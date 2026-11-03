using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace OpenConquer.Content.Wdf;

/// <summary>
/// Computes the WDF virtual path hash used to resolve archive entries.
/// </summary>
internal static class WdfPathHash
{
    private const int BufferDwordCount = 64;
    private const int BufferLength = BufferDwordCount * sizeof(uint);
    private const uint FirstSentinel = 0x9BE74448;
    private const uint SecondSentinel = 0x66F42C48;
    private const uint InitialAccumulator = 0xF4FA8928;
    private const uint InitialLeft = 0x37A8470E;
    private const uint InitialRight = 0x7758B42B;
    private const uint XorSeed = 0x267B0B11;

    public static uint Compute(string contentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentPath);

        Span<byte> normalizedBytes = stackalloc byte[BufferLength];
        normalizedBytes.Clear();

        // WDF path hashing consumes at most the first 256 normalized bytes.
        int length = Math.Min(contentPath.Length, normalizedBytes.Length);

        for (int index = 0; index < length; index++)
        {
            char value = contentPath[index];

            normalizedBytes[index] = value switch
            {
                >= 'A' and <= 'Z' => (byte)(value + ('a' - 'A')),
                '\\' => (byte)'/',
                _ => unchecked((byte)value),
            };
        }

        return ComputeNormalized(normalizedBytes);
    }

    private static uint ComputeNormalized(ReadOnlySpan<byte> normalizedBytes)
    {
        Span<uint> values = stackalloc uint[BufferDwordCount + 2];
        int populatedCount = 0;

        for (int index = 0; index < BufferDwordCount; index++)
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(normalizedBytes[(index * sizeof(uint))..]);
            values[index] = value;

            if (value != 0)
            {
                populatedCount++;
            }
        }

        values[populatedCount] = FirstSentinel;
        values[populatedCount + 1] = SecondSentinel;

        uint accumulator = InitialAccumulator;
        uint left = InitialLeft;
        uint right = InitialRight;

        for (int index = 0; index < populatedCount + 2; index++)
        {
            accumulator = RotateLeftOne(accumulator);
            uint mixedSeed = XorSeed ^ accumulator;
            uint value = values[index];

            left ^= value;
            right ^= value;

            uint leftMultiplier = unchecked(((mixedSeed + right) | 0x02040801) & 0xBFEF7FDF);
            ulong leftProduct = (ulong)left * leftMultiplier;
            uint leftLow = (uint)leftProduct;
            uint leftHigh = (uint)(leftProduct >> 32);
            uint multiplyCarry = leftHigh == 0 ? 0u : 1u;
            ulong leftSum = (ulong)leftLow + leftHigh + multiplyCarry;
            uint additionCarry = leftSum > uint.MaxValue ? 1u : 0u;
            uint nextLeft = unchecked((uint)leftSum + additionCarry);

            uint rightMultiplier = unchecked(((mixedSeed + left) | 0x00804021) & 0x7DFEFBFF);
            ulong rightProduct = (ulong)right * rightMultiplier;
            uint rightLow = (uint)rightProduct;
            uint rightHigh = (uint)(rightProduct >> 32);
            bool doubleCarry = (rightHigh & 0x80000000u) != 0;
            uint doubledHigh = unchecked(rightHigh << 1);
            ulong rightSum = (ulong)rightLow + doubledHigh + (doubleCarry ? 1u : 0u);
            uint nextRight = (uint)rightSum;

            if (rightSum > uint.MaxValue)
            {
                nextRight = unchecked(nextRight + 2);
            }

            left = nextLeft;
            right = nextRight;
        }

        return left ^ right;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotateLeftOne(uint value)
    {
        return (value << 1) | (value >> 31);
    }
}
