/*
 * Copyright 2023 G.W. Lucas
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

/*
 * -----------------------------------------------------------------------
 *
 * Revision History:
 * Date     Name         Description
 * ------   ---------    -------------------------------------------------
 * 08/2025 M.Fender     Created - Extended precision arithmetic for .NET
 *
 * Notes:
 * This implementation provides double-double precision arithmetic similar
 * to the JTS DD class used in the original Java Tinfour implementation.
 * Double-double arithmetic uses two double values to represent a single
 * high-precision number, providing approximately 106 bits of precision
 * (about 30 decimal digits) compared to 53 bits for standard double.
 *
 * The algorithms used here are based on the work of:
 * - Dekker (1971) - A floating-point technique for extending the available precision
 * - Shewchuk (1997) - Adaptive Precision Floating-Point Arithmetic and Fast Robust Geometric Predicates
 * - Hida, Li, Bailey (2000) - Algorithms for quad-double precision floating point arithmetic
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

using System.Runtime.CompilerServices;

/// <summary>
///     Provides extended precision floating-point arithmetic using two double values
///     to represent a single high-precision number. This gives approximately 106 bits
///     of precision (about 30 decimal digits) compared to 53 bits for standard double.
/// </summary>
/// <remarks>
///     This implementation is based on the double-double arithmetic algorithms
///     developed by Dekker and Shewchuk. It is essential for robust geometric
///     predicates in computational geometry applications where numerical precision
///     is critical for correctness.
/// </remarks>
public struct DoubleDouble : IComparable<DoubleDouble>, IEquatable<DoubleDouble>
{
    /// <summary>
    ///     The high-order component of the double-double value.
    /// </summary>
    private readonly double _hi;

    /// <summary>
    ///     The low-order component of the double-double value.
    /// </summary>
    private readonly double _lo;

    /// <summary>
    ///     Constant for zero in double-double precision.
    /// </summary>
    public static readonly DoubleDouble Zero = new(0.0, 0.0);

    /// <summary>
    ///     Constant for one in double-double precision.
    /// </summary>
    public static readonly DoubleDouble One = new(1.0, 0.0);

    /// <summary>
    ///     Constant for positive infinity in double-double precision.
    /// </summary>
    public static readonly DoubleDouble PositiveInfinity = new(double.PositiveInfinity, 0.0);

    /// <summary>
    ///     Constant for negative infinity in double-double precision.
    /// </summary>
    public static readonly DoubleDouble NegativeInfinity = new(double.NegativeInfinity, 0.0);

    /// <summary>
    ///     Constant for NaN in double-double precision.
    /// </summary>
    public static readonly DoubleDouble NaN = new(double.NaN, double.NaN);

    /// <summary>
    ///     Creates a new DoubleDouble from high and low components.
    /// </summary>
    /// <param name="hi">The high-order component</param>
    /// <param name="lo">The low-order component</param>
    public DoubleDouble(double hi, double lo)
    {
        _hi = hi;
        _lo = lo;
    }

    /// <summary>
    ///     Creates a new DoubleDouble from a single double value.
    /// </summary>
    /// <param name="value">The value to convert</param>
    public DoubleDouble(double value)
    {
        _hi = value;
        _lo = 0.0;
    }

    /// <summary>
    ///     Gets the high-order component.
    /// </summary>
    public double Hi => _hi;

    /// <summary>
    ///     Gets the low-order component.
    /// </summary>
    public double Lo => _lo;

    /// <summary>
    ///     Converts the DoubleDouble to a standard double value.
    /// </summary>
    /// <returns>The closest double approximation</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ToDouble()
    {
        return _hi + _lo;
    }

    /// <summary>
    ///     Determines if this value is zero.
    /// </summary>
    public bool IsZero => _hi == 0.0 && _lo == 0.0;

    /// <summary>
    ///     Determines if this value is NaN.
    /// </summary>
    public bool IsNaN => double.IsNaN(_hi) || double.IsNaN(_lo);

    /// <summary>
    ///     Determines if this value is infinite.
    /// </summary>
    public bool IsInfinity => double.IsInfinity(_hi);

    /// <summary>
    ///     Determines if this value is finite.
    /// </summary>
    public bool IsFinite => double.IsFinite(_hi) && double.IsFinite(_lo);

    /// <summary>
    ///     Fast two-sum algorithm: computes the exact sum of two double values.
    /// </summary>
    /// <param name="a">First operand</param>
    /// <param name="b">Second operand</param>
    /// <returns>DoubleDouble representing the exact sum</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DoubleDouble FastTwoSum(double a, double b)
    {
        var s = a + b;
        var e = b - (s - a);
        return new DoubleDouble(s, e);
    }

    /// <summary>
    ///     Two-sum algorithm: computes the exact sum of two double values.
    ///     Works correctly even when |a| < |b|.
    /// </summary>
    /// <param name="a">First operand</param>
    /// <param name="b">Second operand</param>
    /// <returns>DoubleDouble representing the exact sum</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DoubleDouble TwoSum(double a, double b)
    {
        var s = a + b;
        var v = s - a;
        var e = a - (s - v) + (b - v);
        return new DoubleDouble(s, e);
    }

    /// <summary>
    ///     Two-product algorithm: computes the exact product of two double values.
    /// </summary>
    /// <param name="a">First operand</param>
    /// <param name="b">Second operand</param>
    /// <returns>DoubleDouble representing the exact product</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DoubleDouble TwoProduct(double a, double b)
    {
        var p = a * b;
        var e = Math.FusedMultiplyAdd(a, b, -p); // Uses FMA for better accuracy
        return new DoubleDouble(p, e);
    }

    /// <summary>
    ///     Adds two DoubleDouble values.
    /// </summary>
    public static DoubleDouble operator +(DoubleDouble a, DoubleDouble b)
    {
        var s1 = TwoSum(a._hi, b._hi);
        var s2 = TwoSum(a._lo, b._lo);
        var c = s1.Lo + s2.Hi;
        var s3 = FastTwoSum(s1.Hi, c);
        return FastTwoSum(s3.Hi, s2.Lo + s3.Lo);
    }

    /// <summary>
    ///     Adds a DoubleDouble and a double.
    /// </summary>
    public static DoubleDouble operator +(DoubleDouble a, double b)
    {
        var s1 = TwoSum(a._hi, b);
        var s2 = FastTwoSum(s1.Hi, s1.Lo + a._lo);
        return s2;
    }

    /// <summary>
    ///     Subtracts two DoubleDouble values.
    /// </summary>
    public static DoubleDouble operator -(DoubleDouble a, DoubleDouble b)
    {
        return a + -b;
    }

    /// <summary>
    ///     Subtracts a double from a DoubleDouble.
    /// </summary>
    public static DoubleDouble operator -(DoubleDouble a, double b)
    {
        return a + -b;
    }

    /// <summary>
    ///     Negates a DoubleDouble value.
    /// </summary>
    public static DoubleDouble operator -(DoubleDouble a)
    {
        return new DoubleDouble(-a._hi, -a._lo);
    }

    /// <summary>
    ///     Multiplies two DoubleDouble values.
    /// </summary>
    public static DoubleDouble operator *(DoubleDouble a, DoubleDouble b)
    {
        var p = TwoProduct(a._hi, b._hi);
        p = FastTwoSum(p.Hi, p.Lo + a._hi * b._lo + a._lo * b._hi);
        return p;
    }

    /// <summary>
    ///     Multiplies a DoubleDouble by a double.
    /// </summary>
    public static DoubleDouble operator *(DoubleDouble a, double b)
    {
        var p = TwoProduct(a._hi, b);
        return FastTwoSum(p.Hi, p.Lo + a._lo * b);
    }

    /// <summary>
    ///     Divides two DoubleDouble values.
    /// </summary>
    public static DoubleDouble operator /(DoubleDouble a, DoubleDouble b)
    {
        var q1 = a._hi / b._hi;
        var p = b * q1;
        var q2 = (a._hi - p._hi + a._lo - p._lo) / b._hi;
        return FastTwoSum(q1, q2);
    }

    /// <summary>
    ///     Divides a DoubleDouble by a double.
    /// </summary>
    public static DoubleDouble operator /(DoubleDouble a, double b)
    {
        var q1 = a._hi / b;
        var p = TwoProduct(q1, b);
        var q2 = (a._hi - p._hi + a._lo - p._lo) / b;
        return FastTwoSum(q1, q2);
    }

    /// <summary>
    ///     Implicit conversion from double to DoubleDouble.
    /// </summary>
    public static implicit operator DoubleDouble(double value)
    {
        return new DoubleDouble(value);
    }

    /// <summary>
    ///     Explicit conversion from DoubleDouble to double.
    /// </summary>
    public static explicit operator double(DoubleDouble value)
    {
        return value.ToDouble();
    }

    /// <summary>
    ///     Compares two DoubleDouble values for equality.
    /// </summary>
    public static bool operator ==(DoubleDouble a, DoubleDouble b)
    {
        return a._hi == b._hi && a._lo == b._lo;
    }

    /// <summary>
    ///     Compares two DoubleDouble values for inequality.
    /// </summary>
    public static bool operator !=(DoubleDouble a, DoubleDouble b)
    {
        return !(a == b);
    }

    /// <summary>
    ///     Compares if the first DoubleDouble is less than the second.
    /// </summary>
    public static bool operator <(DoubleDouble a, DoubleDouble b)
    {
        return a._hi < b._hi || (a._hi == b._hi && a._lo < b._lo);
    }

    /// <summary>
    ///     Compares if the first DoubleDouble is greater than the second.
    /// </summary>
    public static bool operator >(DoubleDouble a, DoubleDouble b)
    {
        return a._hi > b._hi || (a._hi == b._hi && a._lo > b._lo);
    }

    /// <summary>
    ///     Compares if the first DoubleDouble is less than or equal to the second.
    /// </summary>
    public static bool operator <=(DoubleDouble a, DoubleDouble b)
    {
        return a < b || a == b;
    }

    /// <summary>
    ///     Compares if the first DoubleDouble is greater than or equal to the second.
    /// </summary>
    public static bool operator >=(DoubleDouble a, DoubleDouble b)
    {
        return a > b || a == b;
    }

    /// <summary>
    ///     Compares this DoubleDouble with another for ordering.
    /// </summary>
    public int CompareTo(DoubleDouble other)
    {
        if (this < other) return -1;
        if (this > other) return 1;
        return 0;
    }

    /// <summary>
    ///     Determines whether this DoubleDouble equals another DoubleDouble.
    /// </summary>
    public bool Equals(DoubleDouble other)
    {
        return this == other;
    }

    /// <summary>
    ///     Determines whether this DoubleDouble equals another object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is DoubleDouble other && Equals(other);
    }

    /// <summary>
    ///     Gets the hash code for this DoubleDouble.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(_hi, _lo);
    }

    /// <summary>
    ///     Gets a string representation of this DoubleDouble.
    /// </summary>
    public override string ToString()
    {
        if (IsNaN) return "NaN";
        if (double.IsPositiveInfinity(_hi)) return "+Infinity";
        if (double.IsNegativeInfinity(_hi)) return "-Infinity";

        return ToDouble().ToString("G17");
    }

    /// <summary>
    ///     Gets a string representation with the specified format.
    /// </summary>
    public string ToString(string format)
    {
        if (IsNaN) return "NaN";
        if (double.IsPositiveInfinity(_hi)) return "+Infinity";
        if (double.IsNegativeInfinity(_hi)) return "-Infinity";

        return ToDouble().ToString(format);
    }

    /// <summary>
    ///     Computes the absolute value of a DoubleDouble.
    /// </summary>
    public static DoubleDouble Abs(DoubleDouble value)
    {
        if (value._hi >= 0) return value;
        return -value;
    }

    /// <summary>
    ///     Returns the larger of two DoubleDouble values.
    /// </summary>
    public static DoubleDouble Max(DoubleDouble a, DoubleDouble b)
    {
        return a > b ? a : b;
    }

    /// <summary>
    ///     Returns the smaller of two DoubleDouble values.
    /// </summary>
    public static DoubleDouble Min(DoubleDouble a, DoubleDouble b)
    {
        return a < b ? a : b;
    }

    /// <summary>
    ///     Returns the DoubleDouble with the larger absolute value.
    /// </summary>
    public static DoubleDouble MaxByMagnitude(DoubleDouble a, DoubleDouble b)
    {
        return Abs(a) > Abs(b) ? a : b;
    }
}