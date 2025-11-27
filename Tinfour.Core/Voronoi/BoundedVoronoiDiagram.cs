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
 * 07/2018  G. Lucas     Created
 * 08/2018  G. Lucas     Added vertex based constructor and build options
 * 09/2018  G. Lucas     Fixed bugs with infinite rays, refined concept of operations
 * 08/2025 M.Fender     Ported to C# for Tinfour.Core
 *
 * Notes:
 *
 *   The clipping algorithm used here applied elements of both the classic
 * Cohen-Sutherland line clipping algorithm (from the 1960s!) and the
 * Liang Barsky method.  In the general case, Liang Barsky is more efficient,
 * but in cases where the edges to be clipped would include a large percent
 * of edges in the interior of the clip region, the "trivially accept" logic
 * in Cohen-Sutherland provides a useful shortcut.  And that is just the
 * case for an input data set with a large number of sample points
 * (except for features developed at the perimeter edges, all edges will
 * be interior).
 *   The clipping performed here is also complicated by the fact that
 * a true Voronoi Diagram covers the entire plane (is unbounded) and
 * so some of the boundary divisions are infinite rays rather than
 * finite segments.
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Voronoi;

using System.Drawing;

using Tinfour.Core.Common;
using Tinfour.Core.Edge;
using Tinfour.Core.Standard;

/// <summary>
///     Constructs a Voronoi Diagram structure from a set of sample points.
///     The resulting structure is "bounded" in the sense that it
///     covers only a finite domain on the coordinate plane (unlike a true Voronoi
///     Diagram, which covers an infinite domain).
/// </summary>
/// <remarks>
///     <strong>
///         This class is under development and is subject to minor
///         changes in its API and behavior.
///     </strong>
/// </remarks>
public class BoundedVoronoiDiagram
{
    private readonly List<IVertex> _circleList = new();

    private readonly EdgePool _edgePool;

    private readonly GeometricOperations _geoOp;

    private readonly List<ThiessenPolygon> _polygons = new();

    /// <summary>
    ///     The overall bounds of the sample points
    /// </summary>
    private readonly RectangleF _sampleBounds;

    /// <summary>
    ///     The overall domain of the structure
    /// </summary>
    private RectangleF _bounds;

    private double _maxRadius = -1;

    private double _xmax;

    private double _xmin;

    private double _ymax;

    private double _ymin;

    /// <summary>
    ///     Construct a Voronoi Diagram structure based on the input vertex set.
    /// </summary>
    /// <param name="vertexList">A valid list of vertices</param>
    /// <param name="options">Optional specification for setting build parameters or null to use defaults.</param>
    /// <exception cref="ArgumentException">Thrown if vertex list is null, too small, or has insufficient geometry</exception>
    public BoundedVoronoiDiagram(List<IVertex> vertexList, BoundedVoronoiBuildOptions? options = null)
    {
        if (vertexList == null) throw new ArgumentException("Null input not allowed for constructor");

        var nVertices = vertexList.Count;
        if (nVertices < 3) throw new ArgumentException("Insufficient input size, at least 3 vertices are required");

        var firstVertex = vertexList[0];
        _sampleBounds = new RectangleF((float)firstVertex.X, (float)firstVertex.Y, 0, 0);

        foreach (var v in vertexList)
        {
            var point = new PointF((float)v.X, (float)v.Y);
            _sampleBounds = RectangleF.Union(_sampleBounds, new RectangleF(point.X, point.Y, 0, 0));
        }

        // Estimate a nominal point spacing based on the domain of the
        // input data set and assuming a roughly uniform density.
        // The estimated value is based on the parameters of a regular
        // hexagonal tessellation of a plane
        double area = _sampleBounds.Width * _sampleBounds.Height;
        var nominalPointSpacing = Math.Sqrt(area * 2.0 / (nVertices * Math.Sqrt(3.0)));

        var tin = new IncrementalTin(nominalPointSpacing);
        tin.Add(vertexList);

        if (!tin.IsBootstrapped())
            throw new ArgumentException("Input vertex geometry is insufficient to establish a Voronoi Diagram");

        _bounds = new RectangleF(
            _sampleBounds.X,
            _sampleBounds.Y,
            _sampleBounds.Width,
            _sampleBounds.Height);

        var thresholds = tin.GetThresholds();
        _geoOp = new GeometricOperations(thresholds);

        _edgePool = new EdgePool();

        var pOptions = options ?? new BoundedVoronoiBuildOptions();

        BuildStructure(tin, pOptions);

        // Note: Automatic color assignment using Kempe's 6-color algorithm
        // is not yet implemented. When enabled, this would assign colors to vertices
        // such that no two adjacent vertices share the same color.
        if (pOptions.IsAutomaticColorAssignmentEnabled())
        {
            // Future: Implement VertexColorizerKempe6 for automatic color assignment
            throw new NotImplementedException("Automatic color assignment (Kempe6) is not yet implemented");
        }

        tin.Dispose();
    }

