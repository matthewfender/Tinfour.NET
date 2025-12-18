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
 * ------ --------- -------------------------------------------------
 * 03/2014 G. Lucas Created as a method of IncrementalTIN
 * 05/2014 G. Lucas Broken into separate class
 * 08/2015 G. Lucas Refactored for QuadEdge class
 * 08/2025 M. Fender Ported to C#
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Interpolation;

using Tinfour.Core.Common;

/// <summary>
///     Provides interpolation based on treating the surface as a collection
///     of planar triangular facets.
/// </summary>
public class TriangularFacetInterpolator : IInterpolatorOverTin
{
    private readonly VertexValuatorDefault _defaultValuator = new();

    private readonly IIncrementalTinNavigator _navigator;

    private readonly double _precisionThreshold;

    private readonly IIncrementalTin _tin;

    // tolerance for identical vertices.
    // the tolerance factor for treating closely spaced or identical vertices
    // as a single point.
    private readonly double _vertexTolerance2; // square of vertexTolerance;

    private double _nx;

    private double _ny;

    private double _nz;

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
    ///     Construct an interpolator that operates on the specified TIN.
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
    ///         implementing any kind of synchronization or concurrent-modification
    ///         testing provided by collection classes. If an application modifies
    ///         the TIN, instances of this class will not be aware of the change. In such cases,
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
    ///     Flag indicating whether to restrict
    ///     interpolation to constrained regions only.
    /// </param>
    public TriangularFacetInterpolator(IIncrementalTin tin, bool constrainedRegionsOnly = false)
    {
        ArgumentNullException.ThrowIfNull(tin);

        var thresholds = tin.GetThresholds();

        _vertexTolerance2 = thresholds.GetVertexTolerance2();
        _precisionThreshold = thresholds.GetPrecisionThreshold();

        _tin = tin;
        _navigator = tin.GetNavigator();
        ConstrainedRegionsOnly = constrainedRegionsOnly;
    }

    public bool ConstrainedRegionsOnly { get; }

    /// <summary>
    ///     Gets a string describing the interpolation method
    ///     that can be used for labeling graphs and printouts.
    ///     Because this string may be used as a column header in a table,
    ///     its length should be kept short.
    /// </summary>
    /// <returns>A valid string</returns>
    public string GetMethod()
    {
        return "Triangular Facet";
    }

    /// <summary>
    ///     Gets the unit normal to the surface at the position of the most
    ///     recent interpolation. The unit normal is computed based on the
    ///     partial derivatives of the surface polynomial evaluated at the
    ///     coordinates of the query point. Note that this method
    ///     assumes that the vertical and horizontal coordinates of the
    ///     input sample points are isotropic.
    /// </summary>
    /// <returns>
    ///     If defined, a valid array of dimension 3 giving
    ///     the x, y, and z components of the normal, respectively; otherwise,
    ///     a zero-sized array.
    /// </returns>
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

    /// <summary>
    ///     Perform linear interpolation treating the triangle that contains the
    ///     query point as a flat plane. This interpolation
    ///     develops a continuous surface, but does not provide first-derivative
    ///     continuity at the edges of triangles.
    /// </summary>
    /// <remarks>
    ///     This interpolation is not defined beyond the convex hull of the TIN
    ///     and this method will produce a Double.NaN if the specified coordinates
    ///     are exterior to the TIN.
    /// </remarks>
    /// <param name="x">The x coordinate for the interpolation point</param>
    /// <param name="y">The y coordinate for the interpolation point</param>
    /// <param name="valuator">
    ///     A valid valuator for interpreting the z value of each
    ///     vertex or a null value to use the default.
    /// </param>
    /// <returns>
    ///     If the interpolation is successful, a valid floating point
    ///     value; otherwise, a NaN.
    /// </returns>
    public double Interpolate(double x, double y, IVertexValuator? valuator)
    {
        var vq = valuator ?? _defaultValuator;

        // Check if TIN is bootstrapped first
        if (!_tin.IsBootstrapped()) return double.NaN;

        var edge = _navigator.GetNeighborEdge(x, y);
        if (ConstrainedRegionsOnly)
        {
            // Check if ANY edge of the triangle is marked as interior to a constraint region.
            // A triangle is considered inside a constrained region if at least one of its edges
            // is marked as interior to that region.
            //
            // Note: Border edges alone don't qualify - they touch the boundary but the triangle
            // might be on the outside. Interior edges mean the triangle is definitely inside.
            var edgeFwd = edge.GetForward();
            var edgeRev = edge.GetReverse();

            var isInConstrainedRegion = edge.IsConstraintRegionInterior()
                                     || edgeFwd.IsConstraintRegionInterior()
                                     || edgeRev.IsConstraintRegionInterior();

            if (!isInConstrainedRegion)
                return double.NaN;
        }

        var v0 = edge.GetA();
        var v1 = edge.GetB();
        var v2 = edge.GetForward().GetB();

        // Check MaxInterpolationDistance if set
        if (_maxInterpolationDistance.HasValue)
        {
            var d0 = v0.GetDistanceSq(x, y);
            var d1 = v1.GetDistanceSq(x, y);
            var d2 = v2.IsNullVertex() ? double.MaxValue : v2.GetDistanceSq(x, y);
            var minDistSq = Math.Min(d0, Math.Min(d1, d2));
            if (minDistSq > _maxInterpolationDistance2)
                return double.NaN;
        }

        var z0 = vq.Value(v0);
        var z1 = vq.Value(v1);
        var sx = x - v0.X;
        var sy = y - v0.Y;

        var ax = v1.X - v0.X;
        var ay = v1.Y - v0.Y;
        var az = z1 - z0;

        if (v2.IsNullVertex())
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

        var z2 = vq.Value(v2);

        var bx = v2.X - v0.X;
        var by = v2.Y - v0.Y;
        var bz = z2 - z0;

        _nx = ay * bz - az * by;
        _ny = az * bx - ax * bz;
        _nz = ax * by - ay * bx;

        if (v0.GetDistanceSq(x, y) < _vertexTolerance2) return z0;

        if (v1.GetDistanceSq(x, y) < _vertexTolerance2) return z1;

        if (v2.GetDistanceSq(x, y) < _vertexTolerance2) return z2;

        if (Math.Abs(_nz) < _precisionThreshold) return (z0 + z1 + z2) / 3.0;

        // solve for pz
        return z0 - (_nx * sx + _ny * sy) / _nz;
    }

