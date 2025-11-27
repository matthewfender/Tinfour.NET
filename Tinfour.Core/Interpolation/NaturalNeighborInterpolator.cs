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
 * Date Name Description
 * ------  --------- -------------------------------------------------
 * 03/2013 G. Lucas  Created as part of IncrementalTIN
 * 08/2013 G. Lucas  Replaced stack-based edge flipping algorithm with
 *                    Bowyer-Watson approach that doesn't disturb TIN.
 * 05/2014 G. Lucas  Broken into separate class
 * 08/2015 G. Lucas  Refactored for QuadEdge class
 * 09/2015 G. Lucas  Added BarycenterCoordinateDeviation concept as a way
 *                     to double-check correctness of implementation and
 *                     adjusted calculation to map vertex coordinates so that
 *                     the query point is at the origin.
 * 01/2016 G. Lucas  Added calculation for surface normal
 * 11/2016 G. Lucas  Added support for constrained Delaunay
 * 12/2020 G. Lucas  Correction for misbehavior near edges of problematic
 *                     meshes. Removed ineffective surface normal calculation.
 *                     Modified barycentric coordinates to conform to Sibson's
 *                     definition and properly support transition across
 *                     neighboring point sets.
 * 09/2025 M. Fender  Ported to C#
 * 11/2025 M. Fender  Added pooled Circumcircle objects and Span<T> for performance
 *
 * Notes:
 *
 *   As a reminder, this class must not perform direct comparison of
 * edges because it is used not just for the direct (QuadEdge) implementation
 * of edges, but also for the VirtualEdge implementation. Therefore,
 * comparisons must be conducted by invoking the Equals() method.
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Interpolation;

using Tinfour.Core.Common;

/// <summary>
///     Provides interpolations based on Sibson's Natural Neighbor Interpolation
///     method. See Sibson, Robin, "A Brief Description of Natural Neighbor
///     Interpolation". <i>Interpreting Multivariate Data</i>. Ed. Barnett, Vic.
///     England. John Wiley &amp; Sons (1981).
/// </summary>
public class NaturalNeighborInterpolator : IInterpolatorOverTin
{
    private readonly VertexValuatorDefault _defaultValuator = new();

    private readonly GeometricOperations _geoOp;

    private readonly double _halfPlaneThreshold;

    private readonly double _inCircleThreshold;

    private readonly IIncrementalTinNavigator _navigator;

    private readonly IIncrementalTin _tin;

    // Pooled Circumcircle objects to avoid allocations in hot paths
    private readonly Circumcircle _pooledC0 = new();
    private readonly Circumcircle _pooledC1 = new();
    private readonly Circumcircle _pooledC2 = new();
    private readonly Circumcircle _pooledC3 = new();

    // Tolerance for identical vertices.
    // The tolerance factor for treating closely spaced or identical vertices
    // as a single point.
    private readonly double _vertexTolerance2; // square of vertexTolerance

    private double _areaOfEmbeddedPolygon;

    private double _barycentricCoordinateDeviation;

    private long _nInCircle;

    private long _nInCircleExtended;

    // Diagnostic counts
    private long _sumN;

    private long _sumSides;

    // Backing field for MaxInterpolationDistance
    private double? _maxInterpolationDistance;

