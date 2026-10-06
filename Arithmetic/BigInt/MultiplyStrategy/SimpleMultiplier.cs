using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class SimpleMultiplier : IMultiplier
{
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
        if (IsZero(left) || IsZero(right))
        {
            return [0];
        }

        uint[] result = new uint[left.Length + right.Length];

        for (int i = 0; i < left.Length; i++)
        {
            ulong carry = 0;
            ulong leftDigit = left[i];

            for (int j = 0; j < right.Length; j++)
            {
                ulong current = result[i + j];
                ulong product = leftDigit * right[j] + current + carry;
                result[i + j] = (uint)product;
                carry = product >> 32;
            }

            int index = i + right.Length;
            while (carry != 0)
            {
                ulong sum = result[index] + carry;
                result[index] = (uint)sum;
                carry = sum >> 32;
                index++;
            }
        }

        return TrimLeadingZeros(result);
    }

    private static bool IsZero(ReadOnlySpan<uint> digits)
    {
        foreach (uint digit in digits)
        {
            if (digit != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static uint[] TrimLeadingZeros(uint[] digits)
    {
        int last = digits.Length - 1;
        while (last > 0 && digits[last] == 0)
        {
            last--;
        }

        if (last == digits.Length - 1)
        {
            return digits;
        }

        uint[] trimmed = new uint[last + 1];
        Array.Copy(digits, trimmed, last + 1);
        return trimmed;
    }
}