    /// <summary>
    ///     Constructs an instance of a Voronoi Diagram that corresponds to the input
    ///     Delaunay Triangulation.
    /// </summary>
    /// <param name="delaunayTriangulation">A valid instance of a Delaunay Triangulation implementation.</param>
    /// <exception cref="ArgumentException">Thrown if TIN is null or not bootstrapped</exception>
    public BoundedVoronoiDiagram(IIncrementalTin delaunayTriangulation)
    {
        if (delaunayTriangulation == null) throw new ArgumentException("Null input is not allowed for TIN");

        var tinBounds = delaunayTriangulation.GetBounds();
        if (!delaunayTriangulation.IsBootstrapped() || !tinBounds.HasValue)
            throw new ArgumentException("Input TIN is not bootstrapped (populated)");

        var bounds = tinBounds.Value;
        _sampleBounds = new RectangleF(
            (float)bounds.Left,
            (float)bounds.Top,
            (float)bounds.Width,
            (float)bounds.Height);

        _bounds = new RectangleF(
            _sampleBounds.X,
            _sampleBounds.Y,
            _sampleBounds.Width,
            _sampleBounds.Height);

        _edgePool = new EdgePool();
        var thresholds = delaunayTriangulation.GetThresholds();
        _geoOp = new GeometricOperations(thresholds);
        var pOptions = new BoundedVoronoiBuildOptions();
        BuildStructure(delaunayTriangulation, pOptions);
    }

    /// <summary>
    ///     Private constructor to deter applications from invoking the default constructor
    /// </summary>
    private BoundedVoronoiDiagram()
    {
        _sampleBounds = RectangleF.Empty;
        _bounds = RectangleF.Empty;
        _edgePool = null!;
        _geoOp = null!;
    }

    /// <summary>
    ///     Gets the bounds of the bounded Voronoi Diagram. If the associated Delaunay
    ///     Triangulation included "skinny" triangles along its perimeter, the Voronoi
    ///     Diagram's bounds may be substantially larger than those of the original
    ///     input data set
    /// </summary>
    /// <returns>A valid rectangle</returns>
    public RectangleF GetBounds()
    {
        return _bounds;
    }

    /// <summary>
    ///     Gets the polygon that contains the specified coordinate point (x,y).
    /// </summary>
    /// <remarks>
    ///     <strong>Note: </strong>Although a true Voronoi Diagram covers the entire
    ///     plane, the Bounded Voronoi class has a finite domain. If the specified
    ///     coordinates are outside the bounds of this instance, no polygon will be
    ///     found and a null result will be returned.
    /// </remarks>
    /// <param name="x">A valid floating point value</param>
    /// <param name="y">A valid floating point value</param>
    /// <returns>The containing polygon or null if none is found.</returns>
    public ThiessenPolygon? GetContainingPolygon(double x, double y)
    {
        // The containing polygon is simply the one with the vertex
        // closest to the specified coordinates (x,y).
        ThiessenPolygon? minP = null;
        if (_bounds.Contains((float)x, (float)y))
        {
            var minD = double.PositiveInfinity;

            foreach (var p in _polygons)
            {
                var v = p.GetVertex();
                var d = v.GetDistanceSq(x, y);
                if (d < minD)
                {
                    minD = d;
                    minP = p;
                }
            }
        }

        return minP;
    }

    /// <summary>
    ///     Gets a list of the edges in the Voronoi Diagram. Applications are
    ///     <strong>strongly cautioned against modifying these edges.</strong>
    /// </summary>
    /// <returns>A valid list of edges</returns>
    public List<IQuadEdge> GetEdges()
    {
        return _edgePool.GetEdges().ToList();
    }