    // Cached squared value of MaxInterpolationDistance for performance
    private double _maxInterpolationDistance2;

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
    ///     Constructs an interpolator that operates on the specified TIN.
    ///     Because the interpolator will access the TIN on a read-only basis,
    ///     it is possible to construct multiple instances of this class and
    ///     allow them to operate in parallel threads.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <strong>Important Synchronization Issue</strong>
    ///     </para>
    ///     <para>
    ///         To improve performance, the classes in this package
    ///         frequently maintain state-data about the TIN that can be reused
    ///         for query to query. They also avoid run-time overhead by not
    ///         implementing any kind of synchronization or even the
    ///         concurrent-modification testing provided by the
    ///         .NET collection classes. If an application modifies the TIN, instances
    ///         of this class will not be aware of the change. In such cases,
    ///         interpolation methods may fail by either throwing an exception or,
    ///         worse, returning an incorrect value. The onus is on the calling
    ///         application to manage the use of this class and to ensure that
    ///         no modifications are made to the TIN between interpolation operations.
    ///         If the TIN is modified, the internal state data for this class must
    ///         be reset using a call to ResetForChangeToTin().
    ///     </para>
    /// </remarks>
    /// <param name="tin">A valid instance of an incremental TIN.</param>
    /// <param name="constrainedRegionsOnly">
    ///     If true, restrict interpolation
    ///     to regions of the TIN where the enclosing triangle of the interpolation
    ///     point is entirely within a constrained region.
    /// </param>
    public NaturalNeighborInterpolator(IIncrementalTin tin, bool constrainedRegionsOnly = false)
    {
        ArgumentNullException.ThrowIfNull(tin);

        var thresholds = tin.GetThresholds();
        _geoOp = new GeometricOperations(thresholds);

        _vertexTolerance2 = thresholds.GetVertexTolerance2();
        _inCircleThreshold = thresholds.GetInCircleThreshold();
        _halfPlaneThreshold = thresholds.GetHalfPlaneThreshold();

        _tin = tin;
        _navigator = tin.GetNavigator();
        ConstrainedRegionsOnly = constrainedRegionsOnly;
    }

    public bool ConstrainedRegionsOnly { get; }

    /// <summary>
    ///     Clears any diagnostic information accumulated during processing.
    /// </summary>
    public void ClearDiagnostics()
    {
        _geoOp.ClearDiagnostics();
        _nInCircle = 0;
        _nInCircleExtended = 0;
        _sumSides = 0;
        _sumN = 0;
    }

    /// <summary>
    ///     Gets the deviation of the computed equivalent of the input query (x,y)
    ///     coordinates based on barycentric coordinates. As a byproduct, Sibson's
    ///     method can be used to compute the coordinates of the query point
    ///     by combining the normalized interpolating weights with the coordinates
    ///     of the vertices. The normalized weights are, in fact, Barycentric
    ///     Coordinates for the query point. While the computed equivalent should
    ///     be an exact match for the query point, errors in implementation
    ///     or numeric errors due to float-point precision limitations would
    ///     result in a deviation. Thus, this method provides a diagnostic
    ///     on the most recent interpolation. A large non-zero value would indicate a
    ///     potential implementation problem. A consistently small value would be
    ///     indicative of a successful implementation. At this time, tests on
    ///     a large number of input data sets have always produced small deviation
    ///     values.
    /// </summary>
    /// <returns>
    ///     A positive value, ideally zero but usually a small number
    ///     slightly larger than that.
    /// </returns>
    public double GetBarycentricCoordinateDeviation()
    {
        return _barycentricCoordinateDeviation;
    }

