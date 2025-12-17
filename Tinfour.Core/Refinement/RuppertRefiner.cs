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
    private const double NearVertexRelTol = 1e-9;  // Match Java - 1e-6 was too conservative and rejected too many insertions
    private const double NearEdgeRelTol = 1e-9;    // Match Java - 1e-6 was too conservative and rejected too many insertions
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
    private readonly HashSet<int> _inBadTriangleQueue;  // Track by edge base index for deduplication
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

        // Auto-compute minimum triangle area based on TIN bounds if using default value.
        // This ensures the threshold scales appropriately for any coordinate system
        // (e.g., geographic data in degrees vs. projected data in meters).
        const double defaultMinArea = 1e-3;
        var minArea = options.MinimumTriangleArea;
        if (Math.Abs(minArea - defaultMinArea) < 1e-10)
        {
            // User is using default - compute a sensible value based on data bounds
            var bounds = tin.GetBounds();
            if (bounds.HasValue)
            {
                var (_, _, width, height) = bounds.Value;
                var boundsSize = Math.Max(width, height);
                // Target: triangles with edge length ~= boundsSize / 100000 should still be refineable
                // This allows very fine refinement for geographic data in degrees
                // Minimum edge length = boundsSize / 100000
                // Minimum area (equilateral) ~= (edge^2 * sqrt(3)) / 4 ~= edge^2 / 2
                var minEdge = boundsSize / 100000.0;
                var computedMinArea = minEdge * minEdge / 2.0;
                minArea = computedMinArea;
                System.Diagnostics.Debug.WriteLine($"RuppertRefiner: Auto-computed MinimumTriangleArea = {minArea:E3} (bounds size = {boundsSize:E3})");
            }
        }
        _minTriangleArea = minArea;
        _maxIterations = options.MaxIterations;
        _skipSeditiousTriangles = options.SkipSeditiousTriangles;
        _ignoreSeditiousEncroachments = options.IgnoreSeditiousEncroachments;

        // Initialize collections with reference equality comparison for edges
        _vdata = new Dictionary<IVertex, VData>(ReferenceEqualityComparer.Instance);
        _cornerInfo = new Dictionary<IVertex, CornerInfo>(ReferenceEqualityComparer.Instance);
        _constrainedSegments = new HashSet<IQuadEdge>(ReferenceEqualityComparer.Instance);
        _badTriangles = new PriorityQueue<BadTri, double>();
        _inBadTriangleQueue = new HashSet<int>();
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
        _initialVertexCount = _tin.GetVertices().Count;
        _initialTriangleCount = _tin.CountTriangles().ValidTriangles;

        System.Diagnostics.Debug.WriteLine("=== RUPPERT REFINE() STARTING ===");
        System.Diagnostics.Debug.WriteLine($"  TIN vertices: {_initialVertexCount}");
        System.Diagnostics.Debug.WriteLine($"  TIN triangles: {_initialTriangleCount}");
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

        while (iterations++ < maxIter)
        {
            var v = RefineOnce();

            if (v == null)
                return true;

            // Debug: periodic progress logging
            if (iterations % 1000 == 0)
            {
                System.Diagnostics.Debug.WriteLine($"Ruppert iteration {iterations}: bad queue={_badTriangles.Count}, vertices={_tin.GetVertices().Count}");
            }
        }

        return false;
    }

    /// <inheritdoc />
    public IVertex? RefineOnce()
    {
        if (!_constrainedSegmentsInitialized)
            InitConstrainedSegments();

        // First priority: split encroached segments
        var enc = FindEncroachedSegment();
        if (enc != null)
            return SplitSegmentSmart(enc);

        // Second priority: fix bad triangles
        if (!_badTrianglesInitialized)
            InitBadTriangleQueue();

        // Keep trying bad triangles until we successfully insert one, run out, or hit skip limit
        const int maxSkipsPerCall = 100; // Limit skips to prevent infinite loop in single call
        int skippedThisCall = 0;

        while (skippedThisCall < maxSkipsPerCall)
        {
            var bad = NextBadTriangleFromQueue();
            if (bad == null)
                return null; // No more bad triangles - done

            // Try to insert offcenter or split
            var result = InsertOffcenterOrSplit(bad);
            if (result != null)
                return result; // Success

            // Insertion failed - skip this triangle and try the next
            _insertionFailedCount++;
            skippedThisCall++;
            if (_insertionFailedCount <= 10 || _insertionFailedCount % 1000 == 0)
            {
                System.Diagnostics.Debug.WriteLine($"InsertOffcenterOrSplit returned null (total={_insertionFailedCount}, thisCall={skippedThisCall}) - triangle skipped");
            }
        }

        // Hit skip limit - return a dummy vertex to keep the main loop going
        // This prevents premature termination but avoids infinite loops
        System.Diagnostics.Debug.WriteLine($"WARNING: Hit max skips ({maxSkipsPerCall}) in single RefineOnce call, continuing...");
        return _lastInsertedVertex; // Return last vertex to signal "keep going"
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
        _inBadTriangleQueue.Clear();

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
                EnqueueBadTriangle(rep, p);
            }
            else if (p == 0.0 && !aMember && !bMember && !cMember)
            {
                rejectedNoMembership++;
            }
        }

        System.Diagnostics.Debug.WriteLine($"InitBadTriangleQueue: total={totalTriangles}, constraintMember={constraintMemberTriangles}, bad={badTriangles}, rejectedNoMembership={rejectedNoMembership}");
        _badTrianglesInitialized = true;
    }

    /// <summary>
    ///     Enqueues a bad triangle with deduplication.
    /// </summary>
    private void EnqueueBadTriangle(IQuadEdge repEdge, double priority)
    {
        var baseIdx = repEdge.GetBaseIndex();
        if (_inBadTriangleQueue.Add(baseIdx))
            _badTriangles.Enqueue(new BadTri(repEdge, priority), -priority);
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

    private double TriangleBadPriority(SimpleTriangle t)
    {
        if (t.IsGhost())
        {
            _rejectedGhost++;
            return 0.0;
        }

        var constraints = _tin.GetConstraints();

        if (constraints.Count >= 1)
        {
            // Check if triangle is inside a constraint region by verifying edge membership
            var eA = t.GetEdgeA();
            var eB = t.GetEdgeB();
            var eC = t.GetEdgeC();

            if (!eA.IsConstraintRegionMember() || !eB.IsConstraintRegionMember() || !eC.IsConstraintRegionMember())
            {
                _rejectedNotInRegion++;
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
        {
            // Triangle meets angle criterion - not bad
            _rejectedMeetsAngle++;
            return 0.0;
        }

        if (_skipSeditiousTriangles && IsSeditious(sA, sB))
        {
            _rejectedSeditious++;
            return 0.0;
        }

        // Area must exceed threshold
        if (cross2 <= minCross2)
        {
            _rejectedSmallArea++;
            return 0.0;
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

    private int _dequeueTotal;
    private int _dequeueStillBad;
    private int _dequeueNoLongerBad;
    private int _rejectedNotInRegion;
    private int _rejectedGhost;
    private int _rejectedMeetsAngle;
    private int _rejectedSeditious;
    private int _rejectedSmallArea;
    private int _insertionFailedCount;
    private int _rejectedTooCloseToLast;
    private int _rejectedTooCloseToNearest;
    private int _verticesAdded;
    private int _initialVertexCount;
    private int _initialTriangleCount;

    private SimpleTriangle? NextBadTriangleFromQueue()
    {
        while (_badTriangles.Count > 0)
        {
            var bt = _badTriangles.Dequeue();
            var rep = bt.RepEdge;
            _dequeueTotal++;

            // Remove from deduplication set
            _inBadTriangleQueue.Remove(rep.GetBaseIndex());

            var t = new SimpleTriangle(rep);
            var p = TriangleBadPriority(t);
            if (p > 0.0)
            {
                _dequeueStillBad++;
                return t;
            }
            else
            {
                _dequeueNoLongerBad++;
                // Log why triangles are no longer bad - need to check angle criterion
                if (_dequeueNoLongerBad <= 20 || _dequeueNoLongerBad % 1000 == 0)
                {
                    var a = t.GetVertexA();
                    var b = t.GetVertexB();
                    var c = t.GetVertexC();

                    // Compute angle criterion to see why it's no longer bad
                    var ax = a.X; var ay = a.Y;
                    var bx = b.X; var by = b.Y;
                    var cx = c.X; var cy = c.Y;
                    var abx = bx - ax; var aby = by - ay;
                    var acx = cx - ax; var acy = cy - ay;
                    var la = abx * abx + aby * aby;
                    var lc = acx * acx + acy * acy;
                    var dot = abx * acx + aby * acy;
                    var lb = la + lc - 2.0 * dot;
                    var cross = abx * acy - aby * acx;
                    var cross2 = cross * cross;

                    double pairProd;
                    if (la <= lb && la <= lc) pairProd = lb * lc;
                    else if (lb <= la && lb <= lc) pairProd = la * lc;
                    else pairProd = la * lb;

                    var threshMul = 4.0 * _rhoMin * _rhoMin;
                    var minCross2 = 4.0 * _minTriangleArea * _minTriangleArea;
                    var meetsAngle = pairProd < threshMul * cross2;
                    var areaOk = cross2 > minCross2;

                    // Compute actual angles to verify
                    var lenA = Math.Sqrt(la); // AB
                    var lenB = Math.Sqrt(lb); // BC
                    var lenC = Math.Sqrt(lc); // CA
                    // Angle at A (between AB and AC)
                    var angleA = Math.Acos(dot / (lenA * lenC)) * 180.0 / Math.PI;
                    // Angle at B (between BA and BC)
                    var bax = ax - bx; var bay = ay - by;
                    var bcx = cx - bx; var bcy = cy - by;
                    var dotB = bax * bcx + bay * bcy;
                    var angleB = Math.Acos(dotB / (lenA * lenB)) * 180.0 / Math.PI;
                    // Angle at C
                    var angleC = 180.0 - angleA - angleB;
                    var minAngle = Math.Min(angleA, Math.Min(angleB, angleC));

                    // Check constraint region membership for diagnostics
                    var eA = t.GetEdgeA();
                    var eB = t.GetEdgeB();
                    var eC = t.GetEdgeC();
                    var isMemberA = eA.IsConstraintRegionMember();
                    var isMemberB = eB.IsConstraintRegionMember();
                    var isMemberC = eC.IsConstraintRegionMember();

                    System.Diagnostics.Debug.WriteLine($"Triangle no longer bad #{_dequeueNoLongerBad}: " +
                        $"minAngle={minAngle:F1}° (A={angleA:F1}°, B={angleB:F1}°, C={angleC:F1}°), " +
                        $"meetsAngle={meetsAngle}, areaOk={areaOk}, " +
                        $"constraintMember=[{isMemberA},{isMemberB},{isMemberC}], " +
                        $"rhoMin={_rhoMin:F3}, targetAngle={_minAngleRad * 180.0 / Math.PI:F1}°");
                }
            }
        }

        var finalVertexCount = _tin.GetVertices().Count;
        var finalTriangleCount = _tin.CountTriangles().ValidTriangles;
        System.Diagnostics.Debug.WriteLine($"=== RUPPERT REFINE() COMPLETE ===");
        System.Diagnostics.Debug.WriteLine($"Vertices: {_initialVertexCount} -> {finalVertexCount} (+{_verticesAdded} added)");
        System.Diagnostics.Debug.WriteLine($"Triangles: {_initialTriangleCount} -> {finalTriangleCount} (+{finalTriangleCount - _initialTriangleCount})");
        System.Diagnostics.Debug.WriteLine($"Queue stats: total={_dequeueTotal}, stillBad={_dequeueStillBad}, noLongerBad={_dequeueNoLongerBad}");
        System.Diagnostics.Debug.WriteLine($"Rejection stats: ghost={_rejectedGhost}, notInRegion={_rejectedNotInRegion}, meetsAngle={_rejectedMeetsAngle}, seditious={_rejectedSeditious}, smallArea={_rejectedSmallArea}");
        System.Diagnostics.Debug.WriteLine($"Insertion stats: failed={_insertionFailedCount}, tooCloseToLast={_rejectedTooCloseToLast}, tooCloseToNearest={_rejectedTooCloseToNearest}");
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
            _rejectedTooCloseToLast++;
            return null;
        }

        var nearest = NearestNeighbor(ox, oy);
        if (nearest != null && nearest.GetDistance(off.X, off.Y) <= nearVertexTol)
        {
            _rejectedTooCloseToNearest++;
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
        _verticesAdded++;
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
            {
                EnqueueBadTriangle(e, p);
            }
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