    /// <summary>
    ///     Performs an interpolation with special handling to provide
    ///     values for regions to the exterior of the Delaunay Triangulation.
    ///     If the query point (x,y) lies inside the triangulation, the interpolation
    ///     will be identical to the results from the Interpolate() method.
    ///     If the query point (x,y) lies to the exterior, it will be projected
    ///     down to the nearest edge and a value will be interpolated between
    ///     the values of the edge-defining vertices (v0, v1).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         When the query point is outside the TIN, the normal vector is computed
    ///         based on the behavior of the plane generated by the z values
    ///         in the region adjacent to the perimeter edge.
    ///     </para>
    ///     <para>
    ///         This method does not perform an extrapolation. Instead, the computed
    ///         value is assigned the value of the nearest point on the convex
    ///         hull of the TIN.
    ///     </para>
    ///     <para>
    ///         Note that this method can still return a NaN value if the TIN
    ///         is not populated with at least one non-trivial triangle.
    ///     </para>
    /// </remarks>
    /// <param name="x">The planar x coordinate of the interpolation point</param>
    /// <param name="y">The planar y coordinate of the interpolation point</param>
    /// <param name="valuator">
    ///     A valid valuator for interpreting the z value of each
    ///     vertex or a null value to use the default.
    /// </param>
    /// <returns>
    ///     If successful, a valid floating point value; otherwise,
    ///     a null.
    /// </returns>
    public double InterpolateWithExteriorSupport(double x, double y, IVertexValuator? valuator)
    {
        // in the logic below, we access the Vertex x and z coordinates directly
        // but we use the GetZ() method to get the z value. Some vertices
        // may actually be VertexMergerGroup instances
        var vq = valuator ?? _defaultValuator;

        // Check if TIN is bootstrapped first
        if (!_tin.IsBootstrapped()) return double.NaN;

        var e = _navigator.GetNeighborEdge(x, y);

        if (e == null)

            // this should happen only when TIN is not bootstrapped
            return double.NaN;

        var v0 = e.GetA();
        var v1 = e.GetB();
        var v2 = e.GetForward().GetB();

        // Check MaxInterpolationDistance if set
        if (_maxInterpolationDistance.HasValue)
        {
            var d0 = v0.GetDistanceSq(x, y);
            var d1 = v1.GetDistanceSq(x, y);
            var d2 = v2.IsNullVertex() ? double.MaxValue : v2.GetDistanceSq(x, y);
            var minDistSq = Math.Min(d0, Math.Min(d1, d2));
            if (minDistSq > _maxInterpolationDistance2)
                return double.NaN;
        }

        var z0 = vq.Value(v0);
        var z1 = vq.Value(v1);
        var sx = x - v0.X;
        var sy = y - v0.Y;

        var ax = v1.X - v0.X;
        var ay = v1.Y - v0.Y;
        var az = z1 - z0;

        if (v2.IsNullVertex())
        {
            // (x,y) is either on perimeter edge or outside the TIN.
            // project it down to the perimeter edge and interpolate
            // from there.
            // There are two cases for the normal. In the gap area between
            // edges (t<0 or t>1), the surface is flat (z has a constant value)
            // and the normal is perpendicular to the plane (0, 0, 1).
            // In the region outside and perpendicular to the edge,
            // the computed value of z will vary, but will be constant
            // along a ray perpendicular to the edge.
            // So the perpendicular vector (-ay, ax, 0) lies on the
            // planar surface beyond the edge, as does the edge itself.
            // Thus, the normal can be computed using the cross product
            // n = (ax, ay, az) <cross> (-ay, ax, 0)
            var t = (sx * ax + sy * ay) / (ax * ax + ay * ay);
            double z;
            if (t <= 0)
            {
                z = v0.GetZ();
                _nx = 0;
                _ny = 0;
                _nz = 1;
            }
            else if (t >= 1)
            {
                z = v1.GetZ();
                _nx = 0;
                _ny = 0;
                _nz = 1;
            }
            else
            {
                z = t * az + z0;
                _nx = -az * ax;
                _ny = -az * ay;
                _nz = ax * ax + ay * ay;
            }

            return z;
        }

        var z2 = vq.Value(v2);

        var bx = v2.X - v0.X;
        var by = v2.Y - v0.Y;
        var bz = z2 - z0;

        _nx = ay * bz - az * by;
        _ny = az * bx - ax * bz;
        _nz = ax * by - ay * bx;

        if (v0.GetDistanceSq(x, y) < _vertexTolerance2) return z0;

        if (v1.GetDistanceSq(x, y) < _vertexTolerance2) return z1;

        if (v2.GetDistanceSq(x, y) < _vertexTolerance2) return z2;

        if (Math.Abs(_nz) < _precisionThreshold) return (z0 + z1 + z2) / 3.0;

        // solve for pz
        return z0 - (_nx * sx + _ny * sy) / _nz;
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
        return true;
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
}