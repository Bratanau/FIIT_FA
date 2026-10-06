using System.Runtime.InteropServices;
using Arithmetic.BigInt.Interfaces;
using Arithmetic.BigInt.MultiplyStrategy;

namespace Arithmetic.BigInt;

public sealed class BetterBigInteger : IBigInteger
{
    private const string DigitsAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    internal const int KaratsubaThresholdDigits = 32;
    internal const int FftThresholdDigits = 256;

    private int _signBit;
    private uint _smallValue;
    private uint[]? _data;

    public bool IsNegative => _signBit == 1;

    public BetterBigInteger(uint[] digits, bool isNegative = false)
    {
        ArgumentNullException.ThrowIfNull(digits);
        InitializeFromDigits(digits, isNegative);
    }

    public BetterBigInteger(IEnumerable<uint> digits, bool isNegative = false)
    {
        ArgumentNullException.ThrowIfNull(digits);
        InitializeFromDigits(digits.ToArray(), isNegative);
    }

    public BetterBigInteger(string value, int radix)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (radix is < 2 or > 36)
        {
            throw new ArgumentOutOfRangeException(nameof(radix), "Radix must be in range [2, 36].");
        }

        string trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new FormatException("Value cannot be empty.");
        }

        bool isNegative = false;
        int index = 0;

        if (trimmed[0] is '+' or '-')
        {
            isNegative = trimmed[0] == '-';
            index = 1;
        }

        if (index == trimmed.Length)
        {
            throw new FormatException("Value does not contain digits.");
        }

        uint[] magnitude = [0];
        for (int i = index; i < trimmed.Length; i++)
        {
            int digit = ParseDigit(trimmed[i]);
            if (digit >= radix)
            {
                throw new FormatException($"Digit '{trimmed[i]}' is invalid for base {radix}.");
            }

            magnitude = MultiplyByUInt(magnitude, (uint)radix);
            magnitude = AddUInt(magnitude, (uint)digit);
        }

        InitializeFromDigits(magnitude, isNegative);
    }

    public ReadOnlySpan<uint> GetDigits()
    {
        return _data is not null
            ? _data
            : MemoryMarshal.CreateReadOnlySpan(ref _smallValue, 1);
    }

    public int CompareTo(IBigInteger? other)
    {
        if (other == null)
        {
            return 1;
        }

        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        return CompareSignedMagnitude(GetDigits(), IsNegative, other.GetDigits(), other.IsNegative);
    }

    public bool Equals(IBigInteger? other)
    {
        return other is not null && CompareTo(other) == 0;
    }

    public override bool Equals(object? obj) => obj is IBigInteger other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hash = new();
        ReadOnlySpan<uint> digits = GetDigits();
        int length = GetSignificantLength(digits);
        hash.Add(length == 1 && digits[0] == 0 ? 0 : _signBit);

        for (int i = 0; i < length; i++)
        {
            hash.Add(digits[i]);
        }

        return hash.ToHashCode();
    }

    public static BetterBigInteger operator +(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        ReadOnlySpan<uint> left = a.GetDigits();
        ReadOnlySpan<uint> right = b.GetDigits();

        if (a.IsNegative == b.IsNegative)
        {
            return FromDigits(AddMagnitudes(left, right), a.IsNegative);
        }

        int comparison = CompareMagnitude(left, right);
        if (comparison == 0)
        {
            return Zero();
        }

        return comparison > 0
            ? FromDigits(SubtractMagnitudes(left, right), a.IsNegative)
            : FromDigits(SubtractMagnitudes(right, left), b.IsNegative);
    }

    public static BetterBigInteger operator -(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (IsZero(b.GetDigits()))
        {
            return FromDigits(NormalizeDigits(a.GetDigits()), a.IsNegative);
        }

        return a + FromDigits(NormalizeDigits(b.GetDigits()), !b.IsNegative);
    }

    public static BetterBigInteger operator -(BetterBigInteger a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return IsZero(a.GetDigits())
            ? Zero()
            : FromDigits(NormalizeDigits(a.GetDigits()), !a.IsNegative);
    }

    public static BetterBigInteger operator /(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        ReadOnlySpan<uint> divisorDigits = b.GetDigits();
        if (IsZero(divisorDigits))
        {
            throw new DivideByZeroException();
        }

        (uint[] quotient, _) = DivideMagnitudes(a.GetDigits(), divisorDigits);
        bool isNegative = a.IsNegative ^ b.IsNegative;
        return FromDigits(quotient, isNegative);
    }

    public static BetterBigInteger operator %(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        ReadOnlySpan<uint> divisorDigits = b.GetDigits();
        if (IsZero(divisorDigits))
        {
            throw new DivideByZeroException();
        }

        (_, uint[] remainder) = DivideMagnitudes(a.GetDigits(), divisorDigits);
        return FromDigits(remainder, a.IsNegative);
    }

    public static BetterBigInteger operator *(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        IMultiplier multiplier = SelectMultiplier(a, b);
        return multiplier.Multiply(a, b);
    }

    public static BetterBigInteger operator ~(BetterBigInteger a)
    {
        ArgumentNullException.ThrowIfNull(a);

        int width = Math.Max(1, GetSignificantLength(a.GetDigits()) + 1);
        uint[] words = ToTwosComplement(a.GetDigits(), a.IsNegative, width);
        for (int i = 0; i < words.Length; i++)
        {
            words[i] = ~words[i];
        }

        return FromTwosComplement(words);
    }

    public static BetterBigInteger operator &(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return ApplyBitwiseBinaryOperation(a, b, static (x, y) => x & y);
    }

    public static BetterBigInteger operator |(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return ApplyBitwiseBinaryOperation(a, b, static (x, y) => x | y);
    }

    public static BetterBigInteger operator ^(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return ApplyBitwiseBinaryOperation(a, b, static (x, y) => x ^ y);
    }

    public static BetterBigInteger operator <<(BetterBigInteger a, int shift)
    {
        ArgumentNullException.ThrowIfNull(a);
        if (shift < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shift));
        }

        return FromDigits(ShiftLeftMagnitude(a.GetDigits(), shift), a.IsNegative);
    }

    public static BetterBigInteger operator >>(BetterBigInteger a, int shift)
    {
        ArgumentNullException.ThrowIfNull(a);
        if (shift < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shift));
        }

        if (shift == 0)
        {
            return FromDigits(NormalizeDigits(a.GetDigits()), a.IsNegative);
        }

        int width = GetSignificantLength(a.GetDigits()) + (shift / 32) + 2;
        uint[] words = ToTwosComplement(a.GetDigits(), a.IsNegative, width);
        uint[] shifted = ShiftRightTwosComplement(words, shift, a.IsNegative);
        return FromTwosComplement(shifted);
    }

    public static bool operator ==(BetterBigInteger? a, BetterBigInteger? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        return a.Equals(b);
    }

    public static bool operator !=(BetterBigInteger? a, BetterBigInteger? b) => !(a == b);
    public static bool operator <(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) < 0;
    public static bool operator >(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) > 0;
    public static bool operator <=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) <= 0;
    public static bool operator >=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) >= 0;

    public override string ToString() => ToString(10);

    public string ToString(int radix)
    {
        if (radix is < 2 or > 36)
        {
            throw new ArgumentOutOfRangeException(nameof(radix), "Radix must be in range [2, 36].");
        }
        ReadOnlySpan<uint> digits = GetDigits();
        int significantLength = GetSignificantLength(digits);

        if (significantLength == 1 && digits[0] == 0)
        {
            return "0";
        }

        int maxCharCount = GetBitLength(digits) + (IsNegative ? 1 : 0);

        Span<char> charBuffer = maxCharCount <= 512
            ? stackalloc char[maxCharCount]
            : new char[maxCharCount];

        Span<uint> working = significantLength <= 128
            ? stackalloc uint[significantLength]
            : new uint[significantLength];
        digits[..significantLength].CopyTo(working);
        int workingLength = significantLength;

        int charIndex = charBuffer.Length;

        while (workingLength > 0 && !(workingLength == 1 && working[0] == 0))
        {
            workingLength = DivRemByUIntInPlace(working[..workingLength], (uint)radix, out uint remainder);
            charBuffer[--charIndex] = DigitsAlphabet[(int)remainder];
        }

        if (IsNegative)
        {
            charBuffer[--charIndex] = '-';
        }

        return new string(charBuffer[charIndex..]);
    }

    private static int DivRemByUIntInPlace(Span<uint> digits, uint divisor, out uint remainder)
    {
        ulong carry = 0;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            ulong current = (carry << 32) | digits[i];
            digits[i] = (uint)(current / divisor);
            carry = current % divisor;
        }
        remainder = (uint)carry;

        int last = digits.Length - 1;
        while (last > 0 && digits[last] == 0)
        {
            last--;
        }
        return (last == 0 && digits[0] == 0) ? 0 : last + 1;
    }

    internal static BetterBigInteger FromDigits(uint[] digits, bool isNegative)
    {
        return new BetterBigInteger(digits, isNegative);
    }

    internal static IMultiplier SelectMultiplier(BetterBigInteger a, BetterBigInteger b)
    {
        int maxDigits = Math.Max(a.GetDigits().Length, b.GetDigits().Length);
        if (maxDigits >= FftThresholdDigits)
        {
            return new FftMultiplier();
        }

        if (maxDigits >= KaratsubaThresholdDigits)
        {
            return new KaratsubaMultiplier();
        }

        return new SimpleMultiplier();
    }

    private void InitializeFromDigits(uint[] digits, bool isNegative)
    {
        uint[] normalized = NormalizeDigits(digits);
        if (normalized.Length == 1)
        {
            _smallValue = normalized[0];
            _data = null;
            _signBit = isNegative && _smallValue != 0 ? 1 : 0;
            return;
        }

        _smallValue = 0;
        _data = normalized;
        _signBit = isNegative ? 1 : 0;
    }

    private static BetterBigInteger Zero() => new([0u]);

    private static BetterBigInteger ApplyBitwiseBinaryOperation(
        BetterBigInteger left,
        BetterBigInteger right,
        Func<uint, uint, uint> operation)
    {
        int width = Math.Max(GetSignificantLength(left.GetDigits()), GetSignificantLength(right.GetDigits())) + 1;
        uint[] leftWords = ToTwosComplement(left.GetDigits(), left.IsNegative, width);
        uint[] rightWords = ToTwosComplement(right.GetDigits(), right.IsNegative, width);
        uint[] result = new uint[width];

        for (int i = 0; i < width; i++)
        {
            result[i] = operation(leftWords[i], rightWords[i]);
        }

        return FromTwosComplement(result);
    }

    private static int CompareSignedMagnitude(
        ReadOnlySpan<uint> leftDigits,
        bool leftNegative,
        ReadOnlySpan<uint> rightDigits,
        bool rightNegative)
    {
        bool leftIsZero = IsZero(leftDigits);
        bool rightIsZero = IsZero(rightDigits);
        if (leftIsZero && rightIsZero)
        {
            return 0;
        }

        if (leftNegative != rightNegative)
        {
            return leftNegative ? -1 : 1;
        }

        int magnitudeComparison = CompareMagnitude(leftDigits, rightDigits);
        return leftNegative ? -magnitudeComparison : magnitudeComparison;
    }

    private static int CompareMagnitude(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        int leftLength = GetSignificantLength(left);
        int rightLength = GetSignificantLength(right);

        if (leftLength != rightLength)
        {
            return leftLength.CompareTo(rightLength);
        }

        for (int i = leftLength - 1; i >= 0; i--)
        {
            if (left[i] == right[i])
            {
                continue;
            }

            return left[i] < right[i] ? -1 : 1;
        }

        return 0;
    }

    private static uint[] AddMagnitudes(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
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
        return NormalizeDigits(result);
    }

    private static uint[] SubtractMagnitudes(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
    {
        uint[] result = new uint[left.Length];
        ulong borrow = 0;

        for (int i = 0; i < left.Length; i++)
        {
            ulong leftValue = left[i];
            ulong rightValue = i < right.Length ? right[i] : 0UL;
            ulong subtrahend = rightValue + borrow;

            if (leftValue < subtrahend)
            {
                result[i] = (uint)(leftValue + (1UL << 32) - subtrahend);
                borrow = 1;
            }
            else
            {
                result[i] = (uint)(leftValue - subtrahend);
                borrow = 0;
            }
        }

        return NormalizeDigits(result);
    }

    private static uint[] MultiplyByUInt(ReadOnlySpan<uint> digits, uint factor)
    {
        if (factor == 0 || IsZero(digits))
        {
            return [0];
        }

        uint[] result = new uint[digits.Length + 1];
        ulong carry = 0;

        for (int i = 0; i < digits.Length; i++)
        {
            ulong product = (ulong)digits[i] * factor + carry;
            result[i] = (uint)product;
            carry = product >> 32;
        }

        result[digits.Length] = (uint)carry;
        return NormalizeDigits(result);
    }

    private static uint[] AddUInt(ReadOnlySpan<uint> digits, uint value)
    {
        if (value == 0)
        {
            return NormalizeDigits(digits);
        }

        uint[] result = new uint[Math.Max(1, digits.Length) + 1];
        digits.CopyTo(result);

        ulong carry = value;
        int index = 0;
        while (carry != 0)
        {
            ulong sum = result[index] + carry;
            result[index] = (uint)sum;
            carry = sum >> 32;
            index++;
        }

        return NormalizeDigits(result);
    }

    private static (uint[] Quotient, uint[] Remainder) DivideMagnitudes(
        ReadOnlySpan<uint> dividend,
        ReadOnlySpan<uint> divisor)
    {
        uint[] normalizedDividend = NormalizeDigits(dividend);
        uint[] normalizedDivisor = NormalizeDigits(divisor);

        int comparison = CompareMagnitude(normalizedDividend, normalizedDivisor);
        if (comparison < 0)
        {
            return ([0], normalizedDividend);
        }

        if (comparison == 0)
        {
            return ([1], [0]);
        }

        if (normalizedDivisor.Length == 1)
        {
            uint[] quotient = DivRemByUInt(normalizedDividend, normalizedDivisor[0], out uint smallRemainder);
            return (quotient, [smallRemainder]);
        }

        int bitLength = GetBitLength(normalizedDividend);
        uint[] quotientBits = new uint[(bitLength + 31) / 32];
        uint[] remainder = [0];

        for (int bitIndex = bitLength - 1; bitIndex >= 0; bitIndex--)
        {
            remainder = ShiftLeftMagnitude(remainder, 1);
            if (GetBit(normalizedDividend, bitIndex))
            {
                remainder[0] |= 1;
            }

            if (CompareMagnitude(remainder, normalizedDivisor) >= 0)
            {
                remainder = SubtractMagnitudes(remainder, normalizedDivisor);
                SetBit(quotientBits, bitIndex);
            }
        }

        return (NormalizeDigits(quotientBits), NormalizeDigits(remainder));
    }

    private static uint[] DivRemByUInt(ReadOnlySpan<uint> digits, uint divisor, out uint remainder)
    {
        if (divisor == 0)
        {
            throw new DivideByZeroException();
        }

        uint[] quotient = new uint[Math.Max(1, digits.Length)];
        ulong carry = 0;

        for (int i = digits.Length - 1; i >= 0; i--)
        {
            ulong current = (carry << 32) | digits[i];
            quotient[i] = (uint)(current / divisor);
            carry = current % divisor;
        }

        remainder = (uint)carry;
        return NormalizeDigits(quotient);
    }

    private static uint[] ShiftLeftMagnitude(ReadOnlySpan<uint> digits, int shift)
    {
        uint[] normalized = NormalizeDigits(digits);
        if (shift == 0 || IsZero(normalized))
        {
            return normalized;
        }

        int wordShift = shift / 32;
        int bitShift = shift % 32;
        uint[] result = new uint[normalized.Length + wordShift + 1];
        ulong carry = 0;

        for (int i = 0; i < normalized.Length; i++)
        {
            ulong value = ((ulong)normalized[i] << bitShift) | carry;
            result[i + wordShift] = (uint)value;
            carry = value >> 32;
        }

        result[normalized.Length + wordShift] = (uint)carry;
        return NormalizeDigits(result);
    }

    private static uint[] ShiftRightTwosComplement(uint[] words, int shift, bool isNegative)
    {
        int wordShift = shift / 32;
        int bitShift = shift % 32;
        uint fill = isNegative ? uint.MaxValue : 0;
        uint[] result = new uint[words.Length];

        for (int i = 0; i < result.Length; i++)
        {
            int sourceIndex = i + wordShift;
            if (bitShift == 0)
            {
                result[i] = sourceIndex < words.Length ? words[sourceIndex] : fill;
                continue;
            }

            ulong lower = sourceIndex < words.Length ? words[sourceIndex] : fill;
            ulong upper = sourceIndex + 1 < words.Length ? words[sourceIndex + 1] : fill;
            result[i] = (uint)((lower >> bitShift) | (upper << (32 - bitShift)));
        }

        return result;
    }

    private static uint[] ToTwosComplement(ReadOnlySpan<uint> digits, bool isNegative, int width)
    {
        uint[] result = new uint[Math.Max(1, width)];
        int copyLength = Math.Min(GetSignificantLength(digits), result.Length);
        digits[..copyLength].CopyTo(result);

        if (!isNegative)
        {
            return result;
        }

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = ~result[i];
        }

        AddOneInPlace(result);
        return result;
    }

    private static BetterBigInteger FromTwosComplement(uint[] words)
    {
        if ((words[^1] & 0x80000000) == 0)
        {
            return FromDigits(NormalizeDigits(words), false);
        }

        uint[] magnitude = new uint[words.Length];
        Array.Copy(words, magnitude, words.Length);

        for (int i = 0; i < magnitude.Length; i++)
        {
            magnitude[i] = ~magnitude[i];
        }

        AddOneInPlace(magnitude);
        return FromDigits(NormalizeDigits(magnitude), true);
    }

    private static void AddOneInPlace(Span<uint> words)
    {
        ulong carry = 1;
        for (int i = 0; i < words.Length && carry != 0; i++)
        {
            ulong sum = words[i] + carry;
            words[i] = (uint)sum;
            carry = sum >> 32;
        }
    }

    private static int GetBitLength(ReadOnlySpan<uint> digits)
    {
        int length = GetSignificantLength(digits);
        if (length == 1 && digits[0] == 0)
        {
            return 0;
        }

        uint mostSignificant = digits[length - 1];
        int bitCount = 32;
        while ((mostSignificant & 0x80000000) == 0)
        {
            mostSignificant <<= 1;
            bitCount--;
        }

        return ((length - 1) * 32) + bitCount;
    }

    private static bool GetBit(ReadOnlySpan<uint> digits, int bitIndex)
    {
        int wordIndex = bitIndex / 32;
        if (wordIndex >= GetSignificantLength(digits))
        {
            return false;
        }

        return ((digits[wordIndex] >> (bitIndex % 32)) & 1U) != 0;
    }

    private static void SetBit(Span<uint> digits, int bitIndex)
    {
        digits[bitIndex / 32] |= 1U << (bitIndex % 32);
    }

    private static bool IsZero(ReadOnlySpan<uint> digits)
    {
        return GetSignificantLength(digits) == 1 && digits[0] == 0;
    }

    private static int GetSignificantLength(ReadOnlySpan<uint> digits)
    {
        if (digits.Length == 0)
        {
            return 1;
        }

        int last = digits.Length - 1;
        while (last > 0 && digits[last] == 0)
        {
            last--;
        }

        return last + 1;
    }

    private static uint[] NormalizeDigits(ReadOnlySpan<uint> digits)
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

        uint[] normalized = new uint[last + 1];
        digits[..(last + 1)].CopyTo(normalized);
        return normalized;
    }

    private static int ParseDigit(char c)
    {
        char upper = char.ToUpperInvariant(c);
        if (upper is >= '0' and <= '9')
        {
            return upper - '0';
        }

        if (upper is >= 'A' and <= 'Z')
        {
            return upper - 'A' + 10;
        }

        throw new FormatException($"Invalid digit '{c}'.");
    }
}