    /// <summary>
    ///     Gets the maximum index of the currently allocated edges. This method can be
    ///     used in support of applications that require the need to survey the edge
    ///     set and maintain a parallel array or collection instance that tracks
    ///     information about the edges. In such cases, the maximum edge index provides
    ///     a way of knowing how large to size the array or collection.
    /// </summary>
    /// <remarks>
    ///     Internally, Tinfour uses edge index values to manage edges in memory. The
    ///     while there can be small gaps in the indexing sequence, this method
    ///     provides a way of obtaining the absolute maximum value of currently
    ///     allocated edges.
    /// </remarks>
    /// <returns>A positive value or zero if the TIN is not bootstrapped.</returns>
    public int GetMaximumEdgeAllocationIndex()
    {
        return _edgePool.GetMaximumAllocationIndex();
    }

    /// <summary>
    ///     Gets a list of the polygons that comprise the Voronoi Diagram
    /// </summary>
    /// <returns>A valid list of polygons</returns>
    public List<ThiessenPolygon> GetPolygons()
    {
        return new List<ThiessenPolygon>(_polygons);
    }

    /// <summary>
    ///     Gets the bounds of the sample data set. These will usually be smaller than
    ///     the bounds of the overall structure.
    /// </summary>
    /// <returns>A valid rectangle</returns>
    public RectangleF GetSampleBounds()
    {
        return _sampleBounds;
    }

    /// <summary>
    ///     Gets a list of the vertices that define the Voronoi Diagram. This list is
    ///     based on the input set, though in some cases coincident or nearly
    ///     coincident vertices will be combined into a single vertex of type
    ///     VertexMergerGroup.
    /// </summary>
    /// <returns>A valid list</returns>
    public List<IVertex> GetVertices()
    {
        var vList = new List<IVertex>(_polygons.Count);
        foreach (var p in _polygons) vList.Add(p.GetVertex());
        return vList;
    }

    /// <summary>
    ///     Gets the vertices that were created to produce the Voronoi Diagram. The
    ///     output includes all circumcircle vertices that were computed when the
    ///     structure was created. It does not include the original vertices
    ///     from the input source
    /// </summary>
    /// <returns>A valid list of vertices</returns>
    public List<IVertex> GetVoronoiVertices()
    {
        return new List<IVertex>(_circleList);
    }

    /// <summary>
    ///     Prints diagnostic statistics for the Voronoi Diagram object.
    /// </summary>
    /// <param name="writer">A valid TextWriter instance.</param>
    public void PrintDiagnostics(TextWriter writer)
    {
        var nClosed = 0;
        double sumArea = 0;
        foreach (var p in _polygons)
        {
            var a = p.GetArea();
            if (!double.IsInfinity(a))
            {
                sumArea += a;
                nClosed++;
            }
        }

        var nOpen = _polygons.Count - nClosed;
        var avgArea = sumArea / (nClosed > 0 ? nClosed : 1);

        writer.WriteLine("Bounded Voronoi Diagram");
        writer.WriteLine($"   Polygons:   {_polygons.Count, 8}");
        writer.WriteLine($"     Open:     {nOpen, 8}");
        writer.WriteLine($"     Closed:   {nClosed, 8}");
        writer.WriteLine($"     Avg Area: {avgArea, 13:F4}");
        writer.WriteLine($"   Vertices:   {_circleList.Count, 8}");
        writer.WriteLine($"   Edges:      {_edgePool.Size(), 8}");
        writer.WriteLine("   Voronoi Bounds");
        writer.WriteLine($"      x min:  {_bounds.Left, 16:F4}");
        writer.WriteLine($"      y min:  {_bounds.Top, 16:F4}");
        writer.WriteLine($"      x max:  {_bounds.Right, 16:F4}");
        writer.WriteLine($"      y max:  {_bounds.Bottom, 16:F4}");
        writer.WriteLine($"   Max Circumcircle Radius:  {_maxRadius, 6:F4}");
        writer.WriteLine("   Data Sample Bounds");
        writer.WriteLine($"      x min:  {_sampleBounds.Left, 16:F4}");
        writer.WriteLine($"      y min:  {_sampleBounds.Top, 16:F4}");
        writer.WriteLine($"      x max:  {_sampleBounds.Right, 16:F4}");
        writer.WriteLine($"      y max:  {_sampleBounds.Bottom, 16:F4}");
    }