    /// <summary>
    ///     Gets a list of edges for the polygonal cavity that would be created
    ///     as part of the Bowyer-Watson insertion algorithm. If the list is empty,
    ///     it indicates that TIN was not bootstrapped or the query was to the
    ///     exterior of the TIN.
    /// </summary>
    /// <remarks>
    ///     The vertices associated with the resulting edge list are the
    ///     <i>natural neighbors</i> of the point given by the input coordinates.
    /// </remarks>
    /// <param name="x">A Cartesian coordinate in the coordinate system used for the TIN</param>
    /// <param name="y">A Cartesian coordinate in the coordinate system used for the TIN</param>
    /// <returns>A valid, potentially empty, list.</returns>
    public List<IQuadEdge> GetBowyerWatsonEnvelope(double x, double y)
    {
        // In the logic below, we access the Vertex x and y coordinates directly
        // but we use the GetZ() method to get the z value. Some vertices
        // may actually be VertexMergerGroup instances
        var eList = new List<IQuadEdge>();

        var locatorEdge = _navigator.GetNeighborEdge(x, y);

        if (locatorEdge == null)

            // This would happen only if the TIN were not bootstrapped
            return eList;

        var e = locatorEdge;
        var f = e.GetForward();
        var r = e.GetReverse();

        var v0 = e.GetA();
        var v1 = e.GetB();
        var v2 = f.GetB();

        double h;

        // By the way the GetNeighborEdge() method is defined, if
        // the query is outside the TIN or on the perimeter edge,
        // the edge v0, v1 will be the perimeter edge and v2 will
        // be the ghost vertex (e.g. a null). In either case, v2 will
        // not be defined. So, if v2 is null, the NNI interpolation is not defined.
        if (v2.IsNullVertex()) return eList; // empty list, NNI undefined.

        if (v0.GetDistanceSq(x, y) < _vertexTolerance2)
        {
            eList.Add(e);
            return eList; // edge starting with v0
        }

        if (v1.GetDistanceSq(x, y) < _vertexTolerance2)
        {
            eList.Add(f);
            return eList; // edge starting with v1
        }

        if (v2.GetDistanceSq(x, y) < _vertexTolerance2)
        {
            eList.Add(r);
            return eList; // edge starting with v2
        }

        if (e.IsConstrained())
        {
            h = _geoOp.HalfPlane(v0.X, v0.Y, v1.X, v1.Y, x, y);
            if (h < _halfPlaneThreshold)

                // (x,y) is on the edge v0, v1)
                return eList; // empty list, NNI undefined.
        }

        if (f.IsConstrained())
        {
            h = _geoOp.HalfPlane(v1.X, v1.Y, v2.X, v2.Y, x, y);
            if (h < _halfPlaneThreshold) return eList; // empty list, NNI undefined.
        }

        if (r.IsConstrained())
        {
            h = _geoOp.HalfPlane(v2.X, v2.Y, v0.X, v0.Y, x, y);
            if (h < _halfPlaneThreshold) return eList; // empty list, NNI undefined.
        }

        // ------------------------------------------------------
        // The fundamental idea of natural neighbor interpolation is
        // based on measuring how the local geometry of a Voronoi
        // Diagram would change if a new vertex were inserted.
        // (recall that the Voronoi is the dual of a Delaunay Triangulation).
        // Thus the NNI interpolation has common element with an
        // insertion into a TIN. In writing the code below, I have attempted
        // to preserve similarities with the IncrementalTIN insertion logic
        // where appropriate.
        // Step 1 -----------------------------------------------------
        // Create an array of edges that would connect to the radials
        // from an inserted vertex if it were added at coordinates (x,y).
        // This array happens to describe a Thiessen Polygon around the
        // inserted vertex.
        var stack = new Stack<IQuadEdge>();
        IQuadEdge c, n0, n1;

        c = locatorEdge;
        while (true)
        {
            n0 = c.GetDual();
            n1 = n0.GetForward();

            if (c.IsConstrained())
            {
                // The search does not extend past a constrained edge.
                // Set h=-1 to suppress further testing and add the edge.
                h = -1;
            }
            else if (n1.GetB().IsNullVertex())
            {
                // The search has reached a perimeter edge
                // just add the edge and continue.
                h = -1;
            }
            else
            {
                _nInCircle++;

                // Test for the Delaunay inCircle criterion.
                // See notes about efficiency in the IncrementalTIN class.
                var a11 = n0.GetA().X - x;
                var a21 = n1.GetA().X - x;
                var a31 = n1.GetB().X - x;

                // Column 2
                var a12 = n0.GetA().Y - y;
                var a22 = n1.GetA().Y - y;
                var a32 = n1.GetB().Y - y;

                h = (a11 * a11 + a12 * a12) * (a21 * a32 - a31 * a22)
                    + (a21 * a21 + a22 * a22) * (a31 * a12 - a11 * a32)
                    + (a31 * a31 + a32 * a32) * (a11 * a22 - a21 * a12);

                if (-_inCircleThreshold < h && h < _inCircleThreshold)
                {
                    _nInCircleExtended++;
                    h = _geoOp.InCircle(
                        n0.GetA().X,
                        n0.GetA().Y,
                        n1.GetA().X,
                        n1.GetA().Y,
                        n1.GetB().X,
                        n1.GetB().Y,
                        x,
                        y);
                }
            }

            if (h >= 0)
            {
                // The vertex is within the circumcircle of the associated
                // triangle. The Thiessen triangle will extend to include
                // that triangle and, perhaps, its neighbors.
                // So continue the search.
                stack.Push(n0);
                c = n1;
            }
            else
            {
                eList.Add(c);
                c = c.GetForward();

                if (stack.Count > 0)
                {
                    var p = stack.Peek();
                    while (c.Equals(p))
                    {
                        stack.Pop();
                        c = c.GetDual().GetForward();

                        if (stack.Count == 0)
                            break;

                        p = stack.Peek();
                    }
                }

                if (c.Equals(locatorEdge)) break;
            }
        }

        return eList;
    }

