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

using Tinfour.Core.Edge;

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
    ///     Maximum number of times a single walk may re-enter the solid mesh after the
    ///     exterior classification of its target failed verification (see the ghost-exit
    ///     handling in <see cref="FindAnEdgeFromEnclosingTriangle(EdgeStore,int,double,double)"/>).
    /// </summary>
    private const int MaxGhostReentries = 32;

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

        var qe = (QuadEdge)startingEdge;
        var store = qe.GetStore();
        return store.Wrap(FindAnEdgeFromEnclosingTriangle(store, qe.GetHandle(), x, y));
    }

    /// <summary>
    ///     Handle-native core of the enclosing-triangle search. Reads topology
    ///     and coordinates directly from the store's arrays; no edge or vertex
    ///     objects are touched.
    /// </summary>
    /// <param name="s">The edge store of the TIN being searched</param>
    /// <param name="startingHandle">The handle giving the starting point of the search</param>
    /// <param name="x">The x coordinate of interest</param>
    /// <param name="y">The y coordinate of interest</param>
    /// <returns>The handle of an edge of a triangle containing the coordinates, or the nearest exterior edge</returns>
    internal int FindAnEdgeFromEnclosingTriangle(EdgeStore s, int startingHandle, double x, double y)
    {
        var edge = startingHandle;

        // A caller-cached start edge may have been freed by a subsequent TIN mutation.
        // Freed storage reads as NaN coordinates, which make every orientation test
        // vacuously false and strand the walk; restart from an allocated edge instead.
        if (!s.IsAllocated(edge)) edge = FirstSolidEdge(s, edge);

        // If the starting edge borders the exterior, start from its dual
        if (s.IsNullA(s.Forward(edge) ^ 1)) edge ^= 1;

        _nSlw++;

        var a0x = s.Ax(edge);
        var a0y = s.Ay(edge);
        var b0x = s.Ax(edge ^ 1);
        var b0y = s.Ay(edge ^ 1);

        var vX0 = x - a0x;
        var vY0 = y - a0y;
        var pX0 = a0y - b0y; // perpendicular
        var pY0 = b0x - a0x;

        var h0 = vX0 * pX0 + vY0 * pY0;

        _nSlwTests++;
        if (h0 < _halfPlaneThresholdNeg)
        {
            // Transfer to opposite triangle
            edge ^= 1;
        }
        else if (h0 < _halfPlaneThreshold)
        {
            // Coordinate is close to the edge, use high-precision calculation
            h0 = _geometricOps.HalfPlane(a0x, a0y, b0x, b0y, x, y);
            if (h0 < 0) edge ^= 1;
        }

        var ghostReentries = 0;
        var iterations = 0;

        while (iterations++ < MaxTriangleWalkIterations)
        {
            _nSlwSteps++;

            // Check if we've reached a ghost triangle (exterior)
            if (s.IsNullA(s.Forward(edge) ^ 1))
            {
                var perimeterEdge = FindAssociatedPerimeterEdge(s, edge, x, y);

                // Residual limitation: exhausting the re-entry budget falls back to the
                // unverified pre-fix behaviour (return whatever the perimeter search
                // reported). Each re-entry re-walks with fresh randomization, so reaching
                // the cap for an interior target requires 32 consecutive trapped walks.
                if (ghostReentries >= MaxGhostReentries)
                    return perimeterEdge;

                var verified = FindVerifiedExteriorEdge(s, perimeterEdge, x, y);
                if (verified >= 0)
                    return verified;

                // No nearby hull edge both subtends the target and has it on the ghost
                // side, so the exterior classification is wrong: the walk was trapped in
                // ghost space, either because the start edge no longer borders the solid
                // mesh (a stale cached search edge, or an edge left with ghost triangles
                // on both sides) or because the perimeter search mis-stepped along a
                // near-collinear hull chain (sub-metre collinear segments produced by
                // on-hull vertex insertions). Returning an exterior edge here makes
                // callers insert into the ghost triangle and stitch giant triangles
                // across the TIN interior (crossing contours / double-covered surface).
                // Re-enter the solid mesh and keep walking toward the target.
                ghostReentries++;
                edge = SolidReentryEdge(s, perimeterEdge);
                continue;
            }

            // Test the other two sides of the triangle with randomized order
            var edgeSelection = RandomNext();
            if ((edgeSelection & 1) == 0)
            {
                // Test side 1 first, then side 2
                if (TestAndTransfer(s, s.Forward(edge), x, y, out var nextEdge))
                {
                    edge = nextEdge;
                    continue;
                }

                if (TestAndTransfer(s, s.Reverse(edge), x, y, out nextEdge))
                {
                    edge = nextEdge;
                    continue;
                }
            }
            else
            {
                // Test side 2 first, then side 1
                if (TestAndTransfer(s, s.Reverse(edge), x, y, out var nextEdge))
                {
                    edge = nextEdge;
                    continue;
                }

                if (TestAndTransfer(s, s.Forward(edge), x, y, out nextEdge))
                {
                    edge = nextEdge;
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
    /// <param name="s">The edge store of the TIN being searched</param>
    /// <param name="startingHandle">The starting edge handle for the perimeter search</param>
    /// <param name="x">The x coordinate of interest</param>
    /// <param name="y">The y coordinate of interest</param>
    /// <returns>The handle of the exterior-side edge that subtends the search coordinates</returns>
    private int FindAssociatedPerimeterEdge(EdgeStore s, int startingHandle, double x, double y)
    {
        var edge = startingHandle;
        _nSlwGhost++;

        var v0x = s.Ax(edge);
        var v0y = s.Ay(edge);
        var v1x = s.Ax(edge ^ 1);
        var v1y = s.Ay(edge ^ 1);

        var vX0 = x - v0x;
        var vY0 = y - v0y;
        var tX = v1x - v0x;
        var tY = v1y - v0y;
        var tC = tX * vX0 + tY * vY0;

        if (_halfPlaneThresholdNeg < tC && tC < _halfPlaneThreshold)
            tC = _geometricOps.Direction(v0x, v0y, v1x, v1y, x, y);

        var iterations = 0;

        if (tC < 0)

            // The vertex is retrograde to the starting ghost.
            // Transfer backward along the perimeter.
            while (iterations++ < MaxPerimeterSearchIterations)
            {
                _nSlwSteps++;

                var nEdge = s.Reverse(s.Reverse(edge) ^ 1);

                v0x = s.Ax(nEdge);
                v0y = s.Ay(nEdge);
                v1x = s.Ax(nEdge ^ 1);
                v1y = s.Ay(nEdge ^ 1);

                vX0 = x - v0x;
                vY0 = y - v0y;
                tX = v1x - v0x;
                tY = v1y - v0y;
                var pX = -tY;
                var pY = tX;
                var h = pX * vX0 + pY * vY0;

                if (h < _halfPlaneThresholdNeg) break;
                if (h < _halfPlaneThreshold)
                {
                    h = _geometricOps.HalfPlane(v0x, v0y, v1x, v1y, x, y);
                    if (h <= 0) break;
                }

                edge = nEdge;

                tC = tX * vX0 + tY * vY0;
                if (tC > _halfPlaneThreshold) break;
                if (tC > _halfPlaneThresholdNeg)
                {
                    tC = _geometricOps.Direction(v0x, v0y, v1x, v1y, x, y);
                    if (tC >= 0) break;
                }
            }
        else

            // The vertex is positioned in a positive direction
            // relative to the exterior-side edge.
            while (iterations++ < MaxPerimeterSearchIterations)
            {
                _nSlwSteps++;

                var nEdge = s.Forward(s.Forward(edge) ^ 1);

                v0x = s.Ax(nEdge);
                v0y = s.Ay(nEdge);
                v1x = s.Ax(nEdge ^ 1);
                v1y = s.Ay(nEdge ^ 1);

                vX0 = x - v0x;
                vY0 = y - v0y;
                tX = v1x - v0x;
                tY = v1y - v0y;
                var pX = -tY;
                var pY = tX;
                var h = pX * vX0 + pY * vY0;

                if (h < _halfPlaneThresholdNeg) break;
                if (h < _halfPlaneThreshold)
                {
                    h = _geometricOps.HalfPlane(v0x, v0y, v1x, v1y, x, y);
                    if (h <= 0) break;
                }

                tC = tX * vX0 + tY * vY0;
                if (tC < _halfPlaneThresholdNeg) break;
                if (tC < _halfPlaneThreshold)
                {
                    tC = _geometricOps.Direction(v0x, v0y, v1x, v1y, x, y);
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
    ///     Number of ghost-ring hops examined on each side of the reported perimeter edge
    ///     when verifying an exterior classification.
    /// </summary>
    private const int ExteriorScanRange = 16;

    /// <summary>
    ///     Fractional tolerance on the subtension test: a hull edge's exterior sector is
    ///     taken to cover targets whose projection falls within this fraction of the edge
    ///     length beyond either endpoint, so points over a convex corner are claimed by an
    ///     adjacent edge rather than rejected.
    /// </summary>
    private const double SubtensionTolerance = 0.5;

    /// <summary>
    ///     Verifies an exterior classification and returns a hull edge whose exterior
    ///     sector genuinely contains the target: the target must project onto the edge's
    ///     span (subtension) and lie on the opposite side of the edge from the solid
    ///     triangle's apex. The reported edge is checked first, then the ghost ring is
    ///     scanned a bounded distance in both directions (the perimeter search can stop a
    ///     few edges short on near-collinear hull chains). Points exactly on the hull line
    ///     verify as exterior, preserving the established on-hull insertion behaviour.
    ///     Edges with ghost triangles on both sides cannot be hull edges and never verify.
    /// </summary>
    /// <param name="s">The edge store of the TIN being searched</param>
    /// <param name="perimeterEdge">The exterior-side edge reported by the perimeter search</param>
    /// <param name="x">The x coordinate of the walk target</param>
    /// <param name="y">The y coordinate of the walk target</param>
    /// <returns>The handle of a verified subtending hull edge, or -1 when none is found</returns>
    private int FindVerifiedExteriorEdge(EdgeStore s, int perimeterEdge, double x, double y)
    {
        if (IsVerifiedExteriorEdge(s, perimeterEdge, x, y)) return perimeterEdge;
        if (IsVerifiedCornerWedge(s, perimeterEdge, x, y)) return perimeterEdge;

        // Scan forward and backward along the ghost ring. The hop patterns mirror
        // FindAssociatedPerimeterEdge's traversal of exterior-side edges.
        var forward = perimeterEdge;
        var backward = perimeterEdge;
        for (var i = 0; i < ExteriorScanRange; i++)
        {
            forward = s.Forward(s.Forward(forward) ^ 1);
            if (IsVerifiedExteriorEdge(s, forward, x, y)) return forward;
            if (IsVerifiedCornerWedge(s, forward, x, y)) return forward;

            backward = s.Reverse(s.Reverse(backward) ^ 1);
            if (IsVerifiedExteriorEdge(s, backward, x, y)) return backward;
            if (IsVerifiedCornerWedge(s, backward, x, y)) return backward;
        }

        return -1;
    }

    /// <summary>
    ///     Accepts a hull edge whose CORNER wedge contains the target: the target projects
    ///     beyond one endpoint of the edge, before the start of the adjacent hull edge that
    ///     shares that endpoint, and lies on the ghost side of both. No single hull edge
    ///     subtends a point over a convex corner, so the wedge must be recognised explicitly;
    ///     near-collinear hull chains cannot satisfy the beyond/before pair simultaneously,
    ///     so an interior target trapped by a chain notch is never accepted here.
    /// </summary>
    private bool IsVerifiedCornerWedge(EdgeStore s, int candidate, double x, double y)
    {
        if (!s.IsNullA(s.Forward(candidate) ^ 1)) return false;
        if (!IsOnGhostSide(s, candidate, x, y)) return false;

        var ax = s.Ax(candidate);
        var ay = s.Ay(candidate);
        var bx = s.Ax(candidate ^ 1);
        var by = s.Ay(candidate ^ 1);
        var tX = bx - ax;
        var tY = by - ay;
        var len2 = (tX * tX) + (tY * tY);
        if (len2 <= 0) return false;
        var dot = ((x - ax) * tX) + ((y - ay) * tY);

        if (dot > len2)
        {
            // Beyond the B endpoint: the next hull edge along the ghost ring shares B.
            var next = s.Forward(s.Forward(candidate) ^ 1);
            return IsBeforeSpanStart(s, next, x, y) && IsOnGhostSide(s, next, x, y);
        }

        if (dot < 0)
        {
            // Beyond the A endpoint: the previous hull edge shares A.
            var previous = s.Reverse(s.Reverse(candidate) ^ 1);
            return IsBeyondSpanEnd(s, previous, x, y) && IsOnGhostSide(s, previous, x, y);
        }

        return false;
    }

    private bool IsOnGhostSide(EdgeStore s, int candidate, double x, double y)
    {
        if (!s.IsNullA(s.Forward(candidate) ^ 1)) return false;

        var apex = s.Forward(candidate ^ 1) ^ 1;
        var qx = s.Ax(apex);
        var qy = s.Ay(apex);
        if (double.IsNaN(qx)) return false;

        var ax = s.Ax(candidate);
        var ay = s.Ay(candidate);
        var bx = s.Ax(candidate ^ 1);
        var by = s.Ay(candidate ^ 1);

        var hTarget = HalfPlaneRobust(ax, ay, bx, by, x, y);
        if (hTarget == 0) return true;

        var hApex = HalfPlaneRobust(ax, ay, bx, by, qx, qy);
        return hTarget > 0 != hApex > 0;
    }

    private static bool IsBeforeSpanStart(EdgeStore s, int edge, double x, double y)
    {
        var ax = s.Ax(edge);
        var ay = s.Ay(edge);
        var tX = s.Ax(edge ^ 1) - ax;
        var tY = s.Ay(edge ^ 1) - ay;
        return ((x - ax) * tX) + ((y - ay) * tY) < 0;
    }

    private static bool IsBeyondSpanEnd(EdgeStore s, int edge, double x, double y)
    {
        var ax = s.Ax(edge);
        var ay = s.Ay(edge);
        var tX = s.Ax(edge ^ 1) - ax;
        var tY = s.Ay(edge ^ 1) - ay;
        var len2 = (tX * tX) + (tY * tY);
        return ((x - ax) * tX) + ((y - ay) * tY) > len2;
    }

    /// <summary>
    ///     Tests whether a single exterior-side hull edge's sector contains the target:
    ///     the edge must border a ghost triangle, have a solid triangle on its dual side,
    ///     subtend the target, and have the target on its ghost side.
    /// </summary>
    private bool IsVerifiedExteriorEdge(EdgeStore s, int candidate, double x, double y)
    {
        // Must be the exterior side of a hull edge: ghost apex on its own side.
        if (!s.IsNullA(s.Forward(candidate) ^ 1)) return false;

        var ax = s.Ax(candidate);
        var ay = s.Ay(candidate);
        var bx = s.Ax(candidate ^ 1);
        var by = s.Ay(candidate ^ 1);

        // Subtension: the target must project onto the edge span (with tolerance) —
        // a side-of-line test against a hull edge that does not subtend the target is
        // meaningless because the hull is only locally flat.
        var tX = bx - ax;
        var tY = by - ay;
        var len2 = (tX * tX) + (tY * tY);
        if (len2 <= 0) return false;
        var dot = ((x - ax) * tX) + ((y - ay) * tY);
        if (dot < -SubtensionTolerance * len2 || dot > (1 + SubtensionTolerance) * len2) return false;

        // Ghost-side test also rejects both-sides-ghost edges, which are not hull edges.
        return IsOnGhostSide(s, candidate, x, y);
    }

    /// <summary>
    ///     Computes the half-plane orientation of (x, y) relative to edge (aX, aY)-(bX, bY),
    ///     escalating to the extended-precision calculation inside the ambiguity band —
    ///     the same policy the walk applies to its transfer tests.
    /// </summary>
    private double HalfPlaneRobust(double aX, double aY, double bX, double bY, double x, double y)
    {
        var h = ((x - aX) * (aY - bY)) + ((y - aY) * (bX - aX));
        if (h > _halfPlaneThresholdNeg && h < _halfPlaneThreshold)
            h = _geometricOps.HalfPlane(aX, aY, bX, bY, x, y);
        return h;
    }

    /// <summary>
    ///     Finds an edge bordering a solid triangle from which a walk can (re-)enter the
    ///     mesh. Prefers the dual of the supplied edge; when both of its sides are ghost
    ///     (an edge stranded in ghost space), scans the store for any solid-triangle edge.
    /// </summary>
    /// <param name="s">The edge store of the TIN being searched</param>
    /// <param name="edge">The edge whose neighbourhood failed verification</param>
    /// <returns>An edge whose triangle has a non-ghost apex, or the input when none exists</returns>
    private static int SolidReentryEdge(EdgeStore s, int edge)
    {
        var dual = edge ^ 1;
        if (!s.IsNullA(dual) && !s.IsNullA(dual ^ 1) && !s.IsNullA(s.Forward(dual) ^ 1)) return dual;

        return FirstSolidEdge(s, edge);
    }

    /// <summary>
    ///     Scans the edge store for the first allocated edge that borders a solid
    ///     (non-ghost) triangle. Cold path used only when a walk cannot proceed from its
    ///     given edge (freed storage or ghost triangles on both sides).
    /// </summary>
    /// <param name="s">The edge store of the TIN being searched</param>
    /// <param name="fallback">The handle to return when no solid edge exists</param>
    /// <returns>A solid-triangle edge handle, or the fallback for a degenerate store</returns>
    private static int FirstSolidEdge(EdgeStore s, int fallback)
    {
        for (var pair = 0; pair < s.PairHighWater; pair++)
        {
            var h = pair << 1;
            if (!s.IsAllocated(h)) continue;
            if (s.IsNullA(h) || s.IsNullA(h ^ 1)) continue;
            if (!s.IsNullA(s.Forward(h) ^ 1)) return h;
            if (!s.IsNullA(s.Forward(h ^ 1) ^ 1)) return h ^ 1;
        }

        return fallback;
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
    /// <param name="s">The edge store of the TIN being searched</param>
    /// <param name="sideEdge">The handle of the side to test</param>
    /// <param name="x">The x coordinate being searched for</param>
    /// <param name="y">The y coordinate being searched for</param>
    /// <param name="nextEdge">The next edge handle if transfer occurs</param>
    /// <returns>True if transfer occurred; otherwise false</returns>
    private bool TestAndTransfer(EdgeStore s, int sideEdge, double x, double y, out int nextEdge)
    {
        _nSlwTests++;

        var v1x = s.Ax(sideEdge);
        var v1y = s.Ay(sideEdge);
        var v2x = s.Ax(sideEdge ^ 1);
        var v2y = s.Ay(sideEdge ^ 1);

        var vX1 = x - v1x;
        var vY1 = y - v1y;
        var pX1 = v1y - v2y; // perpendicular
        var pY1 = v2x - v1x;
        var h1 = vX1 * pX1 + vY1 * pY1;

        if (h1 < _halfPlaneThresholdNeg)
        {
            nextEdge = sideEdge ^ 1;
            return true;
        }

        if (h1 < _halfPlaneThreshold)
        {
            h1 = _geometricOps.HalfPlane(v1x, v1y, v2x, v2y, x, y);
            if (h1 < 0)
            {
                nextEdge = sideEdge ^ 1;
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