    private void BuildCenter(Circumcircle cCircle, IQuadEdge e, IVertex[] centers)
    {
        var index = e.GetIndex();
        if (centers[index].IsNullVertex())
        {
            var a = e.GetA();
            var b = e.GetB();
            var f = e.GetForward();
            var r = e.GetReverse();
            var c = e.GetForward().GetB();
            if (!c.IsNullVertex())
            {
                if (!_geoOp.Circumcircle(a, b, c, cCircle))
                    throw new InvalidOperationException("Internal error, triangle does not yield circumcircle");
                var x = cCircle.GetX();
                var y = cCircle.GetY();

                // There is a low, but non-zero, probability that the center
                // will lie on one of the perimeter edges.
                var z = ComputePerimeterParameter(x, y);
                IVertex v;
                if (double.IsNaN(z)) v = new Vertex(x, y, z, MIndex(e, f, r));
                else v = new PerimeterVertex(x, y, z, MIndex(e, f, r));

                centers[e.GetIndex()] = v;
                centers[f.GetIndex()] = v;
                centers[r.GetIndex()] = v;
                _circleList.Add(v);
                var radius = cCircle.GetRadius();
                if (radius > _maxRadius) _maxRadius = radius;

                _bounds = RectangleF.Union(_bounds, new RectangleF((float)x, (float)y, 0, 0));
            }
        }
    }

    private void BuildPart(IQuadEdge e, IVertex[] center, IQuadEdge?[] part)
    {
        var d = e.GetDual();
        var eIndex = e.GetIndex();
        var dIndex = d.GetIndex();
        var v0 = center[dIndex];
        var v1 = center[eIndex];
        if (v0.IsNullVertex() || v1.IsNullVertex())

            // This is a ghost triangle. Just ignore it
            return;

        // The Cohen-Sutherland line-clipping algorithm allows us to
        // trivially reject an edge that is completely outside the bounds
        // and one that is completely inside the bounds.
        var outcode0 = 0;
        var outcode1 = 0;

        // Get the auxiliary index (outcode) if possible
        if (v0 is Vertex vertex0)
            outcode0 = vertex0.GetAuxiliaryIndex();

        if (v1 is Vertex vertex1)
            outcode1 = vertex1.GetAuxiliaryIndex();

        if ((outcode0 & outcode1) != 0) return;

        if ((outcode0 | outcode1) == 0)
        {
            // Both vertices are entirely within the bounded area.
            // The edge can be accepted trivially
            var newEdge = _edgePool.AllocateEdge(v0, v1);
            part[eIndex] = newEdge;
            part[dIndex] = newEdge.GetDual();
            return;
        }

        // The edge intersects at least one and potentially two boundaries.
        var newEdge2 = LiangBarsky(v0, v1);
        if (newEdge2 != null)
        {
            part[eIndex] = newEdge2;
            part[dIndex] = newEdge2.GetDual();
        }
    }

