/*
 * Copyright 2013 Gary W. Lucas.
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
 * 03/2013  G. Lucas     Created
 * 08/2025 M.Fender     Ported to C#
 *
 * Notes:
 * A good review of walk algorithms for point location is provided by
 * Soukal, R.; M�lkov�, Kolingerov� (2012) "Walking algorithms for point
 * location in TIN models", Computational Geoscience 16:853-869.
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

using System.Diagnostics;

/// <summary>
///     Methods and definitions to perform a stochastic Lawson's walk.
///     The walk uses randomization to prevent getting trapped in infinite loops
///     in cases of imperfections in Delaunay triangulations.
/// </summary>
/// <remarks>
///     The randomization uses a custom implementation of the XORShift Random Number
///     Generator discovered by George Marsaglia in 2003. This method is faster than
///     the standard Random class while providing reproducible sequences for debugging.
/// </remarks>
public class StochasticLawsonsWalk
{
    /// <summary>
    ///     Maximum iterations for triangle walk to prevent infinite loops.
    /// </summary>
    private const int MaxTriangleWalkIterations = 100000;

    /// <summary>
    ///     Maximum iterations for perimeter edge search to prevent infinite loops.
    /// </summary>
    private const int MaxPerimeterSearchIterations = 100000;

    /// <summary>
    ///     Geometric operations utilities for calculations.
    /// </summary>
    private readonly GeometricOperations _geometricOps;

    /// <summary>
    ///     Positive threshold for determining if higher-precision calculation is required.
    /// </summary>
    private readonly double _halfPlaneThreshold;

    /// <summary>
    ///     Negative threshold for determining if higher-precision calculation is required.
    /// </summary>
    private readonly double _halfPlaneThresholdNeg;

    /// <summary>
    ///     Diagnostic counter for the number of walks performed.
    /// </summary>
    private int _nSlw;

    /// <summary>
    ///     Diagnostic counter for walks that transferred to the exterior.
    /// </summary>
    private int _nSlwGhost;

    /// <summary>
    ///     Diagnostic counter for the number of steps in walks performed.
    /// </summary>
    private int _nSlwSteps;

    /// <summary>
    ///     Diagnostic counter for the number of side-tests performed.
    /// </summary>
    private int _nSlwTests;

    /// <summary>
    ///     Randomization seed for selecting triangle sides during walks.
    /// </summary>
    private long _seed = 1L;

    /// <summary>
    ///     Initializes a new instance with the specified nominal point spacing.
    /// </summary>
    /// <param name="nominalPointSpacing">A value greater than zero indicating distance magnitude</param>
    public StochasticLawsonsWalk(double nominalPointSpacing)
    {
        var thresholds = new Thresholds(nominalPointSpacing);
        _geometricOps = new GeometricOperations(thresholds);
        _halfPlaneThreshold = thresholds.GetHalfPlaneThreshold();
        _halfPlaneThresholdNeg = -thresholds.GetHalfPlaneThreshold();
    }

    /// <summary>
    ///     Initializes a new instance with a nominal point spacing of 1.0.
    /// </summary>
    public StochasticLawsonsWalk()
        : this(1.0)
    {
    }

    /// <summary>
    ///     Initializes a new instance using specified thresholds.
    /// </summary>
    /// <param name="thresholds">A valid thresholds object</param>
    public StochasticLawsonsWalk(Thresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);

        _geometricOps = new GeometricOperations(thresholds);
        _halfPlaneThreshold = thresholds.GetHalfPlaneThreshold();
        _halfPlaneThresholdNeg = -thresholds.GetHalfPlaneThreshold();
    }

    /// <summary>
    ///     Clears all diagnostic fields and resets the random seed.
    /// </summary>
    public void ClearDiagnostics()
    {
        _geometricOps.ClearDiagnostics();
        _nSlwSteps = 0;
        _nSlw = 0;
        _nSlwTests = 0;
        _nSlwGhost = 0;
        _seed = 1L;
    }

    /// <summary>
    ///     Searches the mesh beginning at the specified edge to find the triangle
    ///     that contains the specified coordinates.
    /// </summary>
    /// <param name="startingEdge">The edge giving the starting point of the search</param>
    /// <param name="x">The x coordinate of interest</param>
    /// <param name="y">The y coordinate of interest</param>
    /// <returns>An edge of a triangle containing the coordinates, or the nearest exterior edge</returns>
    public IQuadEdge FindAnEdgeFromEnclosingTriangle(IQuadEdge startingEdge, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(startingEdge);

        var edge = startingEdge;

        if (edge.GetForward().GetB().IsNullVertex()) edge = edge.GetDual();

        _nSlw++;

        var v0 = edge.GetA();
        var v1 = edge.GetB();

        var vX0 = x - v0.GetX();
        var vY0 = y - v0.GetY();
        var pX0 = v0.GetY() - v1.GetY(); // perpendicular
        var pY0 = v1.GetX() - v0.GetX();

        var h0 = vX0 * pX0 + vY0 * pY0;

        _nSlwTests++;
        if (h0 < _halfPlaneThresholdNeg)
        {
            // Transfer to opposite triangle
            edge = edge.GetDual();
            v0 = edge.GetA();
        }
        else if (h0 < _halfPlaneThreshold)
        {
            // Coordinate is close to the edge, use high-precision calculation
            h0 = _geometricOps.HalfPlane(v0.GetX(), v0.GetY(), v1.GetX(), v1.GetY(), x, y);
            if (h0 < 0)
            {
                edge = edge.GetDual();
                v0 = edge.GetA();
            }
        }

        var iterations = 0;

        while (iterations++ < MaxTriangleWalkIterations)
        {
            _nSlwSteps++;

            // Check if we've reached a ghost triangle (exterior)
            v1 = edge.GetB();
            var v2 = edge.GetForward().GetB();

            if (v2.IsNullVertex()) return FindAssociatedPerimeterEdge(edge, x, y);

            // Test the other two sides of the triangle with randomized order
            var edgeSelection = RandomNext();
            if ((edgeSelection & 1) == 0)
            {
                // Test side 1 first, then side 2
                if (TestAndTransfer(edge.GetForward(), x, y, out var nextEdge))
                {
                    edge = nextEdge;
                    v0 = edge.GetA();
                    continue;
                }

                if (TestAndTransfer(edge.GetReverse(), x, y, out nextEdge))
                {
                    edge = nextEdge;
                    v0 = edge.GetA();
                    continue;
                }
            }
            else
            {
                // Test side 2 first, then side 1
                if (TestAndTransfer(edge.GetReverse(), x, y, out var nextEdge))
                {
                    edge = nextEdge;
                    v0 = edge.GetA();
                    continue;
                }

                if (TestAndTransfer(edge.GetForward(), x, y, out nextEdge))
                {
                    edge = nextEdge;
                    v0 = edge.GetA();
                    continue;
                }
            }

            // No transfer occurred - the point is inside this triangle
            return edge;
        }

        throw new InvalidOperationException(
            $"Triangle walk exceeded maximum iterations ({MaxTriangleWalkIterations}) - possible infinite loop");
    }

    /// <summary>
    ///     Gets diagnostic information about walk performance.
    /// </summary>
    /// <returns>A summary of walk statistics</returns>
    public WalkDiagnostics GetDiagnostics()
    {
        var avgSteps = _nSlw > 0 ? (double)_nSlwSteps / _nSlw : 0;
        return new WalkDiagnostics
                   {
                       NumberOfWalks = _nSlw,
                       NumberOfExteriorWalks = _nSlwGhost,
                       NumberOfTests = _nSlwTests,
                       NumberOfExtendedPrecisionCalls = _geometricOps.GetHalfPlaneCount(),
                       AverageStepsToCompletion = avgSteps
                   };
    }

    /// <summary>
    ///     Resets the random seed to its initial value.
    /// </summary>
    public void Reset()
    {
        _seed = 1;
    }

    /// <summary>
    ///     Finds the associated perimeter edge when the search reaches the exterior.
    /// </summary>
    /// <param name="startingEdge">The starting edge for the perimeter search</param>
    /// <param name="x">The x coordinate of interest</param>
    /// <param name="y">The y coordinate of interest</param>
    /// <returns>The exterior-side edge that subtends the search coordinates</returns>
    private IQuadEdge FindAssociatedPerimeterEdge(IQuadEdge startingEdge, double x, double y)
    {
        var edge = startingEdge;
        _nSlwGhost++;

        var v0 = edge.GetA();
        var v1 = edge.GetB();

        var vX0 = x - v0.GetX();
        var vY0 = y - v0.GetY();
        var tX = v1.GetX() - v0.GetX();
        var tY = v1.GetY() - v0.GetY();
        var tC = tX * vX0 + tY * vY0;

        if (_halfPlaneThresholdNeg < tC && tC < _halfPlaneThreshold)
            tC = _geometricOps.Direction(v0.GetX(), v0.GetY(), v1.GetX(), v1.GetY(), x, y);

        var iterations = 0;

        if (tC < 0)

            // The vertex is retrograde to the starting ghost.
            // Transfer backward along the perimeter.
            while (iterations++ < MaxPerimeterSearchIterations)
            {
                _nSlwSteps++;

                var nEdge = edge.GetReverse().GetReverseFromDual();

                v0 = nEdge.GetA();
                v1 = nEdge.GetB();

                vX0 = x - v0.GetX();
                vY0 = y - v0.GetY();
                tX = v1.GetX() - v0.GetX();
                tY = v1.GetY() - v0.GetY();
                var pX = -tY;
                var pY = tX;
                var h = pX * vX0 + pY * vY0;

                if (h < _halfPlaneThresholdNeg) break;
                if (h < _halfPlaneThreshold)
                {
                    h = _geometricOps.HalfPlane(v0.GetX(), v0.GetY(), v1.GetX(), v1.GetY(), x, y);
                    if (h <= 0) break;
                }

                edge = nEdge;

                tC = tX * vX0 + tY * vY0;
                if (tC > _halfPlaneThreshold) break;
                if (tC > _halfPlaneThresholdNeg)
                {
                    tC = _geometricOps.Direction(v0.GetX(), v0.GetY(), v1.GetX(), v1.GetY(), x, y);
                    if (tC >= 0) break;
                }
            }
        else

            // The vertex is positioned in a positive direction
            // relative to the exterior-side edge.
            while (iterations++ < MaxPerimeterSearchIterations)
            {
                _nSlwSteps++;

                var nEdge = edge.GetForward().GetForwardFromDual();

                v0 = nEdge.GetA();
                v1 = nEdge.GetB();

                vX0 = x - v0.GetX();
                vY0 = y - v0.GetY();
                tX = v1.GetX() - v0.GetX();
                tY = v1.GetY() - v0.GetY();
                var pX = -tY;
                var pY = tX;
                var h = pX * vX0 + pY * vY0;

                if (h < _halfPlaneThresholdNeg) break;
                if (h < _halfPlaneThreshold)
                {
                    h = _geometricOps.HalfPlane(v0.GetX(), v0.GetY(), v1.GetX(), v1.GetY(), x, y);
                    if (h <= 0) break;
                }

                tC = tX * vX0 + tY * vY0;
                if (tC < _halfPlaneThresholdNeg) break;
                if (tC < _halfPlaneThreshold)
                {
                    tC = _geometricOps.Direction(v0.GetX(), v0.GetY(), v1.GetX(), v1.GetY(), x, y);
                    if (tC <= 0) break;
                }

                edge = nEdge;
            }

        if (iterations >= MaxPerimeterSearchIterations)

            // If we hit the iteration limit, it's likely we're in an infinite loop
            // Just return the current edge as the best we can do
            Debug.WriteLine($"WARNING: FindAssociatedPerimeterEdge reached max iterations ({MaxPerimeterSearchIterations})");

        return edge;
    }

    /// <summary>
    ///     Generates the next pseudo-random number using XORShift algorithm.
    /// </summary>
    /// <returns>A pseudo-random long value</returns>
    private long RandomNext()
    {
        _seed ^= _seed << 21;
        _seed ^= (long)((ulong)_seed >> 35);
        _seed ^= _seed << 4;
        return _seed;
    }

    /// <summary>
    ///     Tests a triangle side for potential transfer and performs the transfer if needed.
    /// </summary>
    /// <param name="sideEdge">The edge representing the side to test</param>
    /// <param name="x">The x coordinate being searched for</param>
    /// <param name="y">The y coordinate being searched for</param>
    /// <param name="nextEdge">The next edge if transfer occurs</param>
    /// <returns>True if transfer occurred; otherwise false</returns>
    private bool TestAndTransfer(IQuadEdge sideEdge, double x, double y, out IQuadEdge nextEdge)
    {
        _nSlwTests++;

        var v1 = sideEdge.GetA();
        var v2 = sideEdge.GetB();

        var vX1 = x - v1.GetX();
        var vY1 = y - v1.GetY();
        var pX1 = v1.GetY() - v2.GetY(); // perpendicular
        var pY1 = v2.GetX() - v1.GetX();
        var h1 = vX1 * pX1 + vY1 * pY1;

        if (h1 < _halfPlaneThresholdNeg)
        {
            nextEdge = sideEdge.GetDual();
            return true;
        }

        if (h1 < _halfPlaneThreshold)
        {
            h1 = _geometricOps.HalfPlane(v1.GetX(), v1.GetY(), v2.GetX(), v2.GetY(), x, y);
            if (h1 < 0)
            {
                nextEdge = sideEdge.GetDual();
                return true;
            }
        }

        nextEdge = sideEdge;
        return false;
    }
}

/// <summary>
///     Contains diagnostic information about Stochastic Lawson's Walk performance.
/// </summary>
public class WalkDiagnostics
{
    /// <summary>
    ///     Gets or sets the average number of steps to completion.
    /// </summary>
    public double AverageStepsToCompletion { get; set; }

    /// <summary>
    ///     Gets or sets the number of extended precision calculations performed.
    /// </summary>
    public long NumberOfExtendedPrecisionCalls { get; set; }

    /// <summary>
    ///     Gets or sets the number of walks that went to the exterior.
    /// </summary>
    public int NumberOfExteriorWalks { get; set; }

    /// <summary>
    ///     Gets or sets the number of side tests performed.
    /// </summary>
    public int NumberOfTests { get; set; }

    /// <summary>
    ///     Gets or sets the number of walks performed.
    /// </summary>
    public int NumberOfWalks { get; set; }

    /// <summary>
    ///     Returns a string representation of the diagnostics.
    /// </summary>
    /// <returns>A formatted string with diagnostic information</returns>
    public override string ToString()
    {
        return $"Walks: {NumberOfWalks}, Exterior: {NumberOfExteriorWalks}, "
               + $"Tests: {NumberOfTests}, Extended: {NumberOfExtendedPrecisionCalls}, "
               + $"Avg Steps: {AverageStepsToCompletion:F2}";
    }
}