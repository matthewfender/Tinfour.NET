/*
 * Copyright 2014 Gary W. Lucas.
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
 * Date    Name       Description
 * ------  ---------  -------------------------------------------------
 * 05/2014 G. Lucas   Created
 * 08/2025 M. Fender   Ported to C#
 *
 * Notes: This class collects fragments of code from the various methods/classes
 * in the TIN family into a single unified (and hopefully consistent)
 * class.
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

using System.Runtime.CompilerServices;

/// <summary>
///     Provides a standard calculation of threshold values appropriate for use in an
///     incremental TIN implementation based on nominal point spacing.
///     With the exception of vertex tolerance, all thresholds are computed using
///     a small multiplier times the Unit of Least Precision (ULP) computed
///     from the nominal point spacing. The vertex tolerance is a fixed fraction
///     of the nominal point spacing.
/// </summary>
public class Thresholds
{
    /// <summary>Factor for computing the Delaunay threshold.</summary>
    public const double DelaunayThresholdFactor = 256.0;

    /// <summary>Factor for computing the half-plane threshold.</summary>
    public const double HalfPlaneThresholdFactor = 256.0;

    /// <summary>Factor for computing the in-circle threshold.</summary>
    public const double InCircleThresholdFactor = 1024 * 1024;

    /// <summary>Factor for computing precision threshold.</summary>
    public const double PrecisionThresholdFactor = 256;

    /// <summary>Factor for computing the vertex tolerance.</summary>
    public const double VertexToleranceFactorDefault = 1.0e+5;

    /// <summary>
    ///     Threshold for circumcircle determinant calculations.
    /// </summary>
    private readonly double _circumcircleDeterminantThreshold;

    /// <summary>
    ///     The computed value for evaluating whether a triangle pair is
    ///     within sufficient tolerance when testing to see if they approximately
    ///     meet the Delaunay criterion using the in-circle calculation.
    ///     A positive (non-zero) in-circle value indicates that the pair
    ///     violates the criterion, but for case where floating-point limitations
    ///     may result in conflicts, a very small positive value may be acceptable
    ///     for approximation purposes.
    /// </summary>
    private readonly double _delaunayThreshold;

    /// <summary>
    ///     A threshold value giving guidelines for the smallest absolute value
    ///     result that can be trusted in geometric calculations for determining
    ///     on which side of a point a plane lies (the "half-plane calculation").
    ///     If the absolute value of the result is smaller than this threshold,
    ///     extended-precision arithmetic is advised.
    /// </summary>
    private readonly double _halfPlaneThreshold;

    /// <summary>
    ///     The computed value for the threshold that indicates
    ///     when an ordinary precision calculation for the in-circle criterion
    ///     may be inaccurate and an extended precision calculation should be used.
    /// </summary>
    private readonly double _inCircleThreshold;

    /// <summary>
    ///     The nominal point spacing value specified in the constructor.
    ///     In general, this value is a rough estimate of the
    ///     mean distance between neighboring points (or vertices).
    /// </summary>
    private readonly double _nominalPointSpacing;

    /// <summary>
    ///     A threshold value giving guidelines for the smallest absolute value
    ///     that can be used in geometric calculations without excessive loss
    ///     of precision. This value is based on general assumptions about the
    ///     what constitutes a significant distance given the nominal point
    ///     spacing of the vertices in the TIN.
    /// </summary>
    private readonly double _precisionThreshold;

    /// <summary>
    ///     A threshold value indicating the distance at which a pair
    ///     of (x,y) coordinates will be treated as effectively a match for
    ///     a vertex.
    /// </summary>
    private readonly double _vertexTolerance;

    /// <summary>
    ///     The square of the vertex tolerance value.
    /// </summary>
    private readonly double _vertexTolerance2;

    /// <summary>
    ///     Constructs thresholds for a nominal point spacing of 1.
    /// </summary>
    public Thresholds()
        : this(1.0)
    {
    }

    /// <summary>
    ///     Constructs threshold values for the specified nominalPointSpacing.
    ///     In general, the nominal point spacing is a rough estimate of the
    ///     mean distance between neighboring points (or vertices). It is used
    ///     for estimating threshold values for logic used by the IncrementalTin
    ///     and related classes. A perfect value is not necessary. An estimate
    ///     within a couple orders of magnitude of the actual value is sufficient.
    /// </summary>
    /// <param name="nominalPointSpacing">A positive, non-zero value</param>
    /// <exception cref="ArgumentException">If nominalPointSpacing is not positive</exception>
    public Thresholds(double nominalPointSpacing)
    {
        if (nominalPointSpacing <= 0)
            throw new ArgumentException(
                $"Nominal point spacing specification {nominalPointSpacing} is not greater than zero",
                nameof(nominalPointSpacing));

        _nominalPointSpacing = nominalPointSpacing;

        // Calculate ULP (Unit in the Last Place) for the nominal point spacing
        var ulp = CalculateUlp(nominalPointSpacing);

        _precisionThreshold = PrecisionThresholdFactor * ulp;
        _halfPlaneThreshold = HalfPlaneThresholdFactor * _precisionThreshold;
        _inCircleThreshold = InCircleThresholdFactor * _precisionThreshold;
        _delaunayThreshold = DelaunayThresholdFactor * _precisionThreshold;
        _vertexTolerance = nominalPointSpacing / VertexToleranceFactorDefault;
        _vertexTolerance2 = _vertexTolerance * _vertexTolerance;
        _circumcircleDeterminantThreshold = 32 * _inCircleThreshold;
    }

    /// <summary>
    ///     Gets a threshold value giving guidelines for the smallest absolute value
    ///     result that can be trusted in geometric calculations for for computing
    ///     a determinant to be used in determining a set of circumcircle
    ///     center coordinates and radius. If the absolute value
    ///     of the determinant result is smaller than this threshold, extended-precision
    ///     arithmetic is advised.
    /// </summary>
    /// <returns>
    ///     A positive, non-zero value significantly smaller than the
    ///     nominal point spacing.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetCircumcircleDeterminantThreshold()
    {
        return _circumcircleDeterminantThreshold;
    }

    /// <summary>
    ///     Gets the computed value for evaluating whether a triangle pair is
    ///     within sufficient tolerance when testing to see if they approximately
    ///     meet the Delaunay criterion using the in-circle calculation.
    ///     A positive (non-zero) in-circle value indicates that the pair
    ///     violates the criterion, but for case where floating-point limitations
    ///     may result in conflicts, a very small positive value may be acceptable
    ///     for approximation purposes.
    /// </summary>
    /// <remarks>
    ///     This value is primarily used in test procedures that evaluate
    ///     the correctness of a TIN constructed by the IncrementalTin class.
    /// </remarks>
    /// <returns>A positive value much smaller than the nominal point spacing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDelaunayThreshold()
    {
        return _delaunayThreshold;
    }

    /// <summary>
    ///     Gets a threshold value giving guidelines for the smallest absolute value
    ///     result that can be trusted in geometric calculations for determining
    ///     on which side of a point a plane lies (the "half-plane calculation").
    ///     If the absolute value of the result is smaller than this threshold,
    ///     extended-precision arithmetic is advised.
    /// </summary>
    /// <returns>
    ///     A positive, non-zero value much smaller than the nominal
    ///     point spacing.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetHalfPlaneThreshold()
    {
        return _halfPlaneThreshold;
    }

    /// <summary>
    ///     Gets the threshold value indicating when an extended-precision
    ///     calculation must be used for the in-circle determination.
    /// </summary>
    /// <returns>
    ///     A positive value scaled according to the nominal
    ///     point spacing of the TIN.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetInCircleThreshold()
    {
        return _inCircleThreshold;
    }

    /// <summary>
    ///     Gets the nominal point spacing value specified in the constructor.
    ///     In general, this value is a rough estimate of the
    ///     mean distance between neighboring points (or vertices).
    /// </summary>
    /// <returns>A positive, non-zero value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetNominalPointSpacing()
    {
        return _nominalPointSpacing;
    }

    /// <summary>
    ///     Get a threshold value giving guidelines for the smallest absolute value
    ///     result from a geometric calculations that can be accepted without
    ///     concern for an excessive loss of precision. This value is based on
    ///     general assumptions about the what constitutes a significant distance
    ///     given the nominal point spacing of the vertices in the TIN.
    /// </summary>
    /// <returns>A small, positive value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetPrecisionThreshold()
    {
        return _precisionThreshold;
    }

    /// <summary>
    ///     Gets a threshold value indicating the distance at which a pair
    ///     of (x,y) coordinates will be treated as effectively a match for
    ///     a vertex.
    /// </summary>
    /// <returns>A distance in the system of units consistent with the TIN.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetVertexTolerance()
    {
        return _vertexTolerance;
    }

    /// <summary>
    ///     Gets a threshold value indicating the square of the distance at which a
    ///     pair of (x,y) coordinates will be treated as effectively a match for
    ///     a vertex.
    /// </summary>
    /// <returns>A distance squared in the system of units consistent with the TIN.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetVertexTolerance2()
    {
        return _vertexTolerance2;
    }

    /// <summary>
    ///     Calculate the Unit in the Last Place (ULP) for a given value.
    ///     This is .NET's equivalent of Java's Math.ulp() method.
    /// </summary>
    /// <param name="value">The value to calculate ULP for</param>
    /// <returns>The ULP of the given value</returns>
    private static double CalculateUlp(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return double.NaN;

        if (value == 0.0) return double.Epsilon;

        // Use BitConverter to get the raw IEEE 754 representation
        var bits = BitConverter.DoubleToInt64Bits(Math.Abs(value));

        // For normal numbers, increment the least significant bit
        if (bits != 0x7FEFFFFFFFFFFFFF) // Not the maximum finite value
            bits++;

        var nextValue = BitConverter.Int64BitsToDouble(bits);
        return nextValue - Math.Abs(value);
    }
}