    private void BuildPerimeterRay(IQuadEdge e, IVertex[] center, IQuadEdge?[] part)
    {
        var index = e.GetIndex();
        var vCenter = center[index]; // vertex at the circumcenter
        if (vCenter.IsNullVertex()) return;

        var a = e.GetA();
        var b = e.GetB();
        double x0 = _bounds.Left;
        double x1 = _bounds.Right;
        double y0 = _bounds.Top;
        double y1 = _bounds.Bottom;

        var nBuild = 0;
        var tBuild = new double[5];
        var vBuild = new IVertex[5];

        // Get auxiliary index (outcode) if possible
        var outcode = 0;
        if (vCenter is Vertex vertex)
            outcode = vertex.GetAuxiliaryIndex();

        if (outcode == 0)
        {
            vBuild[0] = vCenter;
            tBuild[0] = 0;
            nBuild = 1;
        }

        // Construct an edge from the outside to the inside.
        var eX = b.GetX() - a.GetX();
        var eY = b.GetY() - a.GetY();
        var u = Math.Sqrt(eX * eX + eY * eY);
        var uX = eY / u;
        var uY = -eX / u;
        var cX = vCenter.GetX();
        var cY = vCenter.GetY();
        double x;
        double y;
        double z;

        // In the following, we screen out the uX==0 and uY==0 cases
        // because (uX, uY) is a unit vector, t corresponds to a distance
        if (uX != 0)
        {
            var t = (x0 - cX) / uX;
            y = t * uY + cY;
            if (t >= 0 && y0 <= y && y <= y1)
            {
                z = 4 - (y - y0) / (y1 - y0); // the left side, descending, z in [3,4]
                var tempVertex = new PerimeterVertex(x0, y, z, -vCenter.GetIndex());
                var v = tempVertex.WithSynthetic(true);
                nBuild = InsertRayVertex(nBuild, vBuild, tBuild, t, v);
            }

            t = (x1 - cX) / uX;
            y = t * uY + cY;
            if (t >= 0 && y0 <= y && y <= y1)
            {
                z = 1 + (y - y0) / (y1 - y0); // right side, ascending, z in [1,2]
                var tempVertex = new PerimeterVertex(x1, y, z, -vCenter.GetIndex());
                var v = tempVertex.WithSynthetic(true);
                nBuild = InsertRayVertex(nBuild, vBuild, tBuild, t, v);
            }
        }

        if (uY != 0)
        {
            var t = (y0 - cY) / uY;
            x = t * uX + cX;
            if (t >= 0 && x0 <= x && x <= x1)
            {
                z = (x - x0) / (x1 - x0); // bottom side, ascending, z in [0,1]
                var tempVertex = new PerimeterVertex(x, y0, z, -vCenter.GetIndex());
                var v = tempVertex.WithSynthetic(true);
                nBuild = InsertRayVertex(nBuild, vBuild, tBuild, t, v);
            }

            t = (y1 - cY) / uY;
            x = t * uX + cX;
            if (t >= 0 && x0 <= x && x <= x1)
            {
                z = 3 - (x - x0) / (x1 - x0); // top side, descending, z in [2,3]
                var tempVertex = new PerimeterVertex(x, y1, z, -vCenter.GetIndex());
                var v = tempVertex.WithSynthetic(true);
                nBuild = InsertRayVertex(nBuild, vBuild, tBuild, t, v);
            }
        }

        if (nBuild >= 2)
        {
            var finalEdge = _edgePool.AllocateEdge(vBuild[1], vBuild[0]); // from out to in
            part[index] = finalEdge;
            part[index ^ 0x01] = finalEdge.GetDual();
        }
    }

    private void BuildPolygon(IQuadEdge e, bool[] visited, IQuadEdge?[] parts, List<IQuadEdge> scratch)
    {
        scratch.Clear();
        IQuadEdge? prior = null;
        IQuadEdge? first = null;
        var ghostEdgeFound = false;
        var hub = e.GetA();

        foreach (var p in e.GetPinwheel())
        {
            var index = p.GetIndex();
            visited[index] = true;
            if (p.GetB().IsNullVertex()) ghostEdgeFound = true;
            var q = parts[p.GetIndex()];
            if (q == null)

                // We've reached a discontinuity in the construction.
                // The discontinuity could be due a clipping border or a perimeter ray.
                // We will leave the prior edge alone and complete the links the
                // next time we encounter a valid edge
                continue;
            if (first == null)
            {
                first = q;
                prior = q;
                continue; // note: "first" not yet added to scratch
            }

            LinkEdges(prior, q, scratch);
            prior = q;
        }

        if (prior == null && first == null)

            // Should never happen
            return;

        if (first != null)
            LinkEdges(prior, first, scratch); // This adds "first" to scratch list
        _polygons.Add(new ThiessenPolygon(hub, scratch, ghostEdgeFound));
    }

