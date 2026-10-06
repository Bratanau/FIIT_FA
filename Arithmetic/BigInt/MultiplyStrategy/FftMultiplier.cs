using System.Numerics;
using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class FftMultiplier : IMultiplier
{
    private const int ChunkBits = 16;
    private const uint ChunkMask = 0xFFFF;

    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        uint[] resultDigits = MultiplyDigits(a.GetDigits(), b.GetDigits());
        bool isNegative = a.IsNegative ^ b.IsNegative;
        return BetterBigInteger.FromDigits(resultDigits, isNegative);
    }

    internal static uint[] MultiplyDigits(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        left = Trim(left);
        right = Trim(right);

        if (left.Length == 1 && left[0] == 0 || right.Length == 1 && right[0] == 0)
        {
            return [0];
        }

        ushort[] leftChunks = ExpandToChunks(left);
        ushort[] rightChunks = ExpandToChunks(right);

        int resultChunkLength = leftChunks.Length + rightChunks.Length;
        int fftLength = 1;
        while (fftLength < resultChunkLength)
        {
            fftLength <<= 1;
        }

        Complex[] leftSpectrum = new Complex[fftLength];
        Complex[] rightSpectrum = new Complex[fftLength];

        for (int i = 0; i < leftChunks.Length; i++)
        {
            leftSpectrum[i] = new Complex(leftChunks[i], 0);
        }

        for (int i = 0; i < rightChunks.Length; i++)
        {
            rightSpectrum[i] = new Complex(rightChunks[i], 0);
        }

        Transform(leftSpectrum, invert: false);
        Transform(rightSpectrum, invert: false);

        for (int i = 0; i < fftLength; i++)
        {
            leftSpectrum[i] *= rightSpectrum[i];
        }

        Transform(leftSpectrum, invert: true);

        ulong[] chunkValues = new ulong[resultChunkLength];
        ulong carry = 0;
        for (int i = 0; i < resultChunkLength; i++)
        {
            ulong value = (ulong)Math.Round(leftSpectrum[i].Real) + carry;
            chunkValues[i] = value & ChunkMask;
            carry = value >> ChunkBits;
        }

        List<ushort> normalizedChunks = new(resultChunkLength + 4);
        for (int i = 0; i < chunkValues.Length; i++)
        {
            normalizedChunks.Add((ushort)chunkValues[i]);
        }

        while (carry != 0)
        {
            normalizedChunks.Add((ushort)(carry & ChunkMask));
            carry >>= ChunkBits;
        }

        return CollapseChunksToDigits(normalizedChunks.ToArray());
    }

    private static ushort[] ExpandToChunks(ReadOnlySpan<uint> digits)
    {
        ushort[] chunks = new ushort[digits.Length * 2];
        int chunkIndex = 0;

        for (int i = 0; i < digits.Length; i++)
        {
            uint digit = digits[i];
            chunks[chunkIndex++] = (ushort)(digit & ChunkMask);
            chunks[chunkIndex++] = (ushort)(digit >> ChunkBits);
        }

        return TrimChunks(chunks);
    }

    private static uint[] CollapseChunksToDigits(ReadOnlySpan<ushort> chunks)
    {
        ushort[] trimmedChunks = TrimChunks(chunks.ToArray());
        int digitCount = (trimmedChunks.Length + 1) / 2;
        uint[] digits = new uint[digitCount];

        for (int i = 0; i < digitCount; i++)
        {
            uint low = trimmedChunks[i * 2];
            uint high = i * 2 + 1 < trimmedChunks.Length ? trimmedChunks[i * 2 + 1] : 0u;
            digits[i] = low | (high << ChunkBits);
        }

        return Trim(digits);
    }

    private static ushort[] TrimChunks(ushort[] chunks)
    {
        int last = chunks.Length - 1;
        while (last > 0 && chunks[last] == 0)
        {
            last--;
        }

        ushort[] trimmed = new ushort[last + 1];
        Array.Copy(chunks, trimmed, last + 1);
        return trimmed;
    }

    private static uint[] Trim(ReadOnlySpan<uint> digits)
    {
        if (digits.Length == 0)
        {
            return [0];
        }

        int last = digits.Length - 1;
        while (last > 0 && digits[last] == 0)
        {
            last--;
        }

        uint[] trimmed = new uint[last + 1];
        digits[..(last + 1)].CopyTo(trimmed);
        return trimmed;
    }

    private static void Transform(Complex[] values, bool invert)
    {
        int length = values.Length;

        for (int i = 1, j = 0; i < length; i++)
        {
            int bit = length >> 1;
            for (; (j & bit) != 0; bit >>= 1)
            {
                j ^= bit;
            }

            j ^= bit;
            if (i < j)
            {
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        for (int size = 2; size <= length; size <<= 1)
        {
            double angle = 2 * Math.PI / size * (invert ? -1 : 1);
            Complex root = Complex.FromPolarCoordinates(1, angle);

            for (int start = 0; start < length; start += size)
            {
                Complex factor = Complex.One;
                int half = size >> 1;

                for (int offset = 0; offset < half; offset++)
                {
                    Complex even = values[start + offset];
                    Complex odd = values[start + offset + half] * factor;
                    values[start + offset] = even + odd;
                    values[start + offset + half] = even - odd;
                    factor *= root;
                }
            }
        }

        if (!invert)
        {
            return;
        }

        for (int i = 0; i < length; i++)
        {
            values[i] /= length;
        }
    }
}
