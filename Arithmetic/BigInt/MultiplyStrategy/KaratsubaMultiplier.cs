using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class KaratsubaMultiplier : IMultiplier
{
    private const int BaseCaseDigits = 32;

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

        int maxLength = Math.Max(left.Length, right.Length);
        if (maxLength <= BaseCaseDigits)
        {
            return SimpleMultiplier.MultiplyDigits(left, right);
        }

        int split = (maxLength + 1) / 2;

        ReadOnlySpan<uint> lowLeft = left[..Math.Min(split, left.Length)];
        ReadOnlySpan<uint> highLeft = left[Math.Min(split, left.Length)..];
        ReadOnlySpan<uint> lowRight = right[..Math.Min(split, right.Length)];
        ReadOnlySpan<uint> highRight = right[Math.Min(split, right.Length)..];

        uint[] z0 = MultiplyDigits(lowLeft, lowRight);
        uint[] z2 = MultiplyDigits(highLeft, highRight);

        uint[] sumLeft = Add(lowLeft, highLeft);
        uint[] sumRight = Add(lowRight, highRight);
        uint[] z1 = MultiplyDigits(sumLeft, sumRight);

        uint[] middle = Subtract(Subtract(z1, z2), z0);

        uint[] result = new uint[Math.Max(z2.Length + (split * 2), Math.Max(middle.Length + split, z0.Length)) + 1];
        AddShifted(result, z0, 0);
        AddShifted(result, middle, split);
        AddShifted(result, z2, split * 2);

        return Trim(result);
    }

    private static uint[] Add(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        int length = Math.Max(left.Length, right.Length);
        uint[] result = new uint[length + 1];
        ulong carry = 0;

        for (int i = 0; i < length; i++)
        {
            ulong sum = carry;
            if (i < left.Length)
            {
                sum += left[i];
            }

            if (i < right.Length)
            {
                sum += right[i];
            }

            result[i] = (uint)sum;
            carry = sum >> 32;
        }

        result[length] = (uint)carry;
        return Trim(result);
    }

    private static uint[] Subtract(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        uint[] result = new uint[left.Length];
        long borrow = 0;

        for (int i = 0; i < left.Length; i++)
        {
            long difference = (long)left[i] - (i < right.Length ? right[i] : 0) - borrow;
            if (difference < 0)
            {
                difference += 1L << 32;
                borrow = 1;
            }
            else
            {
                borrow = 0;
            }

            result[i] = (uint)difference;
        }

        return Trim(result);
    }

    private static void AddShifted(Span<uint> destination, ReadOnlySpan<uint> source, int offset)
    {
        ulong carry = 0;
        int index = offset;

        for (int i = 0; i < source.Length; i++, index++)
        {
            ulong sum = destination[index] + (ulong)source[i] + carry;
            destination[index] = (uint)sum;
            carry = sum >> 32;
        }

        while (carry != 0)
        {
            ulong sum = destination[index] + carry;
            destination[index] = (uint)sum;
            carry = sum >> 32;
            index++;
        }
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
}