    private void BuildStructure(IIncrementalTin tin, BoundedVoronoiBuildOptions pOptions)
    {
        // Set bounds based on options or sample bounds
        var optionBounds = pOptions.GetBounds();
        if (optionBounds == null)
        {
            _xmin = _sampleBounds.Left;
            _xmax = _sampleBounds.Right;
            _ymin = _sampleBounds.Top;
            _ymax = _sampleBounds.Bottom;
            _bounds = new RectangleF(
                (float)_xmin,
                (float)_ymin,
                (float)(_xmax - _xmin),
                (float)(_ymax - _ymin));
        }
        else
        {
            var boundsValue = optionBounds.Value;
            if (!boundsValue.Contains(_sampleBounds))
                throw new ArgumentException("Optional bounds specification does not entirely contain the sample set");

            _xmin = boundsValue.Left;
            _xmax = boundsValue.Right;
            _ymin = boundsValue.Top;
            _ymax = boundsValue.Bottom;
            _bounds = new RectangleF(
                (float)_xmin,
                (float)_ymin,
                (float)(_xmax - _xmin),
                (float)(_ymax - _ymin));
        }

        // The visited array tracks which of the TIN edges were
        // visited for various processes. It is used more than once.
        // There should be one part for each non-ghost edge. The part
        // array is indexed using the tin edge index so that
        // correspondingPart = part[edge.getIndex()]
        var maxEdgeIndex = tin.GetMaximumEdgeAllocationIndex() + 1;
        var visited = new bool[maxEdgeIndex];
        var centers = new IVertex[maxEdgeIndex];
        for (var i = 0; i < maxEdgeIndex; i++) centers[i] = Vertex.Null;
        var parts = new IQuadEdge[maxEdgeIndex];
        var scratch = new List<IQuadEdge>();
        var perimeter = tin.GetPerimeter();
        var cCircle = new Circumcircle();

        // Build the circumcircle-center vertices
        // also collect some information about the overall
        // bounds and edge length of the input TIN.
        double sumEdgeLength = 0;
        var nEdgeLength = 0;

        foreach (var e in tin.GetEdgeIterator())
        {
            if (e.GetA().IsNullVertex() || e.GetB().IsNullVertex())
            {
                // Ghost edge, do not process
                // Mark both sides as visited to suppress future checks
                var index = e.GetIndex();
                visited[index] = true;
                visited[index ^ 0x01] = true;
                continue;
            }

            sumEdgeLength += e.GetLength();
            nEdgeLength++;
            BuildCenter(cCircle, e, centers);
            BuildCenter(cCircle, e.GetDual(), centers);
        }

        // Determine bounds 
        if (optionBounds == null)
        {
            double avgLen;
            if (nEdgeLength == 0)

                // Should never happen
                avgLen = 0;
            else avgLen = sumEdgeLength / nEdgeLength;

            _xmin = _sampleBounds.Left - avgLen / 4;
            _xmax = _sampleBounds.Right + avgLen / 4;
            _ymin = _sampleBounds.Top - avgLen / 4;
            _ymax = _sampleBounds.Bottom + avgLen / 4;
            _bounds = new RectangleF(
                (float)_xmin,
                (float)_ymin,
                (float)(_xmax - _xmin),
                (float)(_ymax - _ymin));
        }
        else
        {
            var boundsValue = optionBounds.Value;
            if (!boundsValue.Contains(_sampleBounds))
                throw new ArgumentException("Optional bounds specification does not entirely contain the sample set");

            _xmin = boundsValue.Left;
            _xmax = boundsValue.Right;
            _ymin = boundsValue.Top;
            _ymax = boundsValue.Bottom;
            _bounds = new RectangleF(
                (float)_xmin,
                (float)_ymin,
                (float)(_xmax - _xmin),
                (float)(_ymax - _ymin));
        }

        var updatedCircleList = new List<IVertex>(_circleList.Count);

        // Compute and set outcodes for all circumcircle centers
        foreach (var circumcircle in _circleList) updatedCircleList.Add(ComputeAndSetOutcode(circumcircle));

        _circleList.Clear();
        _circleList.AddRange(updatedCircleList);

        // Perimeter edges get special treatment because they give rise
        // to an infinite ray outward from circumcenter
        foreach (var p in perimeter)
        {
            visited[p.GetIndex()] = true;
            BuildPerimeterRay(p, centers, parts);
        }

        foreach (var e in tin.GetEdgeIterator())
        {
            var d = e.GetDual();
            var eIndex = e.GetIndex();
            var dIndex = d.GetIndex();
            if (visited[eIndex]) continue;
            visited[eIndex] = true;
            visited[dIndex] = true;

            BuildPart(e, centers, parts);
        }

        // Reset the visited array, set all the ghost edges
        // to visited so that they are not processed below
        Array.Fill(visited, false);
        foreach (var e in perimeter)
        {
            var f = e.GetForwardFromDual();
            var index = f.GetIndex();
            visited[index] = true;
            visited[index ^ 0x01] = true;
        }

        // The first polygons we build are those that are anchored by a perimeter
        // vertex. This is the set of all the open polygons.
        // All other polygons are closed. 
        foreach (var e in perimeter)
        {
            var index = e.GetIndex();
            if (!visited[index]) BuildPolygon(e, visited, parts, scratch);
        }

        foreach (var e in tin.GetEdgeIterator())
        {
            var index = e.GetIndex();
            var hub = e.GetA();
            if (hub.IsNullVertex())

                // A ghost edge. No polygon possible
                visited[index] = true;
            else if (!visited[index]) BuildPolygon(e, visited, parts, scratch);

            var d = e.GetDual();
            index = d.GetIndex();
            hub = d.GetA();
            if (hub.IsNullVertex())

                // A ghost edge, no polygon possible
                visited[index] = true;
            else if (!visited[index]) BuildPolygon(d, visited, parts, scratch);
        }
    }

