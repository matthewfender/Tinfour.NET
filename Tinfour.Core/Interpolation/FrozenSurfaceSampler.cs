/*
 * Copyright 2026 ReefMaster Software.
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
 * 07/2026  M. Fender    Created for ticket #800 (cheaper preInterpolateZ)
 *
 * Notes:
 * Replaces the AddConstraints "copy-TIN": a second full IncrementalTin
 * that used to be re-triangulated from GetVertices() purely so that
 * constraint-Z interpolation had a surface that survives CDT mutation.
 * The sampler snapshots the primary EdgeStore's forward links and vertex
 * references instead (two Array.Copy calls, no re-triangulation), so the
 * frozen surface has IDENTICAL handles, topology, and coordinates to the
 * pre-mutation TIN. Point location replicates StochasticLawsonsWalk and
 * the facet evaluation replicates TriangularFacetInterpolator so that a
 * query against the snapshot produces exactly what a query against the
 * pre-mutation TIN would have produced.
 *
 * At 5 M vertices this is ~372 MB retained (int[31 M] + IVertex[31 M])
 * versus ~2.3 GB retained / ~2.9 GB allocated for the copy-TIN path, and
 * the snapshot costs ~0.1 s versus ~7.8 s for the Hilbert re-build.
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Interpolation;

using System.Runtime.CompilerServices;

using Tinfour.Core.Common;
using Tinfour.Core.Edge;

/// <summary>
///     A read-only snapshot of a TIN surface taken before constraint
///     processing mutates the triangulation, exposing triangular-facet
///     interpolation (<see cref="Interpolate" />) and the nearest-vertex
///     fallback (<see cref="GetNearestVertex" />) used to drape Z values
///     onto no-depth constraint vertices and constraint-edge splits.
/// </summary>
/// <remarks>
///     <para>
///         The snapshot copies only the edge store's forward links and
///         A-vertex references (reverse links are derived: every face of a
///         consistent completed TIN, ghost faces included, is a 3-cycle, so
///         <c>reverse(h) == forward(forward(h))</c>). Vertex coordinates and
///         Z values are read through the shared vertex instances, preserving
///         the live <see cref="VertexMergerGroup" /> resolution semantics the
///         copy-TIN path had (both TINs shared vertex objects).
///     </para>
///     <para>
///         Point location replicates <see cref="StochasticLawsonsWalk" />
///         (same thresholds, same extended-precision predicates, same
///         XORShift randomization) against the frozen arrays. Two independent
///         walk states mirror the legacy path, which used one navigator
///         inside <see cref="TriangularFacetInterpolator" /> and a second for
///         the nearest-vertex fallback.
///     </para>
///     <para>
///         Instances are NOT thread-safe (walk state is retained between
///         queries), matching the interpolator contract.
///     </para>
/// </remarks>
internal sealed class FrozenSurfaceSampler : IInterpolatorOverTin
{
    private const int MaxTriangleWalkIterations = 100000;

    private const int MaxPerimeterSearchIterations = 100000;

    // Mirror StochasticLawsonsWalk's verified-exterior constants (bit-compatibility).
    private const int MaxGhostReentries = 32;

    private const int ExteriorScanRange = 16;

    private const double SubtensionTolerance = 0.5;

    /// <summary>
    ///     Forward link per directed handle, copied from the source
    ///     <see cref="EdgeStore" />. Dead pairs keep their NullHandle links;
    ///     the walk can never reach them because no live edge links to them.
    /// </summary>
    private readonly int[] _f;

    /// <summary>
    ///     The A vertex per directed handle (<see cref="Vertex.Null" /> for
    ///     ghost sides), copied from the source <see cref="EdgeStore" />.
    /// </summary>
    private readonly IVertex[] _v;

    private readonly bool _isBootstrapped;

    private readonly GeometricOperations _geoOp;

    private readonly double _halfPlaneThreshold;

    private readonly double _halfPlaneThresholdNeg;

    private readonly double _vertexTolerance2;

    private readonly double _precisionThreshold;

    private readonly VertexValuatorDefault _defaultValuator = new();

    // Independent point-location state per consumer, mirroring the legacy
    // copy-TIN path (interpolator navigator + explicit fallback navigator).
    private int _interpolateSearchHandle;

    private long _interpolateWalkSeed = 1L;

    private int _nearestSearchHandle;

    private long _nearestWalkSeed = 1L;

    private double _nx;

    private double _ny;

    private double _nz;

    private double? _maxInterpolationDistance;

    private double _maxInterpolationDistance2 = double.MaxValue;

    private FrozenSurfaceSampler(
        int[] f,
        IVertex[] v,
        int seedHandle,
        bool isBootstrapped,
        Thresholds thresholds)
    {
        _f = f;
        _v = v;
        _isBootstrapped = isBootstrapped && seedHandle >= 0;
        _geoOp = new GeometricOperations(thresholds);
        _halfPlaneThreshold = thresholds.GetHalfPlaneThreshold();
        _halfPlaneThresholdNeg = -_halfPlaneThreshold;
        _vertexTolerance2 = thresholds.GetVertexTolerance2();
        _precisionThreshold = thresholds.GetPrecisionThreshold();
        _interpolateSearchHandle = seedHandle;
        _nearestSearchHandle = seedHandle;
    }

    /// <inheritdoc />
    public bool ConstrainedRegionsOnly => false;

    /// <inheritdoc />
    public double? MaxInterpolationDistance
    {
        get => _maxInterpolationDistance;
        set
        {
            _maxInterpolationDistance = value;
            _maxInterpolationDistance2 = value.HasValue ? value.Value * value.Value : double.MaxValue;
        }
    }

    /// <summary>
    ///     Takes a frozen snapshot of the store's current topology. The
    ///     store may be mutated freely afterwards; the sampler is unaffected.
    /// </summary>
    /// <param name="store">The edge store of the TIN to snapshot.</param>
    /// <param name="thresholds">The TIN's thresholds.</param>
    /// <param name="isBootstrapped">Whether the TIN is bootstrapped.</param>
    /// <returns>A sampler over the frozen surface.</returns>
    internal static FrozenSurfaceSampler Snapshot(EdgeStore store, Thresholds thresholds, bool isBootstrapped)
    {
        store.SnapshotTopology(out var f, out var v, out var firstLiveHandle);
        return new FrozenSurfaceSampler(f, v, firstLiveHandle, isBootstrapped, thresholds);
    }

    /// <inheritdoc />
    public string GetMethod()
    {
        return "Frozen Facet";
    }

    /// <inheritdoc />
    public double[] GetSurfaceNormal()
    {
        var nS = Math.Sqrt(_nx * _nx + _ny * _ny + _nz * _nz);
        if (nS < 1.0e-20) return new double[0];
        var n = new double[3];
        n[0] = _nx / nS;
        n[1] = _ny / nS;
        n[2] = _nz / nS;
        return n;
    }

    /// <inheritdoc />
    public bool IsSurfaceNormalSupported()
    {
        return true;
    }

    /// <summary>
    ///     Performs triangular-facet interpolation against the frozen
    ///     surface. Replicates <see cref="TriangularFacetInterpolator.Interpolate" />
    ///     expression-for-expression so results match a facet interpolation
    ///     over the pre-mutation TIN.
    /// </summary>
    /// <param name="x">The x coordinate for the interpolation point</param>
    /// <param name="y">The y coordinate for the interpolation point</param>
    /// <param name="valuator">
    ///     A valid valuator for interpreting the z value of each vertex or a
    ///     null value to use the default.
    /// </param>
    /// <returns>
    ///     If the interpolation is successful, a valid floating point value;
    ///     otherwise, a NaN.
    /// </returns>
    public double Interpolate(double x, double y, IVertexValuator? valuator)
    {
        var vq = valuator ?? _defaultValuator;

        if (!_isBootstrapped) return double.NaN;

        var edge = FindEnclosingEdge(ref _interpolateSearchHandle, ref _interpolateWalkSeed, x, y);

        var fwd = Forward(edge);
        var v2h = fwd ^ 1;

        var v0x = Ax(edge);
        var v0y = Ay(edge);
        var v1x = Ax(edge ^ 1);
        var v1y = Ay(edge ^ 1);
        var v2x = Ax(v2h);
        var v2y = Ay(v2h);
        var v2IsNull = double.IsNaN(v2x);

        var sx = x - v0x;
        var sy = y - v0y;

        if (_maxInterpolationDistance.HasValue)
        {
            var d0 = sx * sx + sy * sy;
            var d1 = (x - v1x) * (x - v1x) + (y - v1y) * (y - v1y);
            var d2 = v2IsNull ? double.MaxValue : (x - v2x) * (x - v2x) + (y - v2y) * (y - v2y);
            var minDistSq = Math.Min(d0, Math.Min(d1, d2));
            if (minDistSq > _maxInterpolationDistance2)
                return double.NaN;
        }

        var v0 = _v[edge];
        var v1 = _v[edge ^ 1];

        var z0 = vq.Value(v0);
        var z1 = vq.Value(v1);

        var ax = v1x - v0x;
        var ay = v1y - v0y;
        var az = z1 - z0;

        if (v2IsNull)
        {
            // (x,y) is either on perimeter or outside the TIN.
            // if on perimeter, apply linear interpolation
            _nx = 0;
            _ny = 0;
            _nz = 0;
            var px = -ay; // the perpendicular
            var py = ax;
            var h = (sx * px + sy * py) / Math.Sqrt(ax * ax + ay * ay);
            if (Math.Abs(h) < _precisionThreshold)
            {
                double t;
                if (Math.Abs(ax) > Math.Abs(ay)) t = sx / ax;
                else t = sy / ay;
                return t * (z1 - z0) + z0;
            }

            return double.NaN;
        }

        var v2 = _v[v2h];
        var z2 = vq.Value(v2);

        var bx = v2x - v0x;
        var by = v2y - v0y;
        var bz = z2 - z0;

        _nx = ay * bz - az * by;
        _ny = az * bx - ax * bz;
        _nz = ax * by - ay * bx;

        if (sx * sx + sy * sy < _vertexTolerance2) return z0;

        if ((x - v1x) * (x - v1x) + (y - v1y) * (y - v1y) < _vertexTolerance2) return z1;

        if ((x - v2x) * (x - v2x) + (y - v2y) * (y - v2y) < _vertexTolerance2) return z2;

        if (Math.Abs(_nz) < _precisionThreshold) return (z0 + z1 + z2) / 3.0;

        // solve for pz
        return z0 - (_nx * sx + _ny * sy) / _nz;
    }

    /// <summary>
    ///     Finds the vertex of the enclosing (or perimeter) triangle nearest
    ///     to the coordinates. Replicates
    ///     <see cref="IncrementalTinNavigator.GetNearestVertex" /> against the
    ///     frozen surface. Returns null when the snapshot was taken from a
    ///     non-bootstrapped TIN (the legacy navigator threw in that state; a
    ///     null simply leaves the caller's Z as NaN).
    /// </summary>
    /// <param name="x">The x coordinate</param>
    /// <param name="y">The y coordinate</param>
    /// <returns>The nearest vertex of the located triangle, or null.</returns>
    public IVertex? GetNearestVertex(double x, double y)
    {
        if (!_isBootstrapped) return null;

        var h = FindEnclosingEdge(ref _nearestSearchHandle, ref _nearestWalkSeed, x, y);

        // Check the three vertices of the containing triangle. A ghost apex
        // has NaN coordinates, so its distance comparison is false and it is
        // skipped, exactly as in the navigator.
        IVertex? nearest = null;
        var minDist = double.PositiveInfinity;

        var dax = Ax(h) - x;
        var day = Ay(h) - y;
        var dA = dax * dax + day * day;
        if (dA < minDist)
        {
            minDist = dA;
            nearest = _v[h];
        }

        var dbx = Ax(h ^ 1) - x;
        var dby = Ay(h ^ 1) - y;
        var dB = dbx * dbx + dby * dby;
        if (dB < minDist)
        {
            minDist = dB;
            nearest = _v[h ^ 1];
        }

        var c = Forward(h) ^ 1;
        var dcx = Ax(c) - x;
        var dcy = Ay(c) - y;
        var dC = dcx * dcx + dcy * dcy;
        if (dC < minDist)
        {
            nearest = _v[c];
        }

        return nearest;
    }

    /// <summary>
    ///     No-op: the surface is frozen by construction and unaffected by
    ///     changes to the TIN it was snapshotted from.
    /// </summary>
    public void ResetForChangeToTin()
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Forward(int h) => _f[h];

    /// <summary>
    ///     Mirrors <see cref="StochasticLawsonsWalk" />'s verified-exterior search over the
    ///     frozen arrays: the reported perimeter edge (or a ghost-ring neighbour within scan
    ///     range) qualifies only when it is a hull edge whose sector subtends the target with
    ///     the target on its ghost side.
    /// </summary>
    private int FindVerifiedExteriorEdge(int perimeterEdge, double x, double y)
    {
        if (IsVerifiedExteriorEdge(perimeterEdge, x, y)) return perimeterEdge;
        if (IsVerifiedCornerWedge(perimeterEdge, x, y)) return perimeterEdge;

        var forward = perimeterEdge;
        var backward = perimeterEdge;
        for (var i = 0; i < ExteriorScanRange; i++)
        {
            forward = Forward(Forward(forward) ^ 1);
            if (IsVerifiedExteriorEdge(forward, x, y)) return forward;
            if (IsVerifiedCornerWedge(forward, x, y)) return forward;

            backward = Reverse(Reverse(backward) ^ 1);
            if (IsVerifiedExteriorEdge(backward, x, y)) return backward;
            if (IsVerifiedCornerWedge(backward, x, y)) return backward;
        }

        return -1;
    }

    private bool IsVerifiedExteriorEdge(int candidate, double x, double y)
    {
        if (!IsNullA(Forward(candidate) ^ 1)) return false;

        var ax = Ax(candidate);
        var ay = Ay(candidate);
        var bx = Ax(candidate ^ 1);
        var by = Ay(candidate ^ 1);

        var tX = bx - ax;
        var tY = by - ay;
        var len2 = (tX * tX) + (tY * tY);
        if (len2 <= 0) return false;
        var dot = ((x - ax) * tX) + ((y - ay) * tY);
        if (dot < -SubtensionTolerance * len2 || dot > (1 + SubtensionTolerance) * len2) return false;

        return IsOnGhostSide(candidate, x, y);
    }

    private bool IsVerifiedCornerWedge(int candidate, double x, double y)
    {
        if (!IsNullA(Forward(candidate) ^ 1)) return false;
        if (!IsOnGhostSide(candidate, x, y)) return false;

        var ax = Ax(candidate);
        var ay = Ay(candidate);
        var bx = Ax(candidate ^ 1);
        var by = Ay(candidate ^ 1);
        var tX = bx - ax;
        var tY = by - ay;
        var len2 = (tX * tX) + (tY * tY);
        if (len2 <= 0) return false;
        var dot = ((x - ax) * tX) + ((y - ay) * tY);

        if (dot > len2)
        {
            var next = Forward(Forward(candidate) ^ 1);
            return IsBeforeSpanStart(next, x, y) && IsOnGhostSide(next, x, y);
        }

        if (dot < 0)
        {
            var previous = Reverse(Reverse(candidate) ^ 1);
            return IsBeyondSpanEnd(previous, x, y) && IsOnGhostSide(previous, x, y);
        }

        return false;
    }

    private bool IsOnGhostSide(int candidate, double x, double y)
    {
        if (!IsNullA(Forward(candidate) ^ 1)) return false;

        var apex = Forward(candidate ^ 1) ^ 1;
        var qx = Ax(apex);
        var qy = Ay(apex);
        if (double.IsNaN(qx)) return false;

        var ax = Ax(candidate);
        var ay = Ay(candidate);
        var bx = Ax(candidate ^ 1);
        var by = Ay(candidate ^ 1);

        var hTarget = HalfPlaneRobust(ax, ay, bx, by, x, y);
        if (hTarget == 0) return true;

        var hApex = HalfPlaneRobust(ax, ay, bx, by, qx, qy);
        return hTarget > 0 != hApex > 0;
    }

    private bool IsBeforeSpanStart(int edge, double x, double y)
    {
        var ax = Ax(edge);
        var ay = Ay(edge);
        var tX = Ax(edge ^ 1) - ax;
        var tY = Ay(edge ^ 1) - ay;
        return ((x - ax) * tX) + ((y - ay) * tY) < 0;
    }

    private bool IsBeyondSpanEnd(int edge, double x, double y)
    {
        var ax = Ax(edge);
        var ay = Ay(edge);
        var tX = Ax(edge ^ 1) - ax;
        var tY = Ay(edge ^ 1) - ay;
        var len2 = (tX * tX) + (tY * tY);
        return ((x - ax) * tX) + ((y - ay) * tY) > len2;
    }

    private double HalfPlaneRobust(double aX, double aY, double bX, double bY, double x, double y)
    {
        var h = ((x - aX) * (aY - bY)) + ((y - aY) * (bX - aX));
        if (h > _halfPlaneThresholdNeg && h < _halfPlaneThreshold)
            h = _geoOp.HalfPlane(aX, aY, bX, bY, x, y);
        return h;
    }

    private int SolidReentryEdge(int edge)
    {
        var dual = edge ^ 1;
        if (!IsNullA(dual) && !IsNullA(dual ^ 1) && !IsNullA(Forward(dual) ^ 1)) return dual;

        return FirstSolidEdge(edge);
    }

    private int FirstSolidEdge(int fallback)
    {
        for (var h = 0; h < _f.Length; h += 2)
        {
            if (_f[h] == EdgeStore.NullHandle) continue;
            if (IsNullA(h) || IsNullA(h ^ 1)) continue;
            if (!IsNullA(Forward(h) ^ 1)) return h;
            if (!IsNullA(Forward(h ^ 1) ^ 1)) return h ^ 1;
        }

        return fallback;
    }

    /// <summary>
    ///     Reverse link, derived from the 3-cycle face invariant of a
    ///     consistent completed TIN: reverse(h) == forward(forward(h)).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Reverse(int h) => _f[_f[h]];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double Ax(int h) => _v[h].X;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double Ay(int h) => _v[h].Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsNullA(int h) => double.IsNaN(_v[h].X);

    /// <summary>
    ///     Replicates <see cref="StochasticLawsonsWalk.FindAnEdgeFromEnclosingTriangle(EdgeStore,int,double,double)" />
    ///     against the frozen arrays: same thresholds, same extended-precision
    ///     fallbacks, same XORShift side-selection.
    /// </summary>
    private int FindEnclosingEdge(ref int searchHandle, ref long seed, double x, double y)
    {
        var edge = searchHandle;

        // Intentional asymmetry vs StochasticLawsonsWalk: no freed-start (IsAllocated)
        // guard here — the frozen arrays are immutable for the sampler's lifetime, so a
        // cached search handle can never be freed under it.

        // If the starting edge borders the exterior, start from its dual
        if (IsNullA(Forward(edge) ^ 1)) edge ^= 1;

        var a0x = Ax(edge);
        var a0y = Ay(edge);
        var b0x = Ax(edge ^ 1);
        var b0y = Ay(edge ^ 1);

        var vX0 = x - a0x;
        var vY0 = y - a0y;
        var pX0 = a0y - b0y; // perpendicular
        var pY0 = b0x - a0x;

        var h0 = vX0 * pX0 + vY0 * pY0;

        if (h0 < _halfPlaneThresholdNeg)
        {
            // Transfer to opposite triangle
            edge ^= 1;
        }
        else if (h0 < _halfPlaneThreshold)
        {
            // Coordinate is close to the edge, use high-precision calculation
            h0 = _geoOp.HalfPlane(a0x, a0y, b0x, b0y, x, y);
            if (h0 < 0) edge ^= 1;
        }

        var ghostReentries = 0;
        var iterations = 0;

        while (iterations++ < MaxTriangleWalkIterations)
        {
            // Check if we've reached a ghost triangle (exterior)
            if (IsNullA(Forward(edge) ^ 1))
            {
                // Mirrors StochasticLawsonsWalk's verified-exterior handling so the
                // frozen walk stays bit-compatible with the live walk: an exterior
                // classification must name a hull edge whose sector genuinely contains
                // the target; otherwise re-enter the solid mesh and keep walking.
                var perimeterEdge = FindAssociatedPerimeterEdge(edge, x, y);
                if (ghostReentries >= MaxGhostReentries)
                {
                    searchHandle = perimeterEdge;
                    return perimeterEdge;
                }

                var verified = FindVerifiedExteriorEdge(perimeterEdge, x, y);
                if (verified >= 0)
                {
                    searchHandle = verified;
                    return verified;
                }

                ghostReentries++;
                edge = SolidReentryEdge(perimeterEdge);
                continue;
            }

            // Test the other two sides of the triangle with randomized order
            var edgeSelection = RandomNext(ref seed);
            if ((edgeSelection & 1) == 0)
            {
                // Test side 1 first, then side 2
                if (TestAndTransfer(Forward(edge), x, y, out var nextEdge))
                {
                    edge = nextEdge;
                    continue;
                }

                if (TestAndTransfer(Reverse(edge), x, y, out nextEdge))
                {
                    edge = nextEdge;
                    continue;
                }
            }
            else
            {
                // Test side 2 first, then side 1
                if (TestAndTransfer(Reverse(edge), x, y, out var nextEdge))
                {
                    edge = nextEdge;
                    continue;
                }

                if (TestAndTransfer(Forward(edge), x, y, out nextEdge))
                {
                    edge = nextEdge;
                    continue;
                }
            }

            // No transfer occurred - the point is inside this triangle
            searchHandle = edge;
            return edge;
        }

        throw new InvalidOperationException(
            $"Triangle walk exceeded maximum iterations ({MaxTriangleWalkIterations}) - possible infinite loop");
    }

    /// <summary>
    ///     Replicates the perimeter-edge search of
    ///     <see cref="StochasticLawsonsWalk" /> against the frozen arrays.
    /// </summary>
    private int FindAssociatedPerimeterEdge(int startingHandle, double x, double y)
    {
        var edge = startingHandle;

        var v0x = Ax(edge);
        var v0y = Ay(edge);
        var v1x = Ax(edge ^ 1);
        var v1y = Ay(edge ^ 1);

        var vX0 = x - v0x;
        var vY0 = y - v0y;
        var tX = v1x - v0x;
        var tY = v1y - v0y;
        var tC = tX * vX0 + tY * vY0;

        if (_halfPlaneThresholdNeg < tC && tC < _halfPlaneThreshold)
            tC = _geoOp.Direction(v0x, v0y, v1x, v1y, x, y);

        var iterations = 0;

        if (tC < 0)

            // The vertex is retrograde to the starting ghost.
            // Transfer backward along the perimeter.
            while (iterations++ < MaxPerimeterSearchIterations)
            {
                var nEdge = Reverse(Reverse(edge) ^ 1);

                v0x = Ax(nEdge);
                v0y = Ay(nEdge);
                v1x = Ax(nEdge ^ 1);
                v1y = Ay(nEdge ^ 1);

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
                    h = _geoOp.HalfPlane(v0x, v0y, v1x, v1y, x, y);
                    if (h <= 0) break;
                }

                edge = nEdge;

                tC = tX * vX0 + tY * vY0;
                if (tC > _halfPlaneThreshold) break;
                if (tC > _halfPlaneThresholdNeg)
                {
                    tC = _geoOp.Direction(v0x, v0y, v1x, v1y, x, y);
                    if (tC >= 0) break;
                }
            }
        else

            // The vertex is positioned in a positive direction
            // relative to the exterior-side edge.
            while (iterations++ < MaxPerimeterSearchIterations)
            {
                var nEdge = Forward(Forward(edge) ^ 1);

                v0x = Ax(nEdge);
                v0y = Ay(nEdge);
                v1x = Ax(nEdge ^ 1);
                v1y = Ay(nEdge ^ 1);

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
                    h = _geoOp.HalfPlane(v0x, v0y, v1x, v1y, x, y);
                    if (h <= 0) break;
                }

                tC = tX * vX0 + tY * vY0;
                if (tC < _halfPlaneThresholdNeg) break;
                if (tC < _halfPlaneThreshold)
                {
                    tC = _geoOp.Direction(v0x, v0y, v1x, v1y, x, y);
                    if (tC <= 0) break;
                }

                edge = nEdge;
            }

        return edge;
    }

    private bool TestAndTransfer(int sideEdge, double x, double y, out int nextEdge)
    {
        var v1x = Ax(sideEdge);
        var v1y = Ay(sideEdge);
        var v2x = Ax(sideEdge ^ 1);
        var v2y = Ay(sideEdge ^ 1);

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
            h1 = _geoOp.HalfPlane(v1x, v1y, v2x, v2y, x, y);
            if (h1 < 0)
            {
                nextEdge = sideEdge ^ 1;
                return true;
            }
        }

        nextEdge = sideEdge;
        return false;
    }

    /// <summary>
    ///     XORShift step identical to <see cref="StochasticLawsonsWalk" />.
    /// </summary>
    private static long RandomNext(ref long seed)
    {
        seed ^= seed << 21;
        seed ^= (long)((ulong)seed >> 35);
        seed ^= seed << 4;
        return seed;
    }
}
