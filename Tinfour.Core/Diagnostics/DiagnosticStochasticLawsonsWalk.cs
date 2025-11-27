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
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Diagnostics;

using Tinfour.Core.Common;

/// <summary>
///     Debug enhanced version of StochasticLawsonsWalk with additional null checking
///     and diagnostic output.
/// </summary>
public class DiagnosticStochasticLawsonsWalk
{
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
    public DiagnosticStochasticLawsonsWalk(double nominalPointSpacing)
    {
        var thresholds = new Thresholds(nominalPointSpacing);
        _geometricOps = new GeometricOperations(thresholds);
        _halfPlaneThreshold = thresholds.GetHalfPlaneThreshold();
        _halfPlaneThresholdNeg = -thresholds.GetHalfPlaneThreshold();
    }

    /// <summary>
    ///     Initializes a new instance with a nominal point spacing of 1.0.
    /// </summary>
    public DiagnosticStochasticLawsonsWalk()
        : this(1.0)
    {
    }

    /// <summary>
    ///     Initializes a new instance using specified thresholds.
    /// </summary>
    /// <param name="thresholds">A valid thresholds object</param>
    public DiagnosticStochasticLawsonsWalk(Thresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);

        _geometricOps = new GeometricOperations(thresholds);
        _halfPlaneThreshold = thresholds.GetHalfPlaneThreshold();
        _halfPlaneThresholdNeg = -thresholds.GetHalfPlaneThreshold();
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
        ConstraintAdditionProcessDiagnostics.EnterMethod(
            "FindAnEdgeFromEnclosingTriangle",
            $"edge={startingEdge.GetIndex()}",
            $"x={x:F3}",
            $"y={y:F3}");

        var edge = startingEdge;
        ConstraintAdditionProcessDiagnostics.LogEdge(edge, "Starting");

        // Ensure we start with an interior-side edge
        var forward = edge.GetForward();
        if (forward == null)
        {
            ConstraintAdditionProcessDiagnostics.Log("WARNING: forward edge is null");
            ConstraintAdditionProcessDiagnostics.ExitMethod(
                "FindAnEdgeFromEnclosingTriangle",
                "ERROR: forward edge is null");
            throw new InvalidOperationException("Forward edge is null in FindAnEdgeFromEnclosingTriangle");
        }

        var forwardB = forward.GetB();
        if (forwardB == null)
        {
            ConstraintAdditionProcessDiagnostics.Log("WARNING: forward.GetB() is null");
            ConstraintAdditionProcessDiagnostics.ExitMethod(
                "FindAnEdgeFromEnclosingTriangle",
                "ERROR: forward.GetB() is null");
            throw new InvalidOperationException("Forward.GetB() is null in FindAnEdgeFromEnclosingTriangle");
        }

        if (forwardB.IsNullVertex())
        {
            ConstraintAdditionProcessDiagnostics.Log("Starting edge has NullVertex at forward.GetB() - using dual");
            var dual = edge.GetDual();
            if (dual == null)
            {
                ConstraintAdditionProcessDiagnostics.Log("WARNING: dual edge is null");
                ConstraintAdditionProcessDiagnostics.ExitMethod(
                    "FindAnEdgeFromEnclosingTriangle",
                    "ERROR: dual edge is null");
                throw new InvalidOperationException("Dual edge is null in FindAnEdgeFromEnclosingTriangle");
            }

            edge = dual;
            ConstraintAdditionProcessDiagnostics.LogEdge(edge, "Using dual");
        }

        _nSlw++;

        var v0 = edge.GetA();
        var v1 = edge.GetB();

        if (v0 == null || v1 == null)
        {
            ConstraintAdditionProcessDiagnostics.Log($"ERROR: v0 is null: {v0 == null}, v1 is null: {v1 == null}");
            ConstraintAdditionProcessDiagnostics.ExitMethod("FindAnEdgeFromEnclosingTriangle", "ERROR: null vertices");
            throw new InvalidOperationException("Invalid starting edge with null vertices");
        }