    private IVertex ComputeAndSetOutcode(IVertex c)
    {
        var x = c.GetX();
        var y = c.GetY();
        int code;
        if (x <= _xmin) code = 0b0001;
        else if (x >= _xmax) code = 0b0010;
        else code = 0;
        if (y <= _ymin) code |= 0b0100;
        else if (y >= _ymax) code |= 0b1000;

        // Store outcode in the auxiliary index
        if (c is Vertex vertex) return vertex.WithAuxiliaryIndex(code);

        return c;
    }

    private double ComputePerimeterParameter(double x, double y)
    {
        if (y == _ymin)
        {
            // bottom border range 0 to 1
            if (_xmin <= x && x <= _xmax) return (x - _xmin) / (_xmax - _xmin);
        }
        else if (x == _xmax)
        {
            // right border, range 1 to 2
            if (_ymin <= y && y <= _ymax) return 1 + (y - _ymin) / (_ymax - _ymin);
        }
        else if (y == _ymax)
        {
            // top border, range 2 to 3
            if (_xmin <= x && x <= _xmax) return 3 - (x - _xmin) / (_xmax - _xmin);
        }
        else if (x == _xmin)
        {
            // left border, range 3 to 4
            if (_ymin <= y && y <= _ymax) return 4 - (y - _ymin) / (_ymax - _ymin);
        }

        return double.NaN;
    }

    private double ComputePerimeterParameter(int iBoarder, double x, double y)
    {
        switch (iBoarder)
        {
            case 0:
                return (x - _xmin) / (_xmax - _xmin);
            case 1:
                return 1 + (y - _ymin) / (_ymax - _ymin);
            case 2:
                return 3 - (x - _xmin) / (_xmax - _xmin);
            default:
                return 4 - (y - _ymin) / (_ymax - _ymin);
        }
    }

    private int InsertRayVertex(int nBuild, IVertex[] vBuild, double[] tBuild, double t, IVertex v)
    {
        var index = nBuild;
        for (var i = nBuild - 1; i >= 0; i--)
            if (t < tBuild[i])
            {
                tBuild[i + 1] = tBuild[i];
                vBuild[i + 1] = vBuild[i];
                index = i;
            }
            else
            {
                break;
            }

        tBuild[index] = t;
        vBuild[index] = v;
        return nBuild + 1;
    }

    private IQuadEdge? LiangBarsky(IVertex v0, IVertex v1)
    {
        var x0 = v0.GetX();
        var y0 = v0.GetY();
        var x1 = v1.GetX();
        var y1 = v1.GetY();

        double t0 = 0;
        double t1 = 1;
        var iBorder0 = -1;
        var iBorder1 = -1;
        var xDelta = x1 - x0;
        var yDelta = y1 - y0;
        double p, q, r;

        for (var iBorder = 0; iBorder < 4; iBorder++)
        {
            switch (iBorder)
            {
                case 0:
                    // bottom
                    p = -yDelta;
                    q = -(_ymin - y0);
                    break;
                case 1:
                    // right
                    p = xDelta;
                    q = _xmax - x0;
                    break;
                case 2:
                    // top
                    p = yDelta;
                    q = _ymax - y0;
                    break;
                case 3:
                default:
                    // left
                    p = -xDelta;
                    q = -(_xmin - x0);
                    break;
            }

            if (p == 0)
            {
                // if q<0, the line is entirely outside.
                // otherwise, it is ambiguous
                if (q < 0)

                    // line is entirely outside
                    return null;
            }
            else
            {
                r = q / p;
                if (p < 0)
                {
                    if (r > t1) return null;

                    if (r > t0)
                    {
                        t0 = r;
                        iBorder0 = iBorder;
                    }
                }
                else
                {
                    // p>0
                    if (r < t0) return null;

                    if (r < t1)
                    {
                        t1 = r;
                        iBorder1 = iBorder;
                    }
                }
            }
        }

        IVertex p0;
        IVertex p1;

        double x, y, z;
        if (iBorder0 == -1)
        {
            p0 = v0;
        }
        else
        {
            x = x0 + t0 * xDelta;
            y = y0 + t0 * yDelta;
            z = ComputePerimeterParameter(iBorder0, x, y);
            var tempVertex = new PerimeterVertex(x, y, z, -v0.GetIndex());
            p0 = tempVertex.WithSynthetic(true);
        }

        if (iBorder1 == -1)
        {
            p1 = v1;
        }
        else
        {
            x = x0 + t1 * xDelta;
            y = y0 + t1 * yDelta;
            z = ComputePerimeterParameter(iBorder1, x, y);
            var tempVertex = new PerimeterVertex(x, y, z, -v1.GetIndex());
            p1 = tempVertex.WithSynthetic(true);
        }

        return _edgePool.AllocateEdge(p0, p1);
    }