    /// <summary>
    ///     Gets a string describing the interpolation method
    ///     that can be used for labeling graphs and printouts.
    /// </summary>
    /// <returns>A valid string</returns>
    public string GetMethod()
    {
        return "Natural Neighbor (Sibson's C0)";
    }

    /// <summary>
    ///     Gets an instance containing the natural neighbors and associated
    ///     Sibson coordinates for a specified query location.
    /// </summary>
    /// <param name="x">The x Cartesian coordinate for the query point</param>
    /// <param name="y">The y Cartesian coordinate for the query point</param>
    /// <returns>A valid instance or null if the query point is not inside the TIN.</returns>
    public NaturalNeighborElements? GetNaturalNeighborElements(double x, double y)
    {
        var eList = GetBowyerWatsonEnvelope(x, y);

        var nEdge = eList.Count;
        if (nEdge == 0)

            // (x,y) is outside defined area
            return null;

        if (nEdge == 1)

            // (x,y) is an exact match with the one edge in the list
            return null;

        // The eList contains a series of edges defining the cavity
        // containing the polygon.
        var w = GetSibsonCoordinates(eList, x, y);
        if (w == null)

            // The coordinate is on the perimeter, no Barycentric coordinates
            // are available.
            return null;

        var vArray = new IVertex[nEdge];
        var k = 0;
        foreach (var edge in eList) vArray[k++] = edge.GetA();

        return new NaturalNeighborElements(x, y, w, vArray, _areaOfEmbeddedPolygon);
    }

