/*
 * Copyright 2018 Gary W. Lucas.
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
 * 11/2018  G. Lucas     Created
 * 08/2025 M.Fender     Ported to C#
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Utils;

using System.Runtime.CompilerServices;

/// <summary>
///     Provides methods and elements for Kahan's algorithm for
///     summing a set of numerical values with extended precision arithmetic.
///     Often, when adding a large set of small values to a large value,
///     the limited precision of computer arithmetic results in the contribution
///     of the small values being lost. This limitation may result in a loss
///     of valuable data if the total sum of the collected small values is
///     large enough to make a meaningful contribution to the
///     large value. Kahan's algorithm extends the precision of the computation
///     so that the contribution of small values is preserved.
/// </summary>
public class KahanSummation
{
    private double _c; // compensator for Kahan summation  

    private int _n; // count of values added

    private double _s; // summand

    /// <summary>
    ///     Creates a new Kahan summation accumulator.
    /// </summary>
    public KahanSummation()
    {
        _c = 0.0;
        _s = 0.0;
        _n = 0;
    }

    /// <summary>
    ///     Implicit conversion to double, returning the current sum.
    /// </summary>
    /// <param name="summation">The KahanSummation instance</param>
    public static implicit operator double(KahanSummation summation)
    {
        return summation._s;
    }

    /// <summary>
    ///     Add the value to the summation using Kahan's compensated summation algorithm.
    /// </summary>
    /// <param name="a">A valid floating-point number</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(double a)
    {
        var y = a - _c;
        var t = _s + y;
        _c = t - _s - y;
        _s = t;
        _n++;
    }

    /// <summary>
    ///     Adds multiple values to the summation.
    /// </summary>
    /// <param name="values">Collection of values to add</param>
    public void Add(IEnumerable<double> values)
    {
        foreach (var value in values) Add(value);
    }

    /// <summary>
    ///     Adds an array of values to the summation.
    /// </summary>
    /// <param name="values">Array of values to add</param>
    public void Add(params double[] values)
    {
        for (var i = 0; i < values.Length; i++) Add(values[i]);
    }

    /// <summary>
    ///     Gets the current compensation value (for diagnostic purposes).
    ///     This represents the accumulated error that Kahan's algorithm tracks.
    /// </summary>
    /// <returns>The current compensation value</returns>
    public double GetCompensation()
    {
        return _c;
    }

    /// <summary>
    ///     Gets the mean value of the summands.
    /// </summary>
    /// <returns>A valid floating-point value.</returns>
    public double GetMean()
    {
        if (_n == 0) return 0;
        return _s / _n;
    }

    /// <summary>
    ///     The current value of the summation.
    /// </summary>
    /// <returns>
    ///     The standard-precision part of the sum,
    ///     a valid floating-point number.
    /// </returns>
    public double GetSum()
    {
        return _s;
    }

    /// <summary>
    ///     Gets the number of summands that were added to the summation.
    /// </summary>
    /// <returns>A value of zero or greater.</returns>
    public int GetSummandCount()
    {
        return _n;
    }

    /// <summary>
    ///     Resets the summation to its initial state.
    /// </summary>
    public void Reset()
    {
        _c = 0.0;
        _s = 0.0;
        _n = 0;
    }

    /// <summary>
    ///     Returns a string representation of the summation.
    /// </summary>
    /// <returns>A string showing the sum and count</returns>
    public override string ToString()
    {
        return $"KahanSummation[Sum: {_s}, Count: {_n}, Compensation: {_c}]";
    }
}