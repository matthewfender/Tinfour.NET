/*
 * Copyright 2015-2025 Gary W. Lucas.
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
 * 02/2013  G. Lucas     Initial implementation
 * 08/2025 M.Fender     Ported to C# - full incremental implementation
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Standard;

using System.Collections;
using System.Diagnostics;

using Tinfour.Core.Common;
using Tinfour.Core.Edge;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Utils;

/// <summary>
///     A faithful port of the Java IncrementalTin class.
///     Provides an incremental TIN using Delaunay triangulation with edge-based representation.
/// </summary>
/// <remarks>
///     This implementation is a direct port of the Java Tinfour IncrementalTin class,
///     following the same algorithms and data structures.
/// </remarks>
public class IncrementalTin : IIncrementalTin
{
    /// <summary>
    ///     A utility for performing the bootstrap operation.
    /// </summary>
    private readonly BootstrapUtility _bootstrapUtility;

    /// <summary>
    ///     List of constraints applied to the TIN.
    /// </summary>
    private readonly List<IConstraint> _constraintList = new();

    /// <summary>
    ///     The edge pool for managing QuadEdge instances.
    /// </summary>
    private readonly EdgePool _edgePool;

    /// <summary>
    ///     Geometric operations instance.
    /// </summary>
    private readonly GeometricOperations _geoOp;

    /// <summary>
    ///     The nominal point spacing used for threshold calculations.
    /// </summary>
    private readonly double _nominalPointSpacing;

    /// <summary>
    ///     Thresholds instance for precision management.
    /// </summary>
    private readonly Thresholds _thresholds;

    /// <summary>
    ///     A walker for point location.
    /// </summary>
    private readonly StochasticLawsonsWalk _walker;

    private double _boundsMaxX = double.NegativeInfinity;

    private double _boundsMaxY = double.NegativeInfinity;

    /// <summary>
    ///     Bounds tracking.
    /// </summary>
    private double _boundsMinX = double.PositiveInfinity;

    private double _boundsMinY = double.PositiveInfinity;

    /// <summary>
    ///     Indicates if the TIN conforms to the Delaunay criterion.
    /// </summary>
    private bool _isConformant = true;

    /// <summary>
    ///     Indicates if the TIN has been disposed.
    /// </summary>
    private bool _isDisposed;

    /// <summary>
    ///     Indicates if the TIN is locked for modifications.
    /// </summary>
    private bool _isLocked;

    /// <summary>
    ///     Indicates if the TIN is locked due to constraints.
    /// </summary>
    private bool _lockedDueToConstraints;

    private int _nSyntheticVertices;

    /// <summary>
    ///     A reference edge for searching operations.
    /// </summary>
    private IQuadEdge? _searchEdge;

    /// <summary>
    ///     Count of vertices supplied to this TIN (staged and inserted). Useful for diagnostics.
    /// </summary>
    private int _vertexCount;

    /// <summary>
    ///     A temporary list of vertices maintained until the TIN is successfully
    ///     bootstrapped, and then discarded.
    /// </summary>
    private List<IVertex>? _vertexList;

    /// <summary>
    ///     Constructs an incremental TIN using numerical thresholds appropriate for
    ///     the default nominal point spacing of 1 unit.
    /// </summary>
    public IncrementalTin()
        : this(1.0)
    {
    }

    /// <summary>
    ///     Constructs an incremental TIN using numerical thresholds appropriate for
    ///     the specified nominal point spacing.
    /// </summary>
    /// <param name="estimatedPointSpacing">The estimated nominal distance between points</param>
    public IncrementalTin(double estimatedPointSpacing)
    {
        _nominalPointSpacing = estimatedPointSpacing;
        _thresholds = new Thresholds(_nominalPointSpacing);
        _geoOp = new GeometricOperations(_thresholds);
        _bootstrapUtility = new BootstrapUtility(_thresholds);
        _walker = new StochasticLawsonsWalk(_thresholds);
        _edgePool = new EdgePool();
    }

    /// <summary>
    ///     Adds a vertex to the TIN.
    /// </summary>
    /// <param name="vertex">The vertex to add</param>
    /// <returns>True if the TIN is bootstrapped; otherwise false</returns>
    public bool Add(IVertex vertex)
    {
        if (_isLocked) throw new InvalidOperationException("TIN is locked and cannot be modified.");

        if (!IsBootstrapped())
        {
            _vertexList ??= new List<IVertex>();

            // The original Java implementation does not check for duplicates
            // in the pre-bootstrap list. It relies on the bootstrap process
            // to select a valid, non-degenerate triangle.
            _vertexList.Add(vertex);
            _vertexCount++;
            UpdateBounds(vertex);
            return TryBootstrap();
        }

        // The TIN is already bootstrapped. The original Java implementation
        // proceeds directly with the geometric insertion without an explicit
        // check for vertex existence. The vertex is implicitly added to the
        // TIN when it is used to create new edges.
        var edge = InsertVertex(vertex);
        _searchEdge = edge;
        _vertexCount++;
        return true;
    }

    /// <summary>
    ///     Adds a collection of vertices to the TIN.
    /// </summary>
    /// <param name="vertices">The vertices to add</param>
    /// <returns>True if the TIN is bootstrapped; otherwise false</returns>
    public bool Add(IEnumerable<IVertex> vertices)
    {
        if (_isLocked) throw new InvalidOperationException("TIN is locked and cannot be modified.");

        var list = vertices.ToList();
        if (list.Count == 0) return IsBootstrapped();

        if (!IsBootstrapped())
        {
            _vertexList ??= new List<IVertex>();
            foreach (var v in list)
            {
                _vertexList.Add(v);
                _vertexCount++;
                UpdateBounds(v);
            }

            return TryBootstrap();
        }

        // The TIN is already bootstrapped, add vertices incrementally.
        foreach (var v in list)
        {
            // As with the single-vertex add, the Java original does not
            // check for uniqueness here. It is the responsibility of the
            // calling code to ensure vertices are unique if desired.
            var edge = InsertVertex(v);
            _searchEdge = edge;
            _vertexCount++;
        }

        return IsBootstrapped();
    }

    /// <summary>
    ///     Adds a collection of vertices to the TIN with an ordering hint.
    ///     When order is Hilbert, vertices are inserted after HilbertSort to improve walk locality.
    /// </summary>
    /// <param name="vertices">Vertices to add.</param>
    /// <param name="order">Ordering hint.</param>
    /// <returns>True if the TIN was modified; otherwise, false.</returns>
    public bool Add(IEnumerable<IVertex> vertices, VertexOrder order)
    {
        return order == VertexOrder.Hilbert ? Add(HilbertSort.Sort(vertices)) : Add(vertices);
    }

    /// <summary>
    ///     Adds constraints to the TIN.
    /// </summary>
    /// <param name="constraints">The constraints to add</param>
    /// <param name="restoreConformity">Whether to restore Delaunay conformity</param>
    public void AddConstraints(IList<IConstraint> constraints, bool restoreConformity)
    {
        if (_isDisposed) throw new InvalidOperationException("Unable to add constraints after disposal");

        if (_isLocked)
        {
            if (_lockedDueToConstraints)
                throw new InvalidOperationException("Constraints already added - no further additions supported");

            throw new InvalidOperationException("TIN is locked");
        }

        if (constraints == null || constraints.Count == 0) return;

        // Note: AddConstraints should only be called once during the lifetime of a TIN.
        // Multiple calls are not currently supported and may produce undefined results.

        // Maximum constraint index is limited by edge storage capacity (8190)
        if (_constraintList.Count + constraints.Count > 8190)
            throw new ArgumentException("Maximum number of constraints (8190) exceeded");

        _isConformant = false;

        // Phase 1: Complete constraints and add all constraint vertices to the TIN, remapping duplicates
        var polygonConstraints = new List<IConstraint>();
        var linearConstraints = new List<IConstraint>();

        foreach (var cIn in constraints)
        {
            var c = cIn;
            c.Complete();

            var anyRedundant = false;
            foreach (var v in c.GetVertices())
                if (!Add(v))
                    anyRedundant = true;

            if (anyRedundant)
            {
                Debug.WriteLine("Redundant vertice(s)  found");
                var remapped = new List<IVertex>(c.GetVertices().Count);
                var prior = Vertex.Null;
                foreach (var v in c.GetVertices())
                {
                    var m = GetMatchingVertex(v);
                    if (m.Equals(prior))
                        continue; // collapse duplicates introduced by remap
                    remapped.Add(m);
                    prior = m;
                }

                c = c.GetConstraintWithNewGeometry(remapped);
            }

            if (c.DefinesConstrainedRegion()) polygonConstraints.Add(c);
            else linearConstraints.Add(c);
        }

        // Phase 2: Assign indices and add to the master list (regions first)
        var ordered = new List<IConstraint>(polygonConstraints.Count + linearConstraints.Count);
        ordered.AddRange(polygonConstraints);
        ordered.AddRange(linearConstraints);

        var nextIdx = _constraintList.Count;
        foreach (var c in ordered)
        {
            c.SetConstraintIndex(this, nextIdx++);
            _constraintList.Add(c);
        }

        // Phase 3: Process constraints using full CDT algorithm
        var processor = new ConstraintProcessor(_edgePool, _geoOp, _thresholds, _walker);
        var edgesForConstraintList = new List<List<IQuadEdge>>(ordered.Count);
        foreach (var c in ordered)
        {
            var eList = new List<IQuadEdge>();
            edgesForConstraintList.Add(eList);
            _searchEdge = processor.ProcessConstraint(c, eList, _searchEdge);
        }

        // Phase 4: Lock due to constraints and optionally restore conformity
        _lockedDueToConstraints = true;
        if (restoreConformity)
        {
            foreach (var e in GetEdgeIterator()) RestoreConformity((QuadEdge)e);

            _isConformant = true;
        }

        // Phase 5: Flood fill polygon interiors and set a linking edge
        var visited = new BitArray(_edgePool.GetMaximumAllocationIndex() + 1);
        for (var i = 0; i < ordered.Count; i++)
        {
            var c = ordered[i];
            if (!c.DefinesConstrainedRegion()) continue;
            var eList = edgesForConstraintList[i];
            processor.FloodFillConstrainedRegion(c, visited, eList);
            if (eList.Count > 0) c.SetConstraintLinkingEdge(eList[0]);
        }
    }

    /// <summary>
    ///     Convenience method to add vertices after Hilbert ordering.
    /// </summary>
    public bool AddSorted(IEnumerable<IVertex> vertices)
    {
        return Add(vertices, VertexOrder.Hilbert);
    }

    /// <summary>
    ///     Clears all internal state data of the TIN.
    /// </summary>
    public void Clear()
    {
        _vertexList?.Clear();
        _constraintList.Clear();
        _edgePool.Clear();
        _searchEdge = null;
        _isLocked = false;
        _lockedDueToConstraints = false;
        _isConformant = true;
        _vertexCount = 0;

        _boundsMinX = double.PositiveInfinity;
        _boundsMinY = double.PositiveInfinity;
        _boundsMaxX = double.NegativeInfinity;
        _boundsMaxY = double.NegativeInfinity;
    }

    /// <summary>
    ///     Performs a survey of the TIN to gather statistics about the triangles.
    /// </summary>
    /// <returns>A valid instance of the TriangleCount class</returns>
    public TriangleCount CountTriangles()
    {
        var validTriangles = 0;
        var ghostTriangles = 0;
        var constrainedTriangles = 0;

        foreach (var triangle in GetTriangles())
            if (triangle.IsGhost())
            {
                ghostTriangles++;
            }
            else
            {
                validTriangles++;
                if (IsTriangleConstrained(triangle)) constrainedTriangles++;
            }

        return new TriangleCount(validTriangles, ghostTriangles, constrainedTriangles);
    }

    /// <summary>
    ///     Disposes of the TIN and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (!_isDisposed)
        {
            _edgePool.Dispose();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    ///     Gets the bounds of the TIN as a rectangle.
    /// </summary>
    /// <returns>The bounds or null if not bootstrapped</returns>
    public (double Left, double Top, double Width, double Height)? GetBounds()
    {
        if (!IsBootstrapped()) return null;

        return (_boundsMinX, _boundsMinY, _boundsMaxX - _boundsMinX,
                   _boundsMaxY - _boundsMinY);
    }

    /// <summary>
    ///     Gets the constraint associated with the specified index.
    /// </summary>
    /// <param name="index">An arbitrary integer index</param>
    /// <returns>The constraint if found; otherwise, null</returns>
    public IConstraint? GetConstraint(int index)
    {
        return _constraintList.FirstOrDefault((IConstraint c) => c.GetConstraintIndex() == index);
    }

    /// <summary>
    ///     Gets a copy of the list of constraints currently stored in the TIN.
    /// </summary>
    /// <returns>A valid, potentially empty list</returns>
    public IList<IConstraint> GetConstraints()
    {
        return _constraintList.ToList();
    }

    /// <summary>
    ///     Returns an iterator over all edges in the TIN (including ghost edges).
    /// </summary>
    /// <returns>An enumerable collection of edges.</returns>
    public IEnumerable<IQuadEdge> GetEdgeIterator()
    {
        return _edgePool;
    }

    /// <summary>
    ///     Gets a list of edges currently allocated by the TIN instance.
    ///     This method is provided for API parity with tests.
    /// </summary>
    /// <returns>A valid, potentially empty list</returns>
    public IList<IQuadEdge> GetEdges()
    {
        return _edgePool.GetEdges();
    }

    /// <summary>
    ///     Gets the maximum index of the currently allocated edges.
    /// </summary>
    /// <returns>A positive value or zero if the TIN is not bootstrapped</returns>
    public int GetMaximumEdgeAllocationIndex()
    {
        return _edgePool.GetMaximumAllocationIndex();
    }

    /// <summary>
    ///     Gets a navigator for interpolation operations.
    /// </summary>
    /// <returns>A navigator instance for the TIN</returns>
    public IIncrementalTinNavigator GetNavigator()
    {
        return new IncrementalTinNavigator(this);
    }

    /// <summary>
    ///     Gets the nominal point spacing for this instance.
    /// </summary>
    /// <returns>A positive floating point value</returns>
    public double GetNominalPointSpacing()
    {
        return _nominalPointSpacing;
    }

    /// <summary>
    ///     Gets a list of edges currently defining the perimeter of the TIN.
    /// </summary>
    /// <returns>A valid, potentially empty list</returns>
    public IList<IQuadEdge> GetPerimeter()
    {
        var perimeter = new List<IQuadEdge>();
        if (!IsBootstrapped()) return perimeter;

        var ghostEdge = _edgePool.GetStartingGhostEdge();
        if (ghostEdge == null) return perimeter;

        var s0 = ghostEdge.GetReverse();
        var s = s0;
        do
        {
            perimeter.Add(s.GetDual());
            s = s.GetForward().GetForward().GetDual().GetReverse();
        }
        while (s != s0);

        return perimeter;
    }

    /// <summary>
    ///     Gets the count of synthetic vertices in the TIN.
    /// </summary>
    /// <returns>The number of synthetic vertices</returns>
    public int GetSyntheticVertexCount()
    {
        return GetVertices().Count((IVertex v) => v.IsSynthetic());
    }

    /// <summary>
    ///     Gets the thresholds instance used by this TIN.
    /// </summary>
    /// <returns>A valid Thresholds instance</returns>
    public Thresholds GetThresholds()
    {
        return _thresholds;
    }

    /// <summary>
    ///     Gets an enumerable collection of triangles for iteration.
    /// </summary>
    /// <returns>A valid enumerable collection of SimpleTriangle instances</returns>
    public IEnumerable<SimpleTriangle> GetTriangles()
    {
        return new SimpleTriangleIterator(this);
    }

    /// <summary>
    ///     Gets a list of vertices currently stored in the TIN.
    /// </summary>
    /// <returns>A valid list of vertices</returns>
    public IList<IVertex> GetVertices()
    {
        if (!IsBootstrapped()) return _vertexList?.ToList() ?? new List<IVertex>();

        var vertexSet = new HashSet<IVertex>();
        foreach (var edge in _edgePool)
        {
            // Add the A vertex if not null
            var a = edge.GetA();
            if (!a.IsNullVertex()) vertexSet.Add(a);

            // Also add the B vertex if not null
            var b = edge.GetB();
            if (!b.IsNullVertex()) vertexSet.Add(b);
        }

        return vertexSet.ToList();
    }

    /// <summary>
    ///     Indicates whether the TIN is successfully bootstrapped.
    /// </summary>
    /// <returns>True if bootstrapped; otherwise, false</returns>
    public bool IsBootstrapped()
    {
        return _searchEdge != null;
    }

    /// <summary>
    ///     Indicates whether the TIN conforms to the Delaunay criterion.
    /// </summary>
    /// <returns>True if conformant; otherwise, false</returns>
    public bool IsConformant()
    {
        return _isConformant;
    }

    /// <summary>
    ///     Indicates whether the TIN is locked. A locked TIN cannot be modified.
    /// </summary>
    /// <returns>True if the TIN is locked; otherwise, false.</returns>
    public bool IsLocked()
    {
        return _isLocked;
    }

    /// <summary>
    ///     Indicates if a point is inside the TIN.
    /// </summary>
    /// <param name="x">The x coordinate of the point.</param>
    /// <param name="y">The y coordinate of the point.</param>
    /// <returns>True if the point is inside the TIN; otherwise, false.</returns>
    public bool IsPointInsideTin(double x, double y)
    {
        if (!IsBootstrapped()) return false;

        var e = _walker.FindAnEdgeFromEnclosingTriangle(_searchEdge!, x, y);
        return !e.GetB().IsNullVertex();
    }

    /// <summary>
    ///     Sets the TIN to a locked state.
    /// </summary>
    public void Lock()
    {
        _isLocked = true;
    }

    /// <summary>
    ///     Provides a preallocation hint for bulk insertion. Heuristic: ~3 edges per vertex.
    /// </summary>
    /// <param name="vertexCount">Expected number of vertices to be added shortly.</param>
    public void PreAllocateForVertices(int vertexCount)
    {
        if (vertexCount <= 0) return;
        var edges = vertexCount > int.MaxValue / 3 ? int.MaxValue : (int)(vertexCount * 3.2);
        _edgePool.PreAllocateEdges(edges);
    }

    /// <summary>
    ///     Prints diagnostic information to the specified writer.
    /// </summary>
    /// <param name="writer">A valid text writer</param>
    public void PrintDiagnostics(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine("IncrementalTin Diagnostics");
        writer.WriteLine($"Bootstrapped: {IsBootstrapped()}");
        writer.WriteLine($"Vertices (count): {_vertexCount}");
        writer.WriteLine($"Edges: {_edgePool.GetEdgeCount()}");
        var tc = CountTriangles();
        writer.WriteLine(
            $"Triangles: valid={tc.ValidTriangles}, ghost={tc.GhostTriangles}, constrained={tc.ConstrainedTriangles}");
        var bounds = GetBounds();
        if (bounds.HasValue)
        {
            var b = bounds.Value;
            writer.WriteLine($"Bounds: L={b.Left}, T={b.Top}, W={b.Width}, H={b.Height}");
        }
        else
        {
            writer.WriteLine("Bounds: (not bootstrapped)");
        }
    }

    /// <summary>
    ///     Releases the lock on a TIN.
    /// </summary>
    public void Unlock()
    {
        _isLocked = false;
    }

    private QuadEdge? CheckTriangleVerticesForMatch(QuadEdge baseEdge, double x, double y, double distanceTolerance2)
    {
        var sEdge = baseEdge;
        if (sEdge.GetA().GetDistanceSq(x, y) < distanceTolerance2) return sEdge;

        if (sEdge.GetB().GetDistanceSq(x, y) < distanceTolerance2) return (QuadEdge)sEdge.GetDual();

        var v2 = sEdge.GetForward().GetB();
        if (!v2.IsNullVertex() && v2.GetDistanceSq(x, y) < distanceTolerance2) return (QuadEdge)sEdge.GetReverse();
        return null;
    }

    /// <summary>
    ///     Handles insertion of a vertex outside the current convex hull by extending the TIN.
    /// </summary>
    /// <param name="e">An edge from the perimeter of the TIN</param>
    /// <param name="v">The vertex to be added</param>
    /// <returns>An edge connected to the new vertex</returns>
    private IQuadEdge ExtendTin(IQuadEdge e, IVertex v)
    {
        // This method is based on the Guibas and Stolfi paper, with a modification
        // from the Sloan paper.  It is also presented in some detail in the
        // second edition of "Computational Geometry" by de Berg, et al.
        // The original edge e is on the perimeter of the TIN.  The new vertex v
        // lies to its left.  The existing TIN lies to the right of e.
        // The vertices of e are A and B.
        // The basic idea is to create a new edge connecting A to v.
        // Then to create a new edge connecting v to B.
        // Then to walk around the perimeter of the TIN connecting v to
        // the existing vertices until the Delaunay criterion is met.
        // The original edge e has vertices A and B (e = A->B).
        // The new vertex v is to the left of e.
        // The existing TIN is to the right of e.
        // The dual of e is on the ghost-triangle side of the TIN.
        // Create a new edge connecting B to v.
        var n1 = _edgePool.AllocateEdge(e.GetB(), v);

        // Create a new edge connecting v to A
        var n2 = _edgePool.AllocateEdge(v, e.GetA());

        // Link the new edges to each other and to the perimeter.
        n1.SetForward(n2);
        n2.SetForward(e.GetDual().GetForward());
        e.GetDual().SetForward(n1);

        // Now, walk around the perimeter checking the Delaunay criterion
        // and flipping edges as required.
        while (true)
        {
            var n3 = n1.GetDual().GetForward();
            var a = n3.GetA();
            var b = n3.GetB();

            // A negative half-plane value indicates that v is to the right of n3.
            if (_geoOp.HalfPlane(a.X, a.Y, b.X, b.Y, v.X, v.Y) < 0)
            {
                // The new vertex v is to the right of n3.
                // This indicates that the edge n3 is not on the convex hull
                // of the point set and must be flipped.
                _edgePool.FlipEdge(n1);
                n1 = n1.GetReverse();
            }
            else
            {
                // The Delaunay criterion is met.  The new vertex v is to the
                // left of n3.  The edge n1 is now part of the convex hull.
                // We are done.
                break;
            }
        }

        // Return an edge that is connected to the new vertex and points inward
        // to the TIN. This is important for the point-location logic that
        // uses the last inserted edge as a starting point for its search.
        return n2.GetDual();
    }

    /// <summary>
    ///     Gets the border constraint associated with an edge.
    /// </summary>
    /// <param name="edge">A valid edge</param>
    /// <returns>If a constraint is found, a valid reference; otherwise, null</returns>
    private IConstraint? GetBorderConstraint(IQuadEdge edge)
    {
        var index = edge.GetConstraintBorderIndex();
        if (index >= 0 && index < _constraintList.Count) return _constraintList[index];
        return null;
    }

    /// <summary>
    ///     Finds a canonical vertex in the TIN matching the supplied vertex within tolerance; otherwise returns the input.
    /// </summary>
    private IVertex GetMatchingVertex(IVertex v)
    {
        if (!IsBootstrapped()) return v;
        _searchEdge ??= _edgePool.GetStartingEdge();
        var found = _walker.FindAnEdgeFromEnclosingTriangle(_searchEdge!, v.X, v.Y);
        var match = CheckTriangleVerticesForMatch(
            (QuadEdge)found,
            v.X,
            v.Y,
            _thresholds.GetVertexTolerance2());
        return match?.GetA() ?? v;
    }

    /// <summary>
    ///     Gets the region constraint associated with an edge.
    /// </summary>
    /// <param name="edge">A valid edge</param>
    /// <returns>If a constraint is found, a valid reference; otherwise, null</returns>
    private IConstraint? GetRegionConstraint(IQuadEdge edge)
    {
        if (edge.IsConstraintRegionBorder()) return GetBorderConstraint(edge);

        if (edge.IsConstraintRegionInterior())
        {
            var index = edge.GetConstraintRegionInteriorIndex();
            if (index >= 0 && index < _constraintList.Count) return _constraintList[index];
        }

        return null;
    }

    private double InCircleWithGhosts(IVertex a, IVertex b, IVertex v)
    {
        var h = (v.X - a.X) * (a.Y - b.Y) + (v.Y - a.Y) * (b.X - a.X);
        var hp = _thresholds.GetHalfPlaneThreshold();
        if (-hp < h && h < hp)
        {
            h = _geoOp.HalfPlane(a.X, a.Y, b.X, b.Y, v.X, v.Y);
            if (h == 0)
            {
                var ax = v.X - a.X;
                var ay = v.Y - a.Y;
                var nx = b.X - a.X;
                var ny = b.Y - a.Y;
                var can = ax * nx + ay * ny;
                if (can < 0) h = -1;
                else if (ax * ax + ay * ay > nx * nx + ny * ny) h = -1;
                else h = 1;
            }
        }

        return h;
    }

    /// <summary>
    ///     Inserts a vertex into the TIN by adding edges from it to vertices of the
    ///     enclosing triangle.
    /// </summary>
    /// <param name="v">The vertex to insert</param>
    /// <returns>An edge connected to the inserted vertex</returns>
    private IQuadEdge InsertVertex(IVertex v)
    {
        // First update bounds - matches Java implementation
        var x = v.X;
        var y = v.Y;
        UpdateBounds(v);

        // If there's no search edge available, get one from the edge pool
        if (_searchEdge == null)
        {
            _searchEdge = _edgePool.GetStartingEdge();
            if (_searchEdge == null) throw new InvalidOperationException("No starting edge available in TIN");
        }

        // Find the triangle that contains the new vertex
        var searchEdge = _walker.FindAnEdgeFromEnclosingTriangle(_searchEdge, x, y);

        // Check if vertex already exists at this location (within tolerance)
        var matchEdge = CheckTriangleVerticesForMatch(
            (QuadEdge)searchEdge,
            x,
            y,
            _thresholds.GetVertexTolerance2());
        if (matchEdge != null)

            // Vertex already exists in TIN, return the edge pointing to it
            return matchEdge;

        var anchor = searchEdge.GetA();

        // If we're inserting outside the current convex hull
        if (searchEdge.GetB().IsNullVertex())

            // Extend the TIN to include this new vertex
            return ExtendTin(searchEdge, v);

        // Insert within existing TIN - Implementation of Lawson's algorithm
        var replacements = 0;
        QuadEdge? buffer = null;
        var c = (QuadEdge)searchEdge;

        // Create first edge from new vertex to an anchor
        var pStart = (QuadEdge)_edgePool.AllocateEdge(v, anchor);
        var p = pStart;

        // Connect first edge into existing triangle
        p.SetForward(searchEdge);
        var n1 = (QuadEdge)searchEdge.GetForward();
        var n2 = (QuadEdge)n1.GetForward();
        n2.SetForward(p.GetDual());

        // Keep going until we're done - this is the core of the insertion algorithm
        while (true)
        {
            var n0 = (QuadEdge)c.GetDual();
            n1 = (QuadEdge)n0.GetForward();

            // Check Delaunay criterion
            double h;
            var vA = n0.GetA();
            var vB = n1.GetA();
            var vC = n1.GetB();

            // Special handling for ghost triangles
            if (vC.IsNullVertex()) h = InCircleWithGhosts(vA, vB, v);
            else if (vA.IsNullVertex()) h = InCircleWithGhosts(vB, vC, v);
            else if (vB.IsNullVertex()) h = InCircleWithGhosts(vC, vA, v);
            else

                // Standard in-circle test for non-ghost triangles
                h = _geoOp.InCircle(vA, vB, vC, v);

            // If h >= 0, the Delaunay criterion is not met, so flip the edge
            if (h >= 0)
            {
                // Edge flip procedure
                n2 = (QuadEdge)n1.GetForward();
                n2.SetForward(c.GetForward());
                p.SetForward(n1);

                if (buffer == null)
                {
                    // We need to get the base reference to ensure ghost edges 
                    // start with a non-null vertex and end with null
                    c = (QuadEdge)c.GetBaseReference();
                    c.Clear();
                    buffer = c;
                }
                else
                {
                    _edgePool.DeallocateEdge(c);
                }

                c = n1;
                replacements++;
            }
            else
            {
                // Check for completion of circuit
                if (c.GetB() == anchor)
                {
                    pStart.GetDual().SetForward(p);

                    // Return unused buffer edge to the pool if it exists
                    if (buffer != null) _edgePool.DeallocateEdge(buffer);

                    return pStart;
                }

                // Continue with next edge
                n1 = (QuadEdge)c.GetForward();
                QuadEdge e;

                if (buffer == null)
                {
                    e = (QuadEdge)_edgePool.AllocateEdge(v, c.GetB());
                }
                else
                {
                    buffer.SetVertices(v, c.GetB());
                    e = buffer;
                    buffer = null;
                }

                e.SetForward(n1);
                e.GetDual().SetForward(p);
                c.SetForward(e.GetDual());
                p = e;
                c = n1;
            }
        }
    }

    /// <summary>
    ///     Checks if a triangle is constrained.
    /// </summary>
    /// <param name="triangle">The triangle to check</param>
    /// <returns>True if constrained; otherwise, false</returns>
    private bool IsTriangleConstrained(SimpleTriangle triangle)
    {
        return triangle.GetEdgeA().IsConstrained() || triangle.GetEdgeB().IsConstrained()
                                                   || triangle.GetEdgeC().IsConstrained();
    }

    /// <summary>
    ///     Marks existing edges as constrained if they match the constraint vertices.
    /// </summary>
    private void MarkExistingEdgeAsConstrained(IVertex v0, IVertex v1, IConstraint constraint)
    {
        foreach (var edge in _edgePool.GetEdges())
            if ((edge.GetA().Equals(v0) && edge.GetB().Equals(v1))
                || (edge.GetA().Equals(v1) && edge.GetB().Equals(v0)))
            {
                // Mark edge as constrained
                if (constraint.DefinesConstrainedRegion())
                {
                    edge.SetConstraintBorderIndex(constraint.GetConstraintIndex());
                }
                else
                {
                    edge.SetConstraintLineIndex(constraint.GetConstraintIndex());
                    _edgePool.AddLinearConstraintToMap(edge, constraint);
                }

                break;
            }
    }

    /// <summary>
    ///     Simplified constraint processing that marks existing edges as constrained.
    ///     This provides basic constraint functionality without complex intersection handling.
    /// </summary>
    /// <param name="constraint">The constraint to process</param>
    private void ProcessConstraintSimple(IConstraint constraint)
    {
        var vertices = constraint.GetVertices().ToList();

        // For polygon constraints, close the loop
        if (constraint is PolygonConstraint) vertices.Add(vertices[0]);

        // Process each segment
        for (var i = 0; i < vertices.Count - 1; i++)
        {
            var v0 = vertices[i];
            var v1 = vertices[i + 1];

            // Find and mark any existing edge between v0 and v1
            MarkExistingEdgeAsConstrained(v0, v1, constraint);
        }
    }

    /// <summary>
    ///     Ensures Delaunay conformity for an edge, recursively handling constraint edges
    ///     by edge splitting or flipping unconstrained edges.
    /// </summary>
    /// <param name="ab">The edge to check for Delaunay conformity</param>
    /// <param name="depthOfRecursion">Current recursion depth for tracking</param>
    private void RestoreConformity(QuadEdge ab, int depthOfRecursion = 1)
    {
        // Check if constraint flood fill has been performed
        var constraintSweepRequired = true; // TODO: implement _maxLengthOfQueueInFloodFill > 0

        // Get all the relevant edges and vertices around the quadrilateral
        var ba = (QuadEdge)ab.GetDual();
        var bc = (QuadEdge)ab.GetForward();
        var ad = (QuadEdge)ba.GetForward();
        var a = ab.GetA();
        var b = ab.GetB();
        var c = bc.GetB();
        var d = ad.GetB();

        // Skip if any vertex is null (ghost triangle)
        if (a.IsNullVertex() || b.IsNullVertex() || c.IsNullVertex() || d.IsNullVertex()) return;

        // Check if the edge violates the Delaunay criterion
        // Use a small threshold value because numerical imprecision can lead to 
        // infinite recursion if we require exact zeros
        var h = _geoOp.InCircle(a, b, c, d);
        if (h <= _thresholds.GetDelaunayThreshold())
        {
            // Edge satisfies Delaunay criterion (within tolerance)
            if (constraintSweepRequired) SweepForConstraintAssignments(ab);
            return;
        }

        // Get remaining edges of the quadrilateral
        var ca = (QuadEdge)ab.GetReverse();
        var db = (QuadEdge)ba.GetReverse();

        if (ab.IsConstrained())
        {
            // Edge is constrained, so we can't flip it
            // Instead, subdivide the constraint edge by adding a midpoint
            var mx = (a.X + b.X) / 2.0;
            var my = (a.Y + b.Y) / 2.0;
            var mz = (a.GetZ() + b.GetZ()) / 2.0;
            var m = new Vertex(mx, my, mz, _nSyntheticVertices++);
            m = m.WithStatus(Vertex.BitSynthetic | Vertex.BitConstraint);

            // Split the edge ab by inserting midpoint m
            // ab becomes the second segment (m->b), the new edge is the first segment (a->m)
            var mb = ab;
            var bm = ba;
            var am = (QuadEdge)_edgePool.SplitEdge(ab, m);

            // Create new edges to connect the midpoint to opposite vertices
            var cm = (QuadEdge)_edgePool.AllocateEdge(c, m);
            var dm = (QuadEdge)_edgePool.AllocateEdge(d, m);
            var ma = (QuadEdge)am.GetDual();
            var mc = (QuadEdge)cm.GetDual();
            var md = (QuadEdge)dm.GetDual();

            // Wire up the triangulation with the new edges
            ma.SetForward(ad); // should already be set
            ad.SetForward(dm);
            dm.SetForward(ma);

            mb.SetForward(bc);
            bc.SetForward(cm);
            cm.SetForward(mb);

            mc.SetForward(ca);
            ca.SetForward(am); // should already be set
            am.SetForward(mc);

            md.SetForward(db);
            db.SetForward(bm);
            bm.SetForward(md);

            // Recursively check the two new constrained edges
            RestoreConformity(am, depthOfRecursion + 1);
            RestoreConformity(mb, depthOfRecursion + 1);
        }
        else
        {
            // Edge is not constrained, so we can flip it to restore Delaunay
            // This replaces edge ab (connecting a-b) with an edge connecting c-d
            ab.SetVertices(d, c);
            ab.SetReverse(ad);
            ab.SetForward(ca);
            ba.SetReverse(bc);
            ba.SetForward(db);
            ca.SetForward(ad);
            db.SetForward(bc);
        }

        // Recursively check the surrounding edges
        RestoreConformity((QuadEdge)bc.GetDual(), depthOfRecursion + 1);
        RestoreConformity((QuadEdge)ca.GetDual(), depthOfRecursion + 1);
        RestoreConformity((QuadEdge)ad.GetDual(), depthOfRecursion + 1);
        RestoreConformity((QuadEdge)db.GetDual(), depthOfRecursion + 1);

        if (constraintSweepRequired) SweepForConstraintAssignments(ab);
    }

    /// <summary>
    ///     Ensures proper constraint region interior flags are propagated
    ///     through the triangulation when edges are modified.
    /// </summary>
    /// <param name="ab">The edge from which to start the sweep</param>
    private void SweepForConstraintAssignments(QuadEdge ab)
    {
        var con = GetRegionConstraint(ab);
        var constraintIndex = con == null ? -1 : con.GetConstraintIndex();

        // Process all edges in the pinwheel around vertex a
        foreach (var e in ab.GetPinwheel())
        {
            var vertexB = e.GetB();
            if (vertexB.IsNullVertex())

                // Skip ghost edges
                continue;

            if (con == null)
            {
                con = GetRegionConstraint(e);
                constraintIndex = con == null ? -1 : con.GetConstraintIndex();
            }

            if (e.IsConstraintRegionBorder())
            {
                con = GetBorderConstraint(e);
                constraintIndex = con == null ? -1 : con.GetConstraintIndex();
            }
            else if (con != null && constraintIndex >= 0)
            {
                e.SetConstraintRegionInteriorIndex(constraintIndex);
            }

            if (constraintIndex >= 0)
            {
                var f = e.GetForward();
                if (!f.IsConstraintRegionBorder()) f.SetConstraintRegionInteriorIndex(constraintIndex);
            }
        }
    }

    /// <summary>
    ///     Attempts to bootstrap the TIN with the current vertices.
    /// </summary>
    /// <returns>True if bootstrap was successful</returns>
    private bool TryBootstrap()
    {
        if (_vertexList == null || _vertexList.Count < 3) return false;

        // Get a suitable initial triangle
        var initialVertices = _bootstrapUtility.Bootstrap(_vertexList);
        if (initialVertices == null) return false;

        var v0 = initialVertices[0];
        var v1 = initialVertices[1];
        var v2 = initialVertices[2];

        // Create the initial triangle
        var e1 = (QuadEdge)_edgePool.AllocateEdge(v0, v1);
        var e2 = (QuadEdge)_edgePool.AllocateEdge(v1, v2);
        var e3 = (QuadEdge)_edgePool.AllocateEdge(v2, v0);

        // Create ghost edges
        var e4 = (QuadEdge)_edgePool.AllocateEdge(v0, Vertex.Null);
        var e5 = (QuadEdge)_edgePool.AllocateEdge(v1, Vertex.Null);
        var e6 = (QuadEdge)_edgePool.AllocateEdge(v2, Vertex.Null);

        // Get dual references for convenience
        var ie1 = (QuadEdge)e1.GetDual();
        var ie2 = (QuadEdge)e2.GetDual();
        var ie3 = (QuadEdge)e3.GetDual();
        var ie4 = (QuadEdge)e4.GetDual();
        var ie5 = (QuadEdge)e5.GetDual();
        var ie6 = (QuadEdge)e6.GetDual();

        // Wire up the internal triangle
        e1.SetForward(e2);
        e2.SetForward(e3);
        e3.SetForward(e1);

        // Wire up the ghost triangle connections following Java implementation
        e4.SetForward(ie5);
        e5.SetForward(ie6);
        e6.SetForward(ie4);

        ie1.SetForward(e4);
        ie2.SetForward(e5);
        ie3.SetForward(e6);

        ie4.SetForward(ie3);
        ie5.SetForward(ie1);
        ie6.SetForward(ie2);

        _searchEdge = _edgePool.GetStartingEdge();

        // Set the initial bounds
        _boundsMinX = Math.Min(v0.X, Math.Min(v1.X, v2.X));
        _boundsMaxX = Math.Max(v0.X, Math.Max(v1.X, v2.X));
        _boundsMinY = Math.Min(v0.Y, Math.Min(v1.Y, v2.Y));
        _boundsMaxY = Math.Max(v0.Y, Math.Max(v1.Y, v2.Y));

        // Process all vertices in the list if there are more than the initial triangle
        // This matches the Java implementation which processes all vertices including
        // the ones used in the initial triangle (the InsertVertex method handles duplicates)
        if (_vertexList.Count > 3)
        {
            // avoid copying: reuse reference, then clear field
            var staged = _vertexList;
            _vertexList = null;

            // Process all vertices - InsertVertex will handle duplicates
            foreach (var v in staged)
            {
                var edge = InsertVertex(v);
                _searchEdge = edge;
            }
        }
        else
        {
            // If only using the initial triangle, still need to clear the vertex list
            _vertexList = null;
        }

        return true;
    }

    /// <summary>
    ///     Updates the bounds to include the specified vertex.
    /// </summary>
    /// <param name="vertex">The vertex to include</param>
    private void UpdateBounds(IVertex vertex)
    {
        if (vertex.X < _boundsMinX) _boundsMinX = vertex.X;
        if (vertex.X > _boundsMaxX) _boundsMaxX = vertex.X;
        if (vertex.Y < _boundsMinY) _boundsMinY = vertex.Y;
        if (vertex.Y > _boundsMaxY) _boundsMaxY = vertex.Y;
    }
}