    /// <summary>
    ///     Given a reference point enclosed by a polygon defining its
    ///     natural neighbors, computes an array of Sibson's <i>local coordinates</i>
    ///     giving the computed weighting factors for the vertices that comprise
    ///     the polygon. Sibson coordinates are often represented using the
    ///     Greek letter lambda. The coordinates are normalized so that their sum
    ///     is 1.0.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method works only if the input polygon represents the set of
    ///         natural neighbors of the reference point given in counterclockwise
    ///         order. This polygon will be a simple, non-self-intersecting loop.
    ///         Other polygons will not necessarily produce correct results.
    ///     </para>
    ///     <para>
    ///         <strong>Using Sibson's coordinates as a self-test for this implementation</strong>
    ///     </para>
    ///     <para>
    ///         Sibson's coordinates are generalized Barycentric coordinates
    ///         (Bobach, 2009, pg. 7). Consequently,
    ///         the weighting factors computed using this method should be able to
    ///         compute the Cartesian coordinates of the reference point to a high
    ///         degree of accuracy (within the limits of floating-point precision).
    ///         Thus, the results of this calculation can be used as a <i>figure of merit</i>
    ///         for any interpolation operation using Sibson coordinates. By backward
    ///         computing the (x,y) coordinates of the reference point from the
    ///         (x,y) coordinates of the natural neighbors and their weights,
    ///         and then comparing the result against the original, this method
    ///         derives a value that Tinfour calls the "Barycentric deviation".
    ///         This deviation is stored internally to instances of this class and
    ///         may be fetched after a calculation by calling the
    ///         GetBarycentricDeviation() method. If the interpolation logic works well,
    ///         the deviation would be zero or close to zero, resulting a favorable
    ///         figure of merit.
    ///     </para>
    ///     <para>
    ///         If the point is not inside the polygon or if
    ///         the polygon is not a proper set of natural neighbors, the results are undefined
    ///         and the method may return a null array or a meaningless result.
    ///         If the point is on the perimeter of the polygon, this method will
    ///         return a null array.
    ///     </para>
    ///     <para>
    ///         For a rigorous discussion of the equivalence of Sibson's local
    ///         coordinates and Barycentric coordinates, see
    ///         <cite>
    ///             Bobach, T (2009). Natural Neighbor Interpolation --
    ///             Critical Assessment and New Contributions (Doctoral dissertation).
    ///         </cite>
    ///     </para>
    /// </remarks>
    /// <param name="polygon">
    ///     List of edges defining a non-self-intersecting,
    ///     potentially non-convex polygon composed of natural neighbor vertices
    /// </param>
    /// <param name="x">The x coordinate of the reference point</param>
    /// <param name="y">The y coordinate of the reference point</param>
    /// <returns>If successful, a valid array; otherwise a null.</returns>
    public double[]? GetSibsonCoordinates(List<IQuadEdge> polygon, double x, double y)
    {
        _areaOfEmbeddedPolygon = 0;
        var nEdge = polygon.Count;
        if (nEdge < 3) return [];

        // The eList contains a series of edges defining the cavity
        // containing the polygon.
        IVertex a, b, c;
        // Use pooled Circumcircle objects to avoid allocations
        var c0 = _pooledC0;
        var c1 = _pooledC1;
        var c2 = _pooledC2;
        var c3 = _pooledC3;
        IQuadEdge e0, e1, n, n1;
        double x0, y0, x1, y1, wThiessen, wXY, wDelta;
        double wSum = 0;
        var weights = new double[nEdge];

        for (var i0 = 0; i0 < nEdge; i0++)
        {
            var i1 = (i0 + 1) % nEdge;
            e0 = polygon[i0];
            e1 = polygon[i1];
            a = e0.GetA();
            b = e1.GetA(); // same as e0.GetB();
            c = e1.GetB();
            var ax = a.X - x;
            var ay = a.Y - y;
            var bx = b.X - x;
            var by = b.Y - y;
            var cx = c.X - x;
            var cy = c.Y - y;

            x0 = (ax + bx) / 2;
            y0 = (ay + by) / 2;
            x1 = (bx + cx) / 2;
            y1 = (by + cy) / 2;

            // For the first edge processed, the code needs to initialize values
            // for c0 and c3. But after that, the code can reuse values from
            // the previous calculation.
            if (i0 == 0)
            {
                _geoOp.Circumcircle(ax, ay, bx, by, 0, 0, c0);
                var nb = e0.GetForward().GetB();
                _geoOp.Circumcircle(ax, ay, bx, by, nb.X - x, nb.Y - y, c3);
            }
            else
            {
                c0.Copy(c1);
            }

            _geoOp.Circumcircle(bx, by, cx, cy, 0, 0, c1);

            // Compute the reduced "component area" of the Theissen polygon
            // constructed around point B, the second point of edge[i0].
            wXY = x0 * c0.GetY() - c0.GetX() * y0 + (c0.GetX() * c1.GetY() - c1.GetX() * c0.GetY())
                                                  + (c1.GetX() * y1 - x1 * c1.GetY());

            // Compute the full "component area" of the Theissen polygon
            // constructed around point B, the second point of edge[i0]
            n = e0.GetForward();
            wThiessen = x0 * c3.GetY() - c3.GetX() * y0;
            while (!n.Equals(e1))
            {
                n1 = n.GetDual();
                n = n1.GetForward();
                c2.Copy(c3);
                a = n1.GetA();
                b = n.GetA(); // same as n1.GetB();
                c = n.GetB();
                ax = a.X - x;
                ay = a.Y - y;
                bx = b.X - x;
                by = b.Y - y;
                cx = c.X - x;
                cy = c.Y - y;
                _geoOp.Circumcircle(ax, ay, bx, by, cx, cy, c3);
                wThiessen += c2.GetX() * c3.GetY() - c3.GetX() * c2.GetY();
            }

            wThiessen += c3.GetX() * y1 - x1 * c3.GetY();

            // Compute wDelta, the amount of area that the Theissen polygon
            // constructed around vertex B would yield to an insertion at
            // the query point.
            // For convenience, both the wXY and wThiessen weights were
            // computed in a clockwise order, which means they are the
            // negative of what we need for the weight computation, so
            // negate them and -(wTheissen-wXY) becomes wXY-wThiessen
            // Also, there would normally be a divide by 2 factor from the
            // shoelace area formula, but that is omitted because it will
            // drop out when we unitize the sum of the set of the weights.
            wDelta = wXY - wThiessen;
            wSum += wDelta;
            weights[i1] = wDelta;
        }

        // Normalize the weights
        for (var i = 0; i < weights.Length; i++) weights[i] /= wSum;

        _areaOfEmbeddedPolygon = wSum / 2;

        // Compute the barycentric coordinate deviation. This is a purely diagnostic
        // value and computing it adds some small overhead to the interpolation.
        double xSum = 0;
        double ySum = 0;
        var k = 0;
        foreach (var s in polygon)
        {
            var v = s.GetA();
            xSum += weights[k] * (v.X - x);
            ySum += weights[k] * (v.Y - y);
            k++;
        }

        _barycentricCoordinateDeviation = Math.Sqrt(xSum * xSum + ySum * ySum);

        return weights;
    }