        if (v0.IsNullVertex() || v1.IsNullVertex())
        {
            ConstraintAdditionProcessDiagnostics.Log(
                $"ERROR: v0 is NullVertex: {v0.IsNullVertex()}, v1 is NullVertex: {v1.IsNullVertex()}");
            ConstraintAdditionProcessDiagnostics.ExitMethod(
                "FindAnEdgeFromEnclosingTriangle",
                "ERROR: NullVertex encountered");
            throw new InvalidOperationException("Invalid starting edge with NullVertex");
        }

        var vX0 = x - v0.GetX();
        var vY0 = y - v0.GetY();
        var pX0 = v0.GetY() - v1.GetY(); // perpendicular
        var pY0 = v1.GetX() - v0.GetX();

        var h0 = vX0 * pX0 + vY0 * pY0;

        _nSlwTests++;
        if (h0 < _halfPlaneThresholdNeg)
        {
            // Transfer to opposite triangle
            var dual = edge.GetDual();
            if (dual == null)
            {
                ConstraintAdditionProcessDiagnostics.Log("WARNING: dual edge is null during transfer");
                ConstraintAdditionProcessDiagnostics.ExitMethod(
                    "FindAnEdgeFromEnclosingTriangle",
                    "ERROR: dual edge is null");
                throw new InvalidOperationException(
                    "Dual edge is null during transfer in FindAnEdgeFromEnclosingTriangle");
            }

            edge = dual;
            v0 = edge.GetA();

            if (v0 == null)
            {
                ConstraintAdditionProcessDiagnostics.Log("ERROR: v0 is null after transfer");
                ConstraintAdditionProcessDiagnostics.ExitMethod(
                    "FindAnEdgeFromEnclosingTriangle",
                    "ERROR: null vertex after transfer");
                throw new InvalidOperationException("Null vertex after transfer in FindAnEdgeFromEnclosingTriangle");
            }

            ConstraintAdditionProcessDiagnostics.Log("Transferred to opposite triangle");
            ConstraintAdditionProcessDiagnostics.LogEdge(edge, "Current");
        }
        else if (h0 < _halfPlaneThreshold)
        {
            // Coordinate is close to the edge, use high-precision calculation
            h0 = _geometricOps.HalfPlane(v0.GetX(), v0.GetY(), v1.GetX(), v1.GetY(), x, y);
            if (h0 < 0)
            {
                var dual = edge.GetDual();
                if (dual == null)
                {
                    ConstraintAdditionProcessDiagnostics.Log(
                        "WARNING: dual edge is null during high-precision transfer");
                    ConstraintAdditionProcessDiagnostics.ExitMethod(
                        "FindAnEdgeFromEnclosingTriangle",
                        "ERROR: dual edge is null");
                    throw new InvalidOperationException(
                        "Dual edge is null during high-precision transfer in FindAnEdgeFromEnclosingTriangle");
                }

                edge = dual;
                v0 = edge.GetA();

                if (v0 == null)
                {
                    ConstraintAdditionProcessDiagnostics.Log("ERROR: v0 is null after high-precision transfer");
                    ConstraintAdditionProcessDiagnostics.ExitMethod(
                        "FindAnEdgeFromEnclosingTriangle",
                        "ERROR: null vertex after transfer");
                    throw new InvalidOperationException(
                        "Null vertex after high-precision transfer in FindAnEdgeFromEnclosingTriangle");
                }

                ConstraintAdditionProcessDiagnostics.Log("Transferred to opposite triangle (high precision)");
                ConstraintAdditionProcessDiagnostics.LogEdge(edge, "Current");
            }
        }

        ConstraintAdditionProcessDiagnostics.Log("Starting triangle walk loop");
        var iterationCount = 0;

