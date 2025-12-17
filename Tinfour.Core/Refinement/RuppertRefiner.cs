/*
 * Copyright 2025 Gary W. Lucas.
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
 * 10/2025  M. Carleton  Created (Java)
 * 12/2025  M. Fender    Ported to C#
 *
 * Notes:
 *   Implements Ruppert's Delaunay refinement algorithm for improving mesh
 *   quality. Uses Shewchuk's off-center technique and includes sophisticated
 *   handling of pathological cases via seditious edge detection and shell
 *   indexing.
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Refinement;

using System.Runtime.CompilerServices;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;

/// <summary>
///     Implements Ruppert's Delaunay refinement algorithm for improving mesh quality.
/// </summary>
/// <remarks>
///     <para>
///         The refiner iteratively refines poor-quality triangles by inserting Steiner
///         points (Shewchuk-style off-centers with circumcenter fallback) and by
///         splitting encroached constrained subsegments. Several practical safeguards
///         are included to avoid pathological infinite refinement:
///     </para>
///     <list type="bullet">
///         <item>Radius-edge gating (configurable; optionally enforces ρ ≥ √2)</item>
///         <item>Concentric-shell segment tagging for robust off-center/midpoint handling</item>
///         <item>Identification of seditious edges and optional skipping/ignoring</item>
///         <item>Scale-aware tolerances for encroachment and near-vertex checks</item>
///     </list>
///     <para>
///         References:
///     </para>
///     <list type="bullet">
///         <item>J. Ruppert, "A Delaunay Refinement Algorithm for Quality 2-Dimensional Mesh Generation", J. Algorithms (1995)</item>
///         <item>J. R. Shewchuk, "Delaunay Refinement Mesh Generation", 1997</item>
///     </list>
/// </remarks>
public class RuppertRefiner : IDelaunayRefiner
{
    #region Constants

    private const double DefaultMinTriangleArea = 1e-3;
    private const double NearVertexRelTol = 1e-9;
    private const double NearEdgeRelTol = 1e-9;
    private const double ShellBase = 2.0;
    private const double ShellEps = 1e-9;
    private const double SmallCornerDeg = 60.0;
    private static readonly double Sqrt2 = Math.Sqrt(2.0);

    #endregion

    #region Fields

    private readonly IIncrementalTin _tin;
    private readonly IIncrementalTinNavigator _navigator;
    private readonly TriangularFacetInterpolator? _interpolator;

    private readonly double _minAngleRad;
    private readonly double _beta;
    private readonly double _rhoTarget;
    private readonly double _rhoMin;
    private readonly double _minTriangleArea;
    private readonly int _maxIterations;

    private readonly bool _skipSeditiousTriangles;
    private readonly bool _ignoreSeditiousEncroachments;
    private readonly bool _interpolateZ;

    // Vertex metadata tracking
    private readonly Dictionary<IVertex, VData> _vdata;
    private Dictionary<IVertex, CornerInfo> _cornerInfo;

    // Live set of constrained subsegments
    private readonly HashSet<IQuadEdge> _constrainedSegments;
    private bool _constrainedSegmentsInitialized;

    // Bad triangle priority queue (worst first - we negate priority for min-heap)
    private readonly PriorityQueue<BadTri, double> _badTriangles;
    private bool _badTrianglesInitialized;

    // Encroachment queue
    private readonly Queue<IQuadEdge> _encroachedSegmentQueue;
    private readonly HashSet<IQuadEdge> _inEncroachmentQueue;

    private IVertex? _lastInsertedVertex;
    private int _vertexIndexer;

    // Original bounds at start of refinement - used for sanity checking
    private double _originalMaxCoord;
    private bool _originalBoundsSet;

    #endregion

    #region Inner Types

    /// <summary>
    ///     Vertex creation type classification.
    /// </summary>
    private enum VType
    {
        Input,
        Midpoint,
        Offcenter,
        Circumcenter
    }

    /// <summary>
    ///     Metadata for each vertex in the TIN.
    /// </summary>
    private sealed class VData
    {
        public VType Type { get; }
        public IVertex? Corner { get; }
        public int Shell { get; }

        public VData(VType type, IVertex? corner, int shell)
        {
            Type = type;
            Corner = corner;
            Shell = shell;
        }
    }

    /// <summary>
    ///     Corner angle information for seditious edge detection.
    /// </summary>
    private sealed class CornerInfo
    {
        public double MinAngleDeg { get; set; } = 180.0;
    }

    /// <summary>
    ///     Priority queue entry for a bad triangle.
    /// </summary>
    private readonly record struct BadTri(IQuadEdge RepEdge, double Priority);

    #endregion

    #region Constructors

    /// <summary>
    ///     Creates a RuppertRefiner with the specified minimum angle.
    /// </summary>
    /// <param name="tin">The TIN to refine (must be bootstrapped)</param>
    /// <param name="minAngleDegrees">Minimum angle threshold in degrees (0 to 60)</param>
    public RuppertRefiner(IIncrementalTin tin, double minAngleDegrees)
        : this(tin, new RuppertOptions(minAngleDegrees))
    {
    }

    /// <summary>
    ///     Creates a RuppertRefiner with the specified options.
    /// </summary>
    /// <param name="tin">The TIN to refine (must be bootstrapped)</param>
    /// <param name="options">Configuration options for the refinement</param>
    public RuppertRefiner(IIncrementalTin tin, RuppertOptions options)
    {
        ArgumentNullException.ThrowIfNull(tin);
        ArgumentNullException.ThrowIfNull(options);

        if (!tin.IsBootstrapped())
            throw new ArgumentException("TIN must be bootstrapped before refinement", nameof(tin));

        if (options.MinimumAngleDegrees <= 0 || options.MinimumAngleDegrees >= 60)
            throw new ArgumentOutOfRangeException(nameof(options), "MinimumAngleDegrees must be in (0, 60)");

        if (!double.IsFinite(options.MinimumTriangleArea) || options.MinimumTriangleArea < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "MinimumTriangleArea must be finite and >= 0");

        _tin = tin;
        _navigator = tin.GetNavigator();
        _interpolateZ = options.InterpolateZ;
        _interpolator = _interpolateZ ? new TriangularFacetInterpolator(tin) : null;

        _minAngleRad = options.MinimumAngleDegrees * Math.PI / 180.0;
        var sinT = Math.Sin(_minAngleRad);
        _beta = 1.0 / (2.0 * sinT);
        _rhoTarget = 1.0 / (2.0 * sinT);
        _rhoMin = options.EnforceSqrt2Guard ? Math.Max(Sqrt2, _rhoTarget) : _rhoTarget;

        _minTriangleArea = options.MinimumTriangleArea;
        _maxIterations = options.MaxIterations;
        _skipSeditiousTriangles = options.SkipSeditiousTriangles;
        _ignoreSeditiousEncroachments = options.IgnoreSeditiousEncroachments;

        // Initialize collections with reference equality comparison for edges
        _vdata = new Dictionary<IVertex, VData>(ReferenceEqualityComparer.Instance);
        _cornerInfo = new Dictionary<IVertex, CornerInfo>(ReferenceEqualityComparer.Instance);
        _constrainedSegments = new HashSet<IQuadEdge>(ReferenceEqualityComparer.Instance);
        _badTriangles = new PriorityQueue<BadTri, double>();
        _encroachedSegmentQueue = new Queue<IQuadEdge>();
        _inEncroachmentQueue = new HashSet<IQuadEdge>(ReferenceEqualityComparer.Instance);

        // Initialize vertex metadata for existing vertices
        foreach (var v in tin.GetVertices())
        {
            _vdata[v] = new VData(VType.Input, null, 0);
        }

        // Initialize constrained segments and corner info
        InitConstrainedSegments();
        _cornerInfo = BuildCornerInfo();

        // Find max vertex index for new vertex creation
        var maxIndex = 0;
        foreach (var v in tin.GetVertices())
        {
            if (v.GetIndex() > maxIndex)
                maxIndex = v.GetIndex();
        }
        _vertexIndexer = maxIndex + 1;
    }

    /// <summary>
    ///     Factory method to create a refiner from a circumradius-to-edge ratio.
    /// </summary>
    /// <param name="tin">The TIN to refine</param>
    /// <param name="ratio">Target circumradius-to-shortest-edge ratio (must be > 0)</param>
    /// <returns>A configured RuppertRefiner</returns>
    public static RuppertRefiner FromEdgeRatio(IIncrementalTin tin, double ratio)
    {
        if (ratio <= 0)
            throw new ArgumentOutOfRangeException(nameof(ratio), "Ratio must be > 0");

        var minAngleDeg = 180.0 / Math.PI * Math.Asin(1.0 / (2.0 * ratio));
        return new RuppertRefiner(tin, minAngleDeg);
    }

    #endregion

    #region Public Interface (IDelaunayRefiner)

    /// <inheritdoc />
    public bool Refine()
    {
        System.Diagnostics.Debug.WriteLine("=== RUPPERT REFINE() STARTING ===");
        System.Diagnostics.Debug.WriteLine($"  TIN vertices: {_tin.GetVertices().Count}");
        System.Diagnostics.Debug.WriteLine($"  TIN edges: {_tin.GetEdges().Count}");
        System.Diagnostics.Debug.WriteLine($"  Min angle threshold: {_minAngleRad} radians ({_minAngleRad * 180.0 / Math.PI:F1} degrees)");
        System.Diagnostics.Debug.WriteLine($"  Constrained segments initialized: {_constrainedSegmentsInitialized}");
        System.Diagnostics.Debug.WriteLine($"  Bad triangles initialized: {_badTrianglesInitialized}");

        // Capture original bounds for sanity checking - do this BEFORE any refinement
        if (!_originalBoundsSet)
        {
            var bounds = _tin.GetBounds();
            if (bounds.HasValue)
            {
                var (left, top, width, height) = bounds.Value;
                var boundsSize = Math.Max(width, height);
                _originalMaxCoord = Math.Max(Math.Abs(left), Math.Max(Math.Abs(top),
                                    Math.Max(Math.Abs(left + width), Math.Abs(top + height)))) + boundsSize * 10;
                _originalBoundsSet = true;
                System.Diagnostics.Debug.WriteLine($"  Original max coordinate set to: {_originalMaxCoord:E2}");
            }
        }

        if (!_badTrianglesInitialized)
            InitBadTriangleQueue();

        System.Diagnostics.Debug.WriteLine($"  Bad triangles queue size: {_badTriangles.Count}");
        System.Diagnostics.Debug.WriteLine($"  Encroached segments queue size: {_encroachedSegmentQueue.Count}");
        System.Diagnostics.Debug.WriteLine($"  Constrained segments count: {_constrainedSegments.Count}");
        System.Diagnostics.Debug.WriteLine("=================================");

        var iterations = 0;
        var maxIter = _maxIterations > 0 ? _maxIterations : _vdata.Count * 200;

        // Debug: track what we're doing
        var lastBadTriCount = _badTriangles.Count;
        var stuckCount = 0;

        while (iterations++ < maxIter)
        {
            // Debug every iteration for first 50
            if (iterations <= 50)
                System.Diagnostics.Debug.WriteLine($"--- Iteration {iterations} starting ---");

            var v = RefineOnce();

            if (iterations <= 50)
                System.Diagnostics.Debug.WriteLine($"--- Iteration {iterations} done, v={(v != null ? $"({v.X:F2},{v.Y:F2})" : "null")} ---");

            if (v == null)
                return true;

            // Debug: detect if we're stuck
            if (iterations % 100 == 0)
            {
                System.Diagnostics.Debug.WriteLine($"Ruppert iteration {iterations}: bad queue={_badTriangles.Count}, encroached queue={_encroachedSegmentQueue.Count}, vertices={_tin.GetVertices().Count}");
            }

            // Detect if queue size isn't decreasing
            if (_badTriangles.Count >= lastBadTriCount)
            {
                stuckCount++;
                if (stuckCount > 100 && stuckCount % 100 == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"WARNING: Potentially stuck at iteration {iterations}, queue not decreasing. Count={_badTriangles.Count}");
                }
            }
            else
            {
                stuckCount = 0;
                lastBadTriCount = _badTriangles.Count;
            }
        }

        return false;
    }

    private static int _refineOnceCount = 0;

    /// <inheritdoc />
    public IVertex? RefineOnce()
    {
        _refineOnceCount++;

        if (!_constrainedSegmentsInitialized)
            InitConstrainedSegments();

        // First priority: split encroached segments
        var enc = FindEncroachedSegment();
        if (enc != null)
        {
            if (_refineOnceCount <= 10 || _refineOnceCount % 100 == 1)
                System.Diagnostics.Debug.WriteLine($"RefineOnce #{_refineOnceCount}: Splitting encroached segment");
            var result = SplitSegmentSmart(enc);
            if (_refineOnceCount <= 10 || _refineOnceCount % 100 == 1)
                System.Diagnostics.Debug.WriteLine($"  SplitSegmentSmart returned: {(result != null ? $"vertex at ({result.X:F2},{result.Y:F2})" : "null")}");
            return result;
        }

        // Second priority: fix bad triangles
        if (!_badTrianglesInitialized)
            InitBadTriangleQueue();

        var bad = NextBadTriangleFromQueue();
        if (bad != null)
        {
            if (_refineOnceCount <= 10 || _refineOnceCount % 100 == 1)
                System.Diagnostics.Debug.WriteLine($"RefineOnce #{_refineOnceCount}: Inserting offcenter for bad triangle");
            var result = InsertOffcenterOrSplit(bad);
            if (_refineOnceCount <= 10 || _refineOnceCount % 100 == 1)
                System.Diagnostics.Debug.WriteLine($"  InsertOffcenterOrSplit returned: {(result != null ? $"vertex at ({result.X:F2},{result.Y:F2})" : "null")}");
            return result;
        }

        System.Diagnostics.Debug.WriteLine($"RefineOnce #{_refineOnceCount}: Nothing to do, returning null");
        return null;
    }

    #endregion

    #region Initialization Methods

    private void InitConstrainedSegments()
    {
        _constrainedSegments.Clear();
        _encroachedSegmentQueue.Clear();
        _inEncroachmentQueue.Clear();

        foreach (var e in _tin.GetEdgeIterator())
        {
            if (e.IsConstrained())
            {
                _constrainedSegments.Add(e.GetBaseReference());
                if (ClosestEncroacherOrNull(e) != null)
                    AddEncroachmentCandidate(e);
            }
        }
        _constrainedSegmentsInitialized = true;
    }

    private void InitBadTriangleQueue()
    {
        _badTriangles.Clear();

        int totalTriangles = 0;
        int constraintMemberTriangles = 0;
        int badTriangles = 0;
        int rejectedNoMembership = 0;

        foreach (var t in _tin.GetTriangles())
        {
            totalTriangles++;

            // Check if this triangle is in a constraint region
            var edgeA = t.GetEdgeA();
            var edgeB = t.GetEdgeB();
            var edgeC = t.GetEdgeC();
            bool aMember = edgeA.IsConstraintRegionMember();
            bool bMember = edgeB.IsConstraintRegionMember();
            bool cMember = edgeC.IsConstraintRegionMember();

            if (aMember || bMember || cMember)
            {
                constraintMemberTriangles++;
            }

            var p = TriangleBadPriority(t);
            if (p > 0.0)
            {
                badTriangles++;
                var rep = t.GetEdgeA();
                // Use negative priority for min-heap to get max-priority first
                _badTriangles.Enqueue(new BadTri(rep, p), -p);
            }
            else if (p == 0.0 && !aMember && !bMember && !cMember)
            {
                rejectedNoMembership++;
            }
        }

        System.Diagnostics.Debug.WriteLine($"InitBadTriangleQueue: total={totalTriangles}, constraintMember={constraintMemberTriangles}, bad={badTriangles}, rejectedNoMembership={rejectedNoMembership}");
        _badTrianglesInitialized = true;
    }

    private Dictionary<IVertex, CornerInfo> BuildCornerInfo()
    {
        var nbrs = new Dictionary<IVertex, List<IVertex>>(ReferenceEqualityComparer.Instance);

        foreach (var e in _constrainedSegments)
        {
            var a = e.GetA();
            var b = e.GetB();

            if (!nbrs.TryGetValue(a, out var listA))
            {
                listA = new List<IVertex>();
                nbrs[a] = listA;
            }
            listA.Add(b);

            if (!nbrs.TryGetValue(b, out var listB))
            {
                listB = new List<IVertex>();
                nbrs[b] = listB;
            }
            listB.Add(a);
        }

        var info = new Dictionary<IVertex, CornerInfo>(ReferenceEqualityComparer.Instance);

        foreach (var (z, list) in nbrs)
        {
            if (list.Count < 2)
                continue;

            var ci = new CornerInfo();
            var angs = list.Select(w => Math.Atan2(w.Y - z.Y, w.X - z.X)).ToList();

            for (var i = 0; i < angs.Count; i++)
            {
                for (var j = i + 1; j < angs.Count; j++)
                {
                    var angle = AngleSmallBetweenDeg(angs[i], angs[j]);
                    ci.MinAngleDeg = Math.Min(ci.MinAngleDeg, angle);
                }
            }
            info[z] = ci;
        }

        return info;
    }

    #endregion

    #region Triangle Quality Assessment

    private static int _debugTriangleCheckCount = 0;

    private double TriangleBadPriority(SimpleTriangle t)
    {
        if (t.IsGhost())
            return 0.0;

        var constraints = _tin.GetConstraints();

        if (constraints.Count >= 1)
        {
            // Check if triangle is inside a constraint region by verifying edge membership
            var eA = t.GetEdgeA();
            var eB = t.GetEdgeB();
            var eC = t.GetEdgeC();
            var aIsMember = eA.IsConstraintRegionMember();
            var bIsMember = eB.IsConstraintRegionMember();
            var cIsMember = eC.IsConstraintRegionMember();

            if (!aIsMember || !bIsMember || !cIsMember)
            {
                // Debug: log every 1000th rejection to see what's happening
                _debugTriangleCheckCount++;
                if (_debugTriangleCheckCount % 1000 == 1)
                {
                    System.Diagnostics.Debug.WriteLine($"Triangle rejected: edges A={aIsMember}, B={bIsMember}, C={cIsMember}");
                    System.Diagnostics.Debug.WriteLine($"  EdgeA: constrained={eA.IsConstrained()}, border={eA.IsConstraintRegionBorder()}, interior={eA.IsConstraintRegionInterior()}");
                    System.Diagnostics.Debug.WriteLine($"  EdgeB: constrained={eB.IsConstrained()}, border={eB.IsConstraintRegionBorder()}, interior={eB.IsConstraintRegionInterior()}");
                    System.Diagnostics.Debug.WriteLine($"  EdgeC: constrained={eC.IsConstrained()}, border={eC.IsConstraintRegionBorder()}, interior={eC.IsConstraintRegionInterior()}");
                }
                return 0.0;
            }
        }

        var a = t.GetVertexA();
        var b = t.GetVertexB();
        var c = t.GetVertexC();

        var ax = a.X; var ay = a.Y;
        var bx = b.X; var by = b.Y;
        var cx = c.X; var cy = c.Y;

        // AB, AC vectors
        var abx = bx - ax; var aby = by - ay;
        var acx = cx - ax; var acy = cy - ay;

        // |AB|^2, |AC|^2, |BC|^2
        var la = abx * abx + aby * aby;
        var lc = acx * acx + acy * acy;
        var dot = abx * acx + aby * acy;
        var lb = la + lc - 2.0 * dot;

        // cross^2 (double-area squared)
        var cross = abx * acy - aby * acx;
        var cross2 = cross * cross;
        if (cross2 <= 0.0)
            return 0.0;

        // Find shortest edge and product of other two squared sides
        double pairProd;
        IVertex sA, sB;
        if (la <= lb && la <= lc)
        {
            pairProd = lb * lc;
            sA = a;
            sB = b;
        }
        else if (lb <= la && lb <= lc)
        {
            pairProd = la * lc;
            sA = b;
            sB = c;
        }
        else
        {
            pairProd = la * lb;
            sA = c;
            sB = a;
        }

        var threshMul = 4.0 * _rhoMin * _rhoMin;
        var minCross2 = 4.0 * _minTriangleArea * _minTriangleArea;

        // Bad if (R/s) >= rhoMin <=> pairProd >= 4*rhoMin^2 * cross^2
        if (pairProd < threshMul * cross2)
            return 0.0;

        if (_skipSeditiousTriangles && IsSeditious(sA, sB))
            return 0.0;

        // Area must exceed threshold
        if (cross2 <= minCross2)
            return 0.0;

        // Debug: log bad triangles occasionally
        _debugTriangleCheckCount++;
        if (_debugTriangleCheckCount % 500 == 1)
        {
            System.Diagnostics.Debug.WriteLine($"Bad triangle found: vertices=({a.X:F2},{a.Y:F2}), ({b.X:F2},{b.Y:F2}), ({c.X:F2},{c.Y:F2}), priority={cross2:F6}");
        }

        return cross2;
    }

    #endregion

    #region Encroachment Detection

    private void AddEncroachmentCandidate(IQuadEdge e)
    {
        var baseEdge = e.GetBaseReference();
        if (!_inEncroachmentQueue.Contains(baseEdge))
        {
            _inEncroachmentQueue.Add(baseEdge);
            _encroachedSegmentQueue.Enqueue(baseEdge);
        }
    }

    private IQuadEdge? FindEncroachedSegment()
    {
        while (_encroachedSegmentQueue.Count > 0)
        {
            var seg = _encroachedSegmentQueue.Dequeue();
            _inEncroachmentQueue.Remove(seg);

            if (!_constrainedSegments.Contains(seg.GetBaseReference()))
                continue;

            var enc = ClosestEncroacherOrNull(seg);
            if (enc != null)
            {
                if (_ignoreSeditiousEncroachments && ShouldIgnoreEncroachment(seg, enc))
                    continue;
                return seg;
            }
        }
        return null;
    }

    private static IVertex? ClosestEncroacherOrNull(IQuadEdge edge)
    {
        var a = edge.GetA();
        var b = edge.GetB();
        var mx = 0.5 * (a.X + b.X);
        var my = 0.5 * (a.Y + b.Y);
        var r2 = edge.GetLengthSquared() / 4.0;

        IVertex? best = null;
        var bestD2 = double.PositiveInfinity;

        var f = edge.GetForward();
        var c = f.GetB();
        if (!c.IsNullVertex())
        {
            var d2 = c.GetDistanceSq(mx, my);
            if (d2 < bestD2)
            {
                bestD2 = d2;
                best = c;
            }
        }

        var fd = edge.GetDual().GetForward();
        var d = fd.GetB();
        if (!d.IsNullVertex())
        {
            var d2 = d.GetDistanceSq(mx, my);
            if (d2 < bestD2)
            {
                bestD2 = d2;
                best = d;
            }
        }

        return (best != null && bestD2 < r2) ? best : null;
    }

    private IQuadEdge? FirstEncroachedByPoint(IVertex p)
    {
        var tri = _navigator.GetContainingTriangle(p.X, p.Y);
        if (tri == null)
            return null;

        var edges = new[] { tri.GetEdgeA(), tri.GetEdgeB(), tri.GetEdgeC() };

        foreach (var e in edges)
        {
            if (CheckEdge(e, p))
                return e.GetBaseReference();

            var dual = e.GetDual();
            var n1 = dual.GetForward();
            if (CheckEdge(n1, p))
                return n1.GetBaseReference();

            var n2 = n1.GetForward();
            if (CheckEdge(n2, p))
                return n2.GetBaseReference();
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CheckEdge(IQuadEdge e, IVertex p)
    {
        return e.IsConstrained() && IsEncroachedByPoint(e, p);
    }

    private static bool IsEncroachedByPoint(IQuadEdge seg, IVertex p)
    {
        if (!seg.IsConstrained())
            return false;

        var vA = seg.GetA();
        var vB = seg.GetB();
        var midX = (vA.X + vB.X) / 2.0;
        var midY = (vA.Y + vB.Y) / 2.0;

        var lenSq = vA.GetDistanceSq(vB.X, vB.Y);
        var r2 = lenSq / 4.0;

        return p.GetDistanceSq(midX, midY) < r2;
    }

    private IQuadEdge? FirstNearConstrainedEdgeInterior(IVertex v, double tol)
    {
        var tri = _navigator.GetContainingTriangle(v.X, v.Y);
        if (tri == null)
            return null;

        var edges = new[] { tri.GetEdgeA(), tri.GetEdgeB(), tri.GetEdgeC() };

        foreach (var e in edges)
        {
            if (e.IsConstrained() && IsNearEdgeInterior(e, v, tol))
                return e;
        }
        return null;
    }

    private static bool IsNearEdgeInterior(IQuadEdge seg, IVertex px, double tol)
    {
        var a = seg.GetA();
        var b = seg.GetB();

        var ax = a.X; var ay = a.Y;
        var bx = b.X; var by = b.Y;
        var vx = bx - ax; var vy = by - ay;
        var wx = px.X - ax; var wy = px.Y - ay;

        var vv = vx * vx + vy * vy;
        if (vv == 0)
            return false;

        var t = (wx * vx + wy * vy) / vv;

        // Strictly interior check (0 < t < 1)
        if (t <= 0 || t >= 1)
            return false;

        var projx = ax + t * vx;
        var projy = ay + t * vy;
        var distSq = (px.X - projx) * (px.X - projx) + (px.Y - projy) * (px.Y - projy);

        return distSq <= (tol * tol);
    }

    #endregion

    #region Insertion Methods

    private static int _dequeueCount = 0;

    private SimpleTriangle? NextBadTriangleFromQueue()
    {
        while (_badTriangles.Count > 0)
        {
            var bt = _badTriangles.Dequeue();
            var rep = bt.RepEdge;

            var t = new SimpleTriangle(rep);
            var p = TriangleBadPriority(t);
            if (p > 0.0)
            {
                _dequeueCount++;
                if (_dequeueCount % 100 == 1)
                {
                    var a = t.GetVertexA();
                    var b = t.GetVertexB();
                    var c = t.GetVertexC();
                    System.Diagnostics.Debug.WriteLine($"Dequeue bad triangle #{_dequeueCount}: ({a.X:F2},{a.Y:F2})-({b.X:F2},{b.Y:F2})-({c.X:F2},{c.Y:F2}), priority={p:F2}");
                }
                return t;
            }
        }
        return null;
    }

    private IVertex? InsertOffcenterOrSplit(SimpleTriangle tri)
    {
        var a = tri.GetVertexA();
        var b = tri.GetVertexB();
        var c = tri.GetVertexC();

        // Sanity check: verify input triangle has reasonable coordinates using original bounds
        if (_originalBoundsSet)
        {
            bool IsBadVertex(IVertex v) => double.IsNaN(v.X) || double.IsNaN(v.Y) ||
                                           double.IsInfinity(v.X) || double.IsInfinity(v.Y) ||
                                           Math.Abs(v.X) > _originalMaxCoord || Math.Abs(v.Y) > _originalMaxCoord;

            if (IsBadVertex(a) || IsBadVertex(b) || IsBadVertex(c))
            {
                System.Diagnostics.Debug.WriteLine("ERROR: Bad triangle input to InsertOffcenterOrSplit!");
                System.Diagnostics.Debug.WriteLine($"  A=({a.X:E2},{a.Y:E2}), B=({b.X:E2},{b.Y:E2}), C=({c.X:E2},{c.Y:E2})");
                System.Diagnostics.Debug.WriteLine($"  Original max coord: {_originalMaxCoord:E2}");
                return null;
            }
        }

        IVertex p = a, q = b;
        var ab2 = a.GetDistanceSq(b.X, b.Y);
        var bc2 = b.GetDistanceSq(c.X, c.Y);
        var ca2 = c.GetDistanceSq(a.X, a.Y);

        if (bc2 < ab2 && bc2 <= ca2)
        {
            p = b;
            q = c;
        }
        else if (ca2 < ab2 && ca2 <= bc2)
        {
            p = c;
            q = a;
        }

        var len = Math.Sqrt(p.GetDistanceSq(q.X, q.Y));
        if (len <= 0)
            return InsertCircumcenterOrSplit(tri);

        var mx = 0.5 * (p.X + q.X);
        var my = 0.5 * (p.Y + q.Y);

        // Normal perpendicular to PQ, oriented into triangle
        var nx = -(q.Y - p.Y);
        var ny = q.X - p.X;
        var nlen = Hypot(nx, ny);
        if (nlen == 0)
            return InsertCircumcenterOrSplit(tri);

        nx /= nlen;
        ny /= nlen;

        var cc = tri.GetCircumcircle();
        if (cc == null)
            return InsertCircumcenterOrSplit(tri);

        var cx = cc.GetX();
        var cy = cc.GetY();
        var dCirc = Hypot(cx - mx, cy - my);

        var d = Math.Min(dCirc, _beta * len);

        var ox = mx + nx * d;
        var oy = my + ny * d;

        // Sanity check: if offcenter coordinates are outside original bounds, something is wrong
        if (_originalBoundsSet &&
            (double.IsNaN(ox) || double.IsNaN(oy) || double.IsInfinity(ox) || double.IsInfinity(oy) ||
             Math.Abs(ox) > _originalMaxCoord || Math.Abs(oy) > _originalMaxCoord))
        {
            System.Diagnostics.Debug.WriteLine($"WARNING: InsertOffcenter produced invalid coordinates ({ox:E2},{oy:E2})");
            System.Diagnostics.Debug.WriteLine($"  Triangle: A=({a.X:F2},{a.Y:F2}), B=({b.X:F2},{b.Y:F2}), C=({c.X:F2},{c.Y:F2})");
            System.Diagnostics.Debug.WriteLine($"  Circumcircle: ({cx:F2},{cy:F2}), dCirc={dCirc:E2}, d={d:E2}");
            System.Diagnostics.Debug.WriteLine($"  Original max coord: {_originalMaxCoord:E2}");
            return null;
        }

        var oz = _interpolateZ && _interpolator != null ? _interpolator.Interpolate(ox, oy, null) : double.NaN;

        var off = new Vertex(ox, oy, oz, _vertexIndexer++);
        off = off.WithSynthetic(true);

        var enc = FirstEncroachedByPoint(off);
        if (enc != null)
            return SplitSegmentSmart(enc);

        var localScale = Math.Max(1e-12, len);
        var nearVertexTol = NearVertexRelTol * localScale;
        var nearEdgeTol = NearEdgeRelTol * localScale;

        if (_lastInsertedVertex != null && _lastInsertedVertex.GetDistance(off.X, off.Y) <= nearVertexTol)
        {
            System.Diagnostics.Debug.WriteLine($"InsertOffcenter: Too close to last inserted vertex, returning null");
            return null;
        }

        var nearest = NearestNeighbor(ox, oy);
        if (nearest != null && nearest.GetDistance(off.X, off.Y) <= nearVertexTol)
        {
            System.Diagnostics.Debug.WriteLine($"InsertOffcenter: Too close to nearest vertex ({nearest.X:F2},{nearest.Y:F2}), returning null");
            return null;
        }

        var nearEdge = FirstNearConstrainedEdgeInterior(off, nearEdgeTol);
        if (nearEdge != null)
            return SplitSegmentSmart(nearEdge);

        AddVertex(off, VType.Offcenter, null, 0);
        return off;
    }

    private IVertex? InsertCircumcenterOrSplit(SimpleTriangle tri)
    {
        var cc = tri.GetCircumcircle();
        if (cc == null)
            return null;

        var center = new Vertex(cc.GetX(), cc.GetY(), 0);

        var enc = FirstEncroachedByPoint(center);
        if (enc != null)
            return SplitSegmentSmart(enc);

        var shortestEdge = tri.GetShortestEdge();
        var localScale = Math.Max(1e-12, shortestEdge?.GetLength() ?? 1.0);
        var nearVertexTol = NearVertexRelTol * localScale;
        var nearEdgeTol = NearEdgeRelTol * localScale;

        var nearest = NearestNeighbor(center.X, center.Y);
        if (nearest != null && nearest.GetDistance(center.X, center.Y) <= nearVertexTol)
            return null;
        if (_lastInsertedVertex != null && _lastInsertedVertex.GetDistance(center.X, center.Y) <= nearVertexTol)
            return null;

        var nearEdge = FirstNearConstrainedEdgeInterior(center, nearEdgeTol);
        if (nearEdge != null)
            return SplitSegmentSmart(nearEdge);

        var cz = _interpolateZ && _interpolator != null ? _interpolator.Interpolate(center.X, center.Y, null) : double.NaN;
        var centerZ = new Vertex(center.X, center.Y, cz, _vertexIndexer++);
        centerZ = centerZ.WithSynthetic(true);

        AddVertex(centerZ, VType.Circumcenter, null, 0);
        return centerZ;
    }

    private IVertex? SplitSegmentSmart(IQuadEdge seg)
    {
        var a = seg.GetA();
        var b = seg.GetB();

        IVertex? corner = null;
        if (IsCornerCritical(a))
            corner = a;
        else if (IsCornerCritical(b))
            corner = b;

        var z = (a.GetZ() + b.GetZ()) * 0.5;

        // Remove old segment from our set
        var baseSeg = seg.GetBaseReference();
        _constrainedSegments.Remove(baseSeg);

        var v = _tin.SplitEdge(baseSeg, 0.5, z);
        if (v != null)
        {
            var k = corner != null ? ShellIndex(corner, v.X, v.Y) : 0;
            _vdata[v] = new VData(VType.Midpoint, corner, k);
            _lastInsertedVertex = v;

            if (_constrainedSegmentsInitialized)
                UpdateConstrainedSegmentsAroundVertex(v);

            if (_badTrianglesInitialized)
            {
                UpdateBadTrianglesAroundVertex(a, null);
                UpdateBadTrianglesAroundVertex(b, null);
                UpdateBadTrianglesAroundVertex(v, null);
            }
        }
        return v;
    }

    private void AddVertex(IVertex v, VType type, IVertex? corner, int shell)
    {
        _tin.Add(v);
        _lastInsertedVertex = v;
        _vdata[v] = new VData(type, corner, shell);

        // Check for new encroachments
        var t0 = _navigator.GetContainingTriangle(v.X, v.Y);
        if (t0 != null)
        {
            IQuadEdge? seed = null;
            if (ReferenceEquals(t0.GetVertexA(), v))
                seed = t0.GetEdgeA();
            else if (ReferenceEquals(t0.GetVertexB(), v))
                seed = t0.GetEdgeB();
            else if (ReferenceEquals(t0.GetVertexC(), v))
                seed = t0.GetEdgeC();

            if (seed != null)
            {
                int pinwheelCount = 0;
                const int maxPinwheel = 1000;
                foreach (var e in seed.GetPinwheel())
                {
                    if (++pinwheelCount > maxPinwheel)
                    {
                        System.Diagnostics.Debug.WriteLine($"ERROR: AddVertex pinwheel exceeded {maxPinwheel} iterations!");
                        break;
                    }
                    var opposite = e.GetForward();
                    if (opposite.IsConstrained() && ClosestEncroacherOrNull(opposite) != null)
                        AddEncroachmentCandidate(opposite);
                }
            }
        }

        if (_badTrianglesInitialized)
            UpdateBadTrianglesAroundVertex(v, t0);
    }

    private void UpdateBadTrianglesAroundVertex(IVertex v, SimpleTriangle? s)
    {
        var t0 = s ?? _navigator.GetContainingTriangle(v.X, v.Y);
        if (t0 == null)
            return;

        var triEdges = new[] { t0.GetEdgeA(), t0.GetEdgeB(), t0.GetEdgeC() };
        IQuadEdge? seed = null;

        foreach (var e in triEdges)
        {
            if (ReferenceEquals(e.GetA(), v))
            {
                seed = e;
                break;
            }
            if (ReferenceEquals(e.GetB(), v))
            {
                var d = e.GetDual();
                if (ReferenceEquals(d.GetA(), v))
                {
                    seed = d;
                    break;
                }
            }
        }

        if (seed == null)
        {
            var ne = _navigator.GetNeighborEdge(v.X, v.Y);
            if (ne == null)
                return;

            if (ReferenceEquals(ne.GetA(), v))
                seed = ne;
            else if (ReferenceEquals(ne.GetB(), v))
            {
                var d = ne.GetDual();
                if (ReferenceEquals(d.GetA(), v))
                    seed = d;
            }

            if (seed == null)
                return;
        }

        int pinwheelCount = 0;
        const int maxPinwheel = 1000; // Safety limit
        foreach (var e in seed.GetPinwheel())
        {
            if (++pinwheelCount > maxPinwheel)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: UpdateBadTrianglesAroundVertex pinwheel exceeded {maxPinwheel} iterations!");
                break;
            }
            var t = new SimpleTriangle(e);
            var p = TriangleBadPriority(t);
            if (p > 0.0)
                _badTriangles.Enqueue(new BadTri(e, p), -p);
        }
    }

    private void UpdateConstrainedSegmentsAroundVertex(IVertex v)
    {
        var t0 = _navigator.GetContainingTriangle(v.X, v.Y);
        if (t0 == null)
            return;

        var triEdges = new[] { t0.GetEdgeA(), t0.GetEdgeB(), t0.GetEdgeC() };
        IQuadEdge? seed = null;

        foreach (var e in triEdges)
        {
            if (ReferenceEquals(e.GetA(), v))
            {
                seed = e;
                break;
            }
            if (ReferenceEquals(e.GetB(), v))
            {
                var d = e.GetDual();
                if (ReferenceEquals(d.GetA(), v))
                {
                    seed = d;
                    break;
                }
            }
        }

        if (seed == null)
        {
            var ne = _navigator.GetNeighborEdge(v.X, v.Y);
            if (ne == null)
                return;

            if (ReferenceEquals(ne.GetA(), v))
                seed = ne;
            else if (ReferenceEquals(ne.GetB(), v))
            {
                var d = ne.GetDual();
                if (ReferenceEquals(d.GetA(), v))
                    seed = d;
            }

            if (seed == null)
                return;
        }

        int pinwheelCount = 0;
        const int maxPinwheel = 1000;
        foreach (var e in seed.GetPinwheel())
        {
            if (++pinwheelCount > maxPinwheel)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: UpdateConstrainedSegmentsAroundVertex pinwheel exceeded {maxPinwheel} iterations!");
                break;
            }
            if (e.IsConstrained())
            {
                _constrainedSegments.Add(e.GetBaseReference());
                if (ClosestEncroacherOrNull(e) != null)
                    AddEncroachmentCandidate(e);
            }

            var opposite = e.GetForward();
            if (opposite.IsConstrained() && ClosestEncroacherOrNull(opposite) != null)
                AddEncroachmentCandidate(opposite);
        }
    }

    #endregion

    #region Seditious Edge Handling

    private bool IsCornerCritical(IVertex z)
    {
        return _cornerInfo.TryGetValue(z, out var ci) && ci.MinAngleDeg < SmallCornerDeg;
    }

    private bool IsSeditious(IVertex u, IVertex v)
    {
        if (!_vdata.TryGetValue(u, out var mu) || !_vdata.TryGetValue(v, out var mv))
            return false;

        if (mu.Type != VType.Midpoint || mv.Type != VType.Midpoint)
            return false;

        if (mu.Corner == null || !ReferenceEquals(mu.Corner, mv.Corner))
            return false;

        var z = mu.Corner;
        if (!IsCornerCritical(z))
            return false;

        return SameShell(z, u, v);
    }

    private bool ShouldIgnoreEncroachment(IQuadEdge e, IVertex witness)
    {
        var a = e.GetA();
        var b = e.GetB();

        IVertex? corner = null;
        if (IsCornerCritical(a))
            corner = a;
        else if (IsCornerCritical(b))
            corner = b;

        if (corner == null)
            return false;

        var mx = 0.5 * (a.X + b.X);
        var my = 0.5 * (a.Y + b.Y);
        var kMid = ShellIndex(corner, mx, my);
        var kW = ShellIndex(corner, witness.X, witness.Y);

        if (kMid != kW)
            return false;

        return _vdata.TryGetValue(witness, out var mw) &&
               mw.Type == VType.Midpoint &&
               ReferenceEquals(mw.Corner, corner);
    }

    private int ShellIndex(IVertex z, double x, double y)
    {
        var d = Hypot(x - z.X, y - z.Y);
        if (d <= ShellEps)
            return 0;
        return (int)Math.Round(Math.Log(d) / Math.Log(ShellBase));
    }

    private bool SameShell(IVertex z, IVertex a, IVertex b)
    {
        return ShellIndex(z, a.X, a.Y) == ShellIndex(z, b.X, b.Y);
    }

    #endregion

    #region Helper Methods

    private IVertex? NearestNeighbor(double x, double y)
    {
        return _navigator.GetNearestVertex(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Hypot(double dx, double dy)
    {
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double AngleSmallBetweenDeg(double a, double b)
    {
        var d = Math.Abs(a - b);
        d = Math.Min(d, 2 * Math.PI - d);
        return d * 180.0 / Math.PI;
    }

    #endregion
}