    /// <summary>
    ///     Not implemented at this time.
    ///     Gets the unit normal to the surface at the position of the most
    ///     recent interpolation. The unit normal is computed based on the
    ///     partial derivatives of the surface polynomial evaluated at the
    ///     coordinates of the query point. Note that this method
    ///     assumes that the vertical and horizontal coordinates of the
    ///     input sample points are isotropic.
    /// </summary>
    /// <returns>
    ///     If defined, a valid array of dimension 3 giving
    ///     the x, y, and z components of the unit normal, respectively; otherwise,
    ///     a zero-sized array.
    /// </returns>
    public double[] GetSurfaceNormal()
    {
        return Array.Empty<double>();
    }

    /// <summary>
    ///     Perform interpolation using Sibson's C0 method. This interpolation
    ///     develops a continuous surface, and provides first derivative
    ///     continuity at all except the input vertex points.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The domain of the interpolator is limited to the interior
    ///         of the convex hull. Methods for extending to the edge of the
    ///         TIN or beyond are being investigated.
    ///     </para>
    ///     <para>
    ///         The interpolation is treated as undefined at points that lie
    ///         directly on a constrained edge.
    ///     </para>
    /// </remarks>
    /// <param name="x">The x coordinate for the interpolation point</param>
    /// <param name="y">The y coordinate for the interpolation point</param>
    /// <param name="valuator">
    ///     A valid valuator for interpreting the z value of each
    ///     vertex or a null value to use the default.
    /// </param>
    /// <returns>
    ///     If the interpolation is successful, a valid floating point
    ///     value; otherwise, a Double.NaN.
    /// </returns>
    public double Interpolate(double x, double y, IVertexValuator? valuator)
    {
        var vq = valuator ?? _defaultValuator;
        var locatorEdge = _navigator.GetNeighborEdge(x, y);

        // Check MaxInterpolationDistance if set
        if (_maxInterpolationDistance.HasValue)
        {
            var v0 = locatorEdge.GetA();
            var v1 = locatorEdge.GetB();
            var v2 = locatorEdge.GetForward().GetB();
            var d0 = v0.GetDistanceSq(x, y);
            var d1 = v1.GetDistanceSq(x, y);
            var d2 = v2.IsNullVertex() ? double.MaxValue : v2.GetDistanceSq(x, y);
            var minDistSq = Math.Min(d0, Math.Min(d1, d2));
            if (minDistSq > _maxInterpolationDistance2)
                return double.NaN;
        }

        if (ConstrainedRegionsOnly)

            // Only interpolate if at least one edge of the triangle is interior to a constrained region
            if (!locatorEdge.IsConstraintRegionMember() || (!locatorEdge.IsConstraintRegionInterior()
                                                            && !locatorEdge.GetForward().IsConstraintRegionInterior()
                                                            && !locatorEdge.GetReverse().IsConstraintRegionInterior()))
                return double.NaN;

        var eList = GetBowyerWatsonEnvelope(x, y);
        var nEdge = eList.Count;
        if (nEdge == 0)

            // (x,y) is outside defined area
            return double.NaN;

        if (nEdge == 1)
        {
            // (x,y) is an exact match with the one edge in the list
            var e = eList[0];
            var v = e.GetA();
            return vq.Value(v);
        }

        _sumN++;
        _sumSides += eList.Count;

        // The eList contains a series of edges defining the cavity
        // containing the polygon.
        var w = GetSibsonCoordinates(eList, x, y);
        if (w == null)

            // The coordinate is on the perimeter, no Barycentric coordinates
            // are available.
            return double.NaN;

        double zSum = 0;
        var k = 0;
        foreach (var s in eList)
        {
            var z = vq.Value(s.GetA());
            zSum += w[k++] * z;
        }

        return zSum;
    }