        while (true)
        {
            iterationCount++;
            if (iterationCount > 1000)
            {
                ConstraintAdditionProcessDiagnostics.Log(
                    "WARNING: Excessive iterations in triangle walk, possible infinite loop");
                ConstraintAdditionProcessDiagnostics.ExitMethod(
                    "FindAnEdgeFromEnclosingTriangle",
                    "ERROR: excessive iterations");
                throw new InvalidOperationException("Excessive iterations in triangle walk, possible infinite loop");
            }

            _nSlwSteps++;

            // Verify the edge is valid at the start of each iteration
            if (edge == null)
            {
                ConstraintAdditionProcessDiagnostics.Log("ERROR: edge became null during walk");
                ConstraintAdditionProcessDiagnostics.ExitMethod(
                    "FindAnEdgeFromEnclosingTriangle",
                    "ERROR: null edge during walk");
                throw new InvalidOperationException("Edge became null during walk in FindAnEdgeFromEnclosingTriangle");
            }

            // Check if we've reached a ghost triangle (exterior)
            v1 = edge.GetB();
            if (v1 == null)
            {
                ConstraintAdditionProcessDiagnostics.Log("ERROR: v1 (edge.GetB()) is null");
                ConstraintAdditionProcessDiagnostics.ExitMethod("FindAnEdgeFromEnclosingTriangle", "ERROR: null v1");
                throw new InvalidOperationException("Edge.GetB() returned null in FindAnEdgeFromEnclosingTriangle");
            }

            forward = edge.GetForward();
            if (forward == null)
            {
                ConstraintAdditionProcessDiagnostics.Log("ERROR: forward edge is null");
                ConstraintAdditionProcessDiagnostics.ExitMethod(
                    "FindAnEdgeFromEnclosingTriangle",
                    "ERROR: null forward edge");
                throw new InvalidOperationException("Forward edge is null in FindAnEdgeFromEnclosingTriangle");
            }

            var v2 = forward.GetB();
            if (v2 == null)
            {
                ConstraintAdditionProcessDiagnostics.Log("ERROR: v2 (forward.GetB()) is null");
                ConstraintAdditionProcessDiagnostics.ExitMethod("FindAnEdgeFromEnclosingTriangle", "ERROR: null v2");
                throw new InvalidOperationException("Forward.GetB() returned null in FindAnEdgeFromEnclosingTriangle");
            }

            if (v2.IsNullVertex())
            {
                ConstraintAdditionProcessDiagnostics.Log(
                    "Reached exterior (ghost triangle) - calling FindAssociatedPerimeterEdge");
                var result = FindAssociatedPerimeterEdge(edge, x, y);
                ConstraintAdditionProcessDiagnostics.ExitMethod(
                    "FindAnEdgeFromEnclosingTriangle",
                    $"result edge={result.GetIndex()} (perimeter)");
                return result;
            }

            // Test the other two sides of the triangle with randomized order
            var edgeSelection = RandomNext();
            if ((edgeSelection & 1) == 0)
            {
                // Test side 1 first, then side 2
                ConstraintAdditionProcessDiagnostics.Log("Testing side 1 then side 2");

                if (TestAndTransfer(forward, x, y, out var nextEdge1))
                {
                    ConstraintAdditionProcessDiagnostics.Log("Transferred via side 1");
                    edge = nextEdge1;
                    v0 = edge.GetA();

                    if (v0 == null)
                    {
                        ConstraintAdditionProcessDiagnostics.Log("ERROR: v0 is null after side 1 transfer");
                        ConstraintAdditionProcessDiagnostics.ExitMethod(
                            "FindAnEdgeFromEnclosingTriangle",
                            "ERROR: null vertex after transfer");
                        throw new InvalidOperationException(
                            "Null vertex after side 1 transfer in FindAnEdgeFromEnclosingTriangle");
                    }

                    ConstraintAdditionProcessDiagnostics.LogEdge(edge, "Current after side 1 transfer");
                    continue;
                }

                var reverseEdge = edge.GetReverse();
                if (reverseEdge == null)
                {
                    ConstraintAdditionProcessDiagnostics.Log("ERROR: reverse edge is null");
                    ConstraintAdditionProcessDiagnostics.ExitMethod(
                        "FindAnEdgeFromEnclosingTriangle",
                        "ERROR: null reverse edge");
                    throw new InvalidOperationException("Reverse edge is null in FindAnEdgeFromEnclosingTriangle");
                }

                if (TestAndTransfer(reverseEdge, x, y, out var nextEdge2))
                {
                    ConstraintAdditionProcessDiagnostics.Log("Transferred via side 2");
                    edge = nextEdge2;
                    v0 = edge.GetA();

                    if (v0 == null)
                    {
                        ConstraintAdditionProcessDiagnostics.Log("ERROR: v0 is null after side 2 transfer");
                        ConstraintAdditionProcessDiagnostics.ExitMethod(
                            "FindAnEdgeFromEnclosingTriangle",
                            "ERROR: null vertex after transfer");
                        throw new InvalidOperationException(
                            "Null vertex after side 2 transfer in FindAnEdgeFromEnclosingTriangle");
                    }

                    ConstraintAdditionProcessDiagnostics.LogEdge(edge, "Current after side 2 transfer");
                    continue;
                }
            }
            else
            {
                // Test side 2 first, then side 1
                ConstraintAdditionProcessDiagnostics.Log("Testing side 2 then side 1");

                var reverseEdge = edge.GetReverse();
                if (reverseEdge == null)
                {
                    ConstraintAdditionProcessDiagnostics.Log("ERROR: reverse edge is null");
                    ConstraintAdditionProcessDiagnostics.ExitMethod(
                        "FindAnEdgeFromEnclosingTriangle",
                        "ERROR: null reverse edge");
                    throw new InvalidOperationException("Reverse edge is null in FindAnEdgeFromEnclosingTriangle");
                }

                if (TestAndTransfer(reverseEdge, x, y, out var nextEdge2))
                {
                    ConstraintAdditionProcessDiagnostics.Log("Transferred via side 2 (first)");
                    edge = nextEdge2;
                    v0 = edge.GetA();

                    if (v0 == null)
                    {
                        ConstraintAdditionProcessDiagnostics.Log("ERROR: v0 is null after side 2 transfer");
                        ConstraintAdditionProcessDiagnostics.ExitMethod(
                            "FindAnEdgeFromEnclosingTriangle",
                            "ERROR: null vertex after transfer");
                        throw new InvalidOperationException(
                            "Null vertex after side 2 transfer in FindAnEdgeFromEnclosingTriangle");
                    }

                    ConstraintAdditionProcessDiagnostics.LogEdge(edge, "Current after side 2 transfer");
                    continue;
                }

                if (TestAndTransfer(forward, x, y, out var nextEdge1))
                {
                    ConstraintAdditionProcessDiagnostics.Log("Transferred via side 1 (second)");
                    edge = nextEdge1;
                    v0 = edge.GetA();

                    if (v0 == null)
                    {
                        ConstraintAdditionProcessDiagnostics.Log("ERROR: v0 is null after side 1 transfer");
                        ConstraintAdditionProcessDiagnostics.ExitMethod(
                            "FindAnEdgeFromEnclosingTriangle",
                            "ERROR: null vertex after transfer");
                        throw new InvalidOperationException(
                            "Null vertex after side 1 transfer in FindAnEdgeFromEnclosingTriangle");
                    }

                    ConstraintAdditionProcessDiagnostics.LogEdge(edge, "Current after side 1 transfer");
                    continue;
                }
            }

            // No transfer occurred - the point is inside this triangle
            ConstraintAdditionProcessDiagnostics.Log("No transfer occurred - point is inside triangle");
            ConstraintAdditionProcessDiagnostics.LogEdge(edge, "Result");
            ConstraintAdditionProcessDiagnostics.ExitMethod(
                "FindAnEdgeFromEnclosingTriangle",
                $"result edge={edge.GetIndex()} (interior)");
            return edge;
        }
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
    ///     Finds the associated perimeter edge when the search reaches the exterior.
    /// </summary>
    /// <param name="startingEdge">The starting edge for the perimeter search</param>
    /// <param name="x">The x coordinate of interest</param>
    /// <param name="y">The y coordinate of interest</param>
    /// <returns>The exterior-side edge that subtends the search coordinates</returns>
    private IQuadEdge FindAssociatedPerimeterEdge(IQuadEdge startingEdge, double x, double y)
    {
        ConstraintAdditionProcessDiagnostics.EnterMethod(
            "FindAssociatedPerimeterEdge",
            $"edge={startingEdge.GetIndex()}",
            $"x={x:F3}",
            $"y={y:F3}");

        var edge = startingEdge;
        _nSlwGhost++;

        var v0 = edge.GetA();
        if (v0 == null)
        {
            ConstraintAdditionProcessDiagnostics.Log("ERROR: v0 is null");
            ConstraintAdditionProcessDiagnostics.ExitMethod("FindAssociatedPerimeterEdge", "ERROR: null v0");
            throw new InvalidOperationException("Edge.GetA() returned null in FindAssociatedPerimeterEdge");
        }

        if (v0.IsNullVertex())
        {
            // This case should not be reached in normal processing,
            // but is included for robustness.
            ConstraintAdditionProcessDiagnostics.Log("v0 is NullVertex - returning edge as is");
            ConstraintAdditionProcessDiagnostics.ExitMethod(
                "FindAssociatedPerimeterEdge",
                $"result edge={edge.GetIndex()} (v0 is NullVertex)");
            return edge;
        }

        var v1 = edge.GetB();
        if (v1 == null)
        {
            ConstraintAdditionProcessDiagnostics.Log("ERROR: v1 is null");
            ConstraintAdditionProcessDiagnostics.ExitMethod("FindAssociatedPerimeterEdge", "ERROR: null v1");
            throw new InvalidOperationException("Edge.GetB() returned null in FindAssociatedPerimeterEdge");
        }

        var vX0 = x - v0.GetX();
        var vY0 = y - v0.GetY();
        var tX = v1.GetX() - v0.GetX();
        var tY = v1.GetY() - v0.GetY();
        var tC = tX * vX0 + tY * vY0;

        if (_halfPlaneThresholdNeg < tC && tC < _halfPlaneThreshold)
            tC = _geometricOps.Direction(v0.GetX(), v0.GetY(), v1.GetX(), v1.GetY(), x, y);

        if (tC < 0)
        {
            // The vertex is retrograde to the starting ghost.
            // Transfer backward along the perimeter.
            ConstraintAdditionProcessDiagnostics.Log("Perimeter walk: retrograde transfer");
            var iterationCount = 0;

            while (true)
            {
                iterationCount++;
                if (iterationCount > 1000)
                {
                    ConstraintAdditionProcessDiagnostics.Log(
                        "WARNING: Excessive iterations in perimeter walk, possible infinite loop");
                    ConstraintAdditionProcessDiagnostics.ExitMethod(
                        "FindAssociatedPerimeterEdge",
                        "ERROR: excessive iterations");
                    throw new InvalidOperationException(
                        "Excessive iterations in perimeter walk, possible infinite loop");
                }

                _nSlwSteps++;

                var reverse = edge.GetReverse();
                if (reverse == null)
                {
                    ConstraintAdditionProcessDiagnostics.Log("ERROR: reverse edge is null");
                    ConstraintAdditionProcessDiagnostics.ExitMethod(
                        "FindAssociatedPerimeterEdge",
                        "ERROR: null reverse edge");
                    throw new InvalidOperationException("Reverse edge is null in FindAssociatedPerimeterEdge");
                }

                var reverseFromDual = reverse.GetReverseFromDual();
                if (reverseFromDual == null)
                {
                    ConstraintAdditionProcessDiagnostics.Log("ERROR: reverse.GetReverseFromDual() is null");
                    ConstraintAdditionProcessDiagnostics.ExitMethod(
                        "FindAssociatedPerimeterEdge",
                        "ERROR: null reverseFromDual");
                    throw new InvalidOperationException("ReverseFromDual is null in FindAssociatedPerimeterEdge");
                }

                var nEdge = reverseFromDual;

                v0 = nEdge.GetA();
                v1 = nEdge.GetB();
                if (v0 == null || v1 == null || v0.IsNullVertex() || v1.IsNullVertex()) break;

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
        }
        else
        {
            // The vertex is positioned in a positive direction
            // relative to the exterior-side edge.
            ConstraintAdditionProcessDiagnostics.Log("Perimeter walk: positive direction");
            var iterationCount = 0;

            while (true)
            {
                iterationCount++;
                if (iterationCount > 1000)
                {
                    ConstraintAdditionProcessDiagnostics.Log(
                        "WARNING: Excessive iterations in perimeter walk, possible infinite loop");
                    ConstraintAdditionProcessDiagnostics.ExitMethod(
                        "FindAssociatedPerimeterEdge",
                        "ERROR: excessive iterations");
                    throw new InvalidOperationException(
                        "Excessive iterations in perimeter walk, possible infinite loop");
                }

                _nSlwSteps++;

                var forward = edge.GetForward();
                if (forward == null)
                {
                    ConstraintAdditionProcessDiagnostics.Log("ERROR: forward edge is null");
                    ConstraintAdditionProcessDiagnostics.ExitMethod(
                        "FindAssociatedPerimeterEdge",
                        "ERROR: null forward edge");
                    throw new InvalidOperationException("Forward edge is null in FindAssociatedPerimeterEdge");
                }

                var forwardFromDual = forward.GetForwardFromDual();
                if (forwardFromDual == null)
                {
                    ConstraintAdditionProcessDiagnostics.Log("ERROR: forward.GetForwardFromDual() is null");
                    ConstraintAdditionProcessDiagnostics.ExitMethod(
                        "FindAssociatedPerimeterEdge",
                        "ERROR: null forwardFromDual");
                    throw new InvalidOperationException("ForwardFromDual is null in FindAssociatedPerimeterEdge");
                }

                var nEdge = forwardFromDual;

                v0 = nEdge.GetA();
                v1 = nEdge.GetB();
                if (v0.IsNullVertex() || v1.IsNullVertex()) break;

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
        }

        ConstraintAdditionProcessDiagnostics.LogEdge(edge, "Result");
        ConstraintAdditionProcessDiagnostics.ExitMethod(
            "FindAssociatedPerimeterEdge",
            $"result edge={edge.GetIndex()}");
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

        // Validate input
        if (sideEdge == null)
        {
            ConstraintAdditionProcessDiagnostics.Log("TestAndTransfer: sideEdge is null");
            nextEdge = null!;
            return false;
        }

        var v1 = sideEdge.GetA();
        var v2 = sideEdge.GetB();

        // Validate vertices
        if (v1 == null || v2 == null)
        {
            ConstraintAdditionProcessDiagnostics.Log(
                $"TestAndTransfer: v1 is null: {v1 == null}, v2 is null: {v2 == null}");
            nextEdge = sideEdge;
            return false;
        }

        if (v1.IsNullVertex() || v2.IsNullVertex())
        {
            ConstraintAdditionProcessDiagnostics.Log(
                $"TestAndTransfer: v1 is NullVertex: {v1.IsNullVertex()}, v2 is NullVertex: {v2.IsNullVertex()}");
            nextEdge = sideEdge;
            return false;
        }

        // Calculate half-plane test
        var vX1 = x - v1.GetX();
        var vY1 = y - v1.GetY();
        var pX1 = v1.GetY() - v2.GetY(); // perpendicular
        var pY1 = v2.GetX() - v1.GetX();
        var h1 = vX1 * pX1 + vY1 * pY1;

        if (h1 < _halfPlaneThresholdNeg)
        {
            // Need to transfer - validate dual edge
            var dual = sideEdge.GetDual();
            if (dual == null)
            {
                ConstraintAdditionProcessDiagnostics.Log("TestAndTransfer: dual edge is null during transfer");
                nextEdge = sideEdge;
                return false;
            }

            nextEdge = dual;
            return true;
        }

        if (h1 < _halfPlaneThreshold)
        {
            // Need high-precision test
            h1 = _geometricOps.HalfPlane(v1.GetX(), v1.GetY(), v2.GetX(), v2.GetY(), x, y);
            if (h1 < 0)
            {
                // Need to transfer - validate dual edge
                var dual = sideEdge.GetDual();
                if (dual == null)
                {
                    ConstraintAdditionProcessDiagnostics.Log(
                        "TestAndTransfer: dual edge is null during high-precision transfer");
                    nextEdge = sideEdge;
                    return false;
                }

                nextEdge = dual;
                return true;
            }
        }

        nextEdge = sideEdge;
        return false;
    }
}