    private void LinkEdges(IQuadEdge? xprior, IQuadEdge q, List<IQuadEdge> scratch)
    {
        if (xprior == null) return;

        var prior = xprior;

        // Connect v0 to v1
        var v0 = prior.GetB();
        var v1 = q.GetA();

        // Get Z coordinate (perimeter parameter)
        var z0 = v0.GetZ();
        var z1 = v1.GetZ();

        if (double.IsNaN(z0))
        {
            // v0 should be same object as v1
            // a simple link is all that's required
            scratch.Add(q);

            // We need to cast to QuadEdge for SetForward/SetReverse methods
            ((QuadEdge)prior).SetForward((QuadEdge)q);
            return;
        }

        // Construct new edges to thread a line for z0 to z1
        // First, it is possible that z0 and z1 are nearly equal but not quite.
        // This could happen due to round-off in the clipping routine.
        // At this time, I have never observed this special case happening
        // but I am including code to handle it anyway
        var test = Math.Abs(z0 - z1);
        if (test < 1.0e-9 || test > 4 - 1.0e-9)
        {
            // A simple link is all that's required.
            // Note: The vertices are kept separate even when nearly coincident.
            // Merging them would require handling reverse traversal order carefully.
            scratch.Add(q);
            ((QuadEdge)prior).SetForward((QuadEdge)q);
            return;
        }

        // We need to thread v0 to v1. If both lie on the same
        // border, then this action requires the construction of a synthetic
        // edge from v0 to v1. But if the vertices lie on different borders,
        // it will be necessary to construct joining lines that bend
        // around the corners.
        // The borders are numbered from 0 to 3 in the order:
        //   0 = bottom, 1 = right, 2 = top, 3 = left
        // The z coordinates indicate which border the vertices lie on,
        // with z being given as a fractional value.
        // iLast/iFirst refer to the border indices for z0/z1 respectively.
        var iLast = (int)z0;
        var iFirst = (int)z1;
        if (iFirst < iLast)

            // It wraps around the lower-left corner
            iFirst += 4;

        // Add corners, if any
        for (var i = iLast + 1; i <= iFirst; i++)
        {
            double x;
            double y;

            // Anding with 0x03 is equivalent to modulus 4
            var iCorner = i & 0x03;
            switch (iCorner)
            {
                case 0:
                    // Lower-left corner
                    x = _xmin;
                    y = _ymin;
                    break;
                case 1:
                    x = _xmax;
                    y = _ymin;
                    break;
                case 2:
                    x = _xmax;
                    y = _ymax;
                    break;
                default:
                    // iCorner == 3
                    x = _xmin;
                    y = _ymax;
                    break;
            }

            // Adding the corner point we will set the ID to be the corner
            // index and will also set it as synthetic.
            var v = new Vertex(x, y, double.NaN, iCorner).WithSynthetic(true);
            var n = _edgePool.AllocateEdge(v0, v);

            // n.setSynthetic(true); - edges don't have synthetic flag in our C# port
            v0 = v;

            scratch.Add(n);
            ((QuadEdge)n).SetReverse((QuadEdge)prior);
            prior = n;
        }

        var finalEdge = _edgePool.AllocateEdge(v0, v1);

        // finalEdge.setSynthetic(true); - edges don't have synthetic flag in our C# port
        scratch.Add(finalEdge);
        scratch.Add(q);
        ((QuadEdge)finalEdge).SetReverse((QuadEdge)prior);
        ((QuadEdge)q).SetReverse((QuadEdge)finalEdge);
    }

    private int MIndex(IQuadEdge e, IQuadEdge f, IQuadEdge r)
    {
        var index = e.GetIndex();
        if (f.GetIndex() < index) index = f.GetIndex();
        if (r.GetIndex() < index) return r.GetIndex();

        return index;
    }
}