    /// <summary>
    ///     Indicates whether the interpolation class supports the computation
    ///     of surface normals through the GetSurfaceNormal() method.
    /// </summary>
    /// <returns>
    ///     True if the class implements the ability to compute
    ///     surface normals; otherwise, false.
    /// </returns>
    public bool IsSurfaceNormalSupported()
    {
        return false;
    }

    /// <summary>
    ///     Prints a set of diagnostic information describing the operations
    ///     used to interpolate points.
    /// </summary>
    /// <param name="ps">A valid TextWriter such as Console.Out.</param>
    public void PrintDiagnostics(TextWriter ps)
    {
        var nC = _geoOp.GetCircumcircleCount();
        var nCE = _geoOp.GetExtendedPrecisionInCircleCount();
        ps.WriteLine($"N inCircle:          {_nInCircle, 12}");
        ps.WriteLine($"N inCircle extended: {_nInCircleExtended, 12}");
        ps.WriteLine($"N circumcircle:      {nC, 12}");
        ps.WriteLine($"N circumcircle ext:  {nCE, 12}");
        var n = _sumN > 0 ? _sumN : 1;
        ps.WriteLine($"Avg circumcircles per interpolation: {(double)nC / n, 9:F6}");
        ps.WriteLine($"Avg sides per Theissen polygon:      {(double)_sumSides / n, 9:F6}");
    }

    /// <summary>
    ///     Used by an application to reset the state data within the interpolator
    ///     when the content of the TIN may have changed. Resetting the state data
    ///     unnecessarily may result in a minor performance reduction when processing
    ///     a large number of interpolations, but is otherwise harmless.
    /// </summary>
    public void ResetForChangeToTin()
    {
        _navigator.ResetForChangeToTin();
    }

    private IQuadEdge CheckTriangleVerticesForMatch(IQuadEdge baseEdge, double x, double y, double distanceTolerance2)
    {
        var sEdge = baseEdge;
        var sFwd = sEdge.GetForward();
        var dMin = sEdge.GetA().GetDistanceSq(x, y);

        var dFwd = sFwd.GetA().GetDistanceSq(x, y);
        if (dFwd < dMin)
        {
            sEdge = sFwd;
            dMin = dFwd;
        }

        var v2 = sEdge.GetForward().GetB();
        if (!v2.IsNullVertex() && v2.GetDistanceSq(x, y) < dMin) return sFwd.GetForward();

        return sEdge;
    }
}