/*
 * Copyright 2014 Gary W. Lucas.
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
 * 03/2014  G. Lucas     Created
 * 06/2014  G. Lucas     Refactored from earlier implementation
 * 12/2015  G. Lucas     Moved into common package
 * 08/2025 M.Fender     Ported to C# with extended precision arithmetic
 *
 * Notes:
 * This C# implementation includes extended precision arithmetic using the
 * DoubleDouble class for numerical stability. When geometric predicates
 * produce results close to zero, the implementation automatically switches
 * to extended precision calculations to ensure robustness.
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

using System.Runtime.CompilerServices;

/// <summary>
///     Provides elements and methods to support geometric operations
///     for Delaunay triangulation and related computational geometry tasks.
///     This implementation uses standard double precision arithmetic with
///     automatic fallback to extended precision when needed for numerical stability.
/// </summary>
public class GeometricOperations
{
    /// <summary>
    ///     Threshold for circumcircle determinant calculations.
    /// </summary>
    private readonly double _circumcircleDeterminantThreshold;

    /// <summary>
    ///     Positive threshold for half-plane calculations.
    /// </summary>
    private readonly double _halfPlaneThreshold;

    /// <summary>
    ///     Negative threshold for half-plane calculations.
    /// </summary>
    private readonly double _halfPlaneThresholdNeg;

    /// <summary>
    ///     Threshold for in-circle calculations.
    /// </summary>
    private readonly double _inCircleThreshold;

    /// <summary>
    ///     The threshold values used for numerical precision management.
    /// </summary>
    private readonly Thresholds _thresholds;

    private long _nCircumcircle;

    private long _nExtendedPrecisionInCircle;

    private long _nHalfPlaneCalls;

    // Diagnostic counters
    private long _nInCircleCalls;

    /// <summary>
    ///     Construct an instance based on a nominal point spacing of 1 unit.
    /// </summary>
    public GeometricOperations()
        : this(new Thresholds(1.0))
    {
    }

    /// <summary>
    ///     Construct an instance based on the specified threshold values.
    /// </summary>
    /// <param name="thresholds">A valid instance</param>
    public GeometricOperations(Thresholds thresholds)
    {
        _thresholds = thresholds;
        _inCircleThreshold = thresholds.GetInCircleThreshold();
        _halfPlaneThresholdNeg = -thresholds.GetHalfPlaneThreshold();
        _halfPlaneThreshold = thresholds.GetHalfPlaneThreshold();
        _circumcircleDeterminantThreshold = thresholds.GetCircumcircleDeterminantThreshold();
    }

    /// <summary>
    ///     Determines the signed area of triangle ABC.
    /// </summary>
    /// <param name="a">The initial vertex</param>
    /// <param name="b">The second vertex</param>
    /// <param name="c">The third vertex</param>
    /// <returns>
    ///     A positive value if the triangle is oriented counterclockwise,
    ///     negative if it is oriented clockwise, or zero if it is degenerate.
    /// </returns>
    public double Area(IVertex a, IVertex b, IVertex c)
    {
        return Area(a.X, a.Y, b.X, b.Y, c.X, c.Y);
    }

    /// <summary>
    ///     Determines the signed area of triangle ABC.
    /// </summary>
    /// <param name="ax">The x coordinate of the first vertex in the triangle</param>
    /// <param name="ay">The y coordinate of the first vertex in the triangle</param>
    /// <param name="bx">The x coordinate of the second vertex in the triangle</param>
    /// <param name="by">The y coordinate of the second vertex in the triangle</param>
    /// <param name="cx">The x coordinate of the third vertex in the triangle</param>
    /// <param name="cy">The y coordinate of the third vertex in the triangle</param>
    /// <returns>
    ///     A positive value if the triangle is oriented counterclockwise,
    ///     negative if it is oriented clockwise, or zero if it is degenerate.
    /// </returns>
    public double Area(double ax, double ay, double bx, double by, double cx, double cy)
    {
        var h = (cy - ay) * (bx - ax) - (cx - ax) * (by - ay);

        // For very small results, might need extended precision
        if (-_inCircleThreshold < h && h < _inCircleThreshold) h = HalfPlane(ax, ay, bx, by, cx, cy);

        return h / 2.0;
    }

    /// <summary>
    ///     Computes the circumcircle for the coordinates of three vertices.
    ///     For efficiency purposes, results are stored in a reusable container instance.
    /// </summary>
    /// <param name="a">Vertex A</param>
    /// <param name="b">Vertex B</param>
    /// <param name="c">Vertex C</param>
    /// <param name="result">A valid instance to store the result.</param>
    /// <returns>
    ///     True if the circumcircle was computed successfully
    ///     with a finite radius; otherwise, false.
    /// </returns>
    public bool Circumcircle(IVertex a, IVertex b, IVertex c, Circumcircle result)
    {
        Circumcircle(a.X, a.Y, b.X, b.Y, c.X, c.Y, result);
        return double.IsFinite(result.GetRadiusSq());
    }

    /// <summary>
    ///     Computes the circumcircle for the coordinates of three vertices.
    ///     For efficiency purposes, results are stored in a reusable container instance.
    /// </summary>
    /// <param name="vax">The x coordinate of vertex A</param>
    /// <param name="vay">The y coordinate of vertex A</param>
    /// <param name="vbx">The x coordinate of vertex B</param>
    /// <param name="vby">The y coordinate of vertex B</param>
    /// <param name="vcx">The x coordinate of vertex C</param>
    /// <param name="vcy">The y coordinate of vertex C</param>
    /// <param name="result">A valid instance to store the result.</param>
    public void Circumcircle(
        double vax,
        double vay,
        double vbx,
        double vby,
        double vcx,
        double vcy,
        Circumcircle result)
    {
        _nCircumcircle++;

        // Remap coordinate system using (vax, vay) as origin to reduce magnitude
        var bx = vbx - vax;
        var by = vby - vay;
        var cx = vcx - vax;
        var cy = vcy - vay;

        var d = bx * cy - by * cx;

        if (Math.Abs(d) > _circumcircleDeterminantThreshold)
        {
            // Standard precision calculation
            d *= 2;
            var b2 = bx * bx + by * by;
            var c2 = cx * cx + cy * cy;
            var x = (cy * b2 - by * c2) / d;
            var y = (bx * c2 - cx * b2) / d;

            result.SetCircumcenter(x + vax, y + vay, x * x + y * y);
        }
        else
        {
            // Use extended precision for nearly degenerate cases
            CircumcircleExtendedPrecision(vax, vay, vbx, vby, vcx, vcy, result);
        }
    }

    /// <summary>
    ///     Clear the diagnostic operation counts maintained by this class.
    /// </summary>
    public void ClearDiagnostics()
    {
        _nInCircleCalls = 0;
        _nExtendedPrecisionInCircle = 0;
        _nHalfPlaneCalls = 0;
        _nCircumcircle = 0;
    }

    /// <summary>
    ///     Computes the direction parameter for a point relative to a line segment.
    ///     This is used to determine if a point projects onto the line segment.
    /// </summary>
    /// <param name="x0">X coordinate of first line point</param>
    /// <param name="y0">Y coordinate of first line point</param>
    /// <param name="x1">X coordinate of second line point</param>
    /// <param name="y1">Y coordinate of second line point</param>
    /// <param name="px">X coordinate of test point</param>
    /// <param name="py">Y coordinate of test point</param>
    /// <returns>Direction parameter (dot product of vectors)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Direction(double x0, double y0, double x1, double y1, double px, double py)
    {
        // Vector from (x0,y0) to (x1,y1)
        var dx = x1 - x0;
        var dy = y1 - y0;

        // Vector from (x0,y0) to (px,py)
        var vx = px - x0;
        var vy = py - y0;

        // Dot product: dx*vx + dy*vy
        return dx * vx + dy * vy;
    }

    /// <summary>
    ///     Computes the circumcenter of a triangle defined by three vertices.
    ///     If the vertices are collinear, this method will return a null.
    /// </summary>
    /// <param name="a">A valid vertex</param>
    /// <param name="b">A valid vertex</param>
    /// <param name="c">A valid vertex</param>
    /// <returns>
    ///     A valid vertex giving the circumcenter, or null if the
    ///     vertices are collinear.
    /// </returns>
    public IVertex GetCircumcenter(IVertex a, IVertex b, IVertex c)
    {
        // _circumcircleCount++;
        var ax = a.X;
        var ay = a.Y;
        var bx = b.X;
        var by = b.Y;
        var cx = c.X;
        var cy = c.Y;

        var d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
        if (Math.Abs(d) < _thresholds.GetCircumcircleDeterminantThreshold()) return Vertex.Null;

        var a2 = ax * ax + ay * ay;
        var b2 = bx * bx + by * by;
        var c2 = cx * cx + cy * cy;

        var x = (a2 * (by - cy) + b2 * (cy - ay) + c2 * (ay - by)) / d;
        var y = (a2 * (cx - bx) + b2 * (ax - cx) + c2 * (bx - ax)) / d;

        return new Vertex(x, y, 0, -1);
    }

    /// <summary>
    ///     Get a diagnostic count of the number of circumcircle calculations
    /// </summary>
    /// <returns>A positive integer value</returns>
    public long GetCircumcircleCount()
    {
        return _nCircumcircle;
    }

    /// <summary>
    ///     Get a diagnostic count of the number of incidents where an extended
    ///     precision calculation was required for an in-circle calculation
    ///     due to the small-magnitude value of the computed value.
    /// </summary>
    /// <returns>A positive integer value</returns>
    public long GetExtendedPrecisionInCircleCount()
    {
        return _nExtendedPrecisionInCircle;
    }

    /// <summary>
    ///     Get a diagnostic count of the number of half-plane calculations
    /// </summary>
    /// <returns>A positive integer value</returns>
    public long GetHalfPlaneCount()
    {
        return _nHalfPlaneCalls;
    }

    /// <summary>
    ///     Get a diagnostic count of the number of times an in-circle calculation was performed.
    /// </summary>
    /// <returns>A positive integer value</returns>
    public long GetInCircleCount()
    {
        return _nInCircleCalls;
    }

    /// <summary>
    ///     Gets the threshold values associated with this instance.
    /// </summary>
    /// <returns>A valid instance of Thresholds.</returns>
    public Thresholds GetThresholds()
    {
        return _thresholds;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double HalfPlane(double ax, double ay, double bx, double by, double cx, double cy)
    {
        _nHalfPlaneCalls++;

        var q11 = cx - ax;
        var q12 = ay - by;

        var q21 = cy - ay;
        var q22 = bx - ax;

        // Cross product: dx*vy - dy*vx
        var result = q11 * q12 + q21 * q22;

        // Use extended precision for very small results
        if (result > _halfPlaneThresholdNeg && result < _halfPlaneThreshold)
            return HalfPlaneExtendedPrecision(ax, ay, bx, by, cx, cy);

        return result;
    }

    /// <summary>
    ///     Determines which side of a directed line segment a point lies on.
    /// </summary>
    /// <param name="a">The first vertex of the segment</param>
    /// <param name="b">The second vertex of the segment</param>
    /// <param name="c">The point of interest</param>
    /// <returns>
    ///     Positive if the point is to the left of the edge,
    ///     negative if it is to the right, or zero if it lies on the line.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double HalfPlane(IVertex a, IVertex b, IVertex c)
    {
        return HalfPlane(a.X, a.Y, b.X, b.Y, c.X, c.Y);
    }

    /// <summary>
    ///     Determines if vertex d lies within the circumcircle of triangle a,b,c.
    /// </summary>
    /// <param name="a">A valid vertex</param>
    /// <param name="b">A valid vertex</param>
    /// <param name="c">A valid vertex</param>
    /// <param name="d">A valid vertex</param>
    /// <returns>
    ///     Positive if d is inside the circumcircle; negative if it is
    ///     outside; zero if it is on the edge.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double InCircle(IVertex a, IVertex b, IVertex c, IVertex d)
    {
        return InCircle(a.X, a.Y, b.X, b.Y, c.X, c.Y, d.X, d.Y);
    }

    /// <summary>
    ///     Determines if vertex d lies within the circumcircle of triangle a,b,c.
    /// </summary>
    /// <param name="ax">The x coordinate of vertex a</param>
    /// <param name="ay">The y coordinate of vertex a</param>
    /// <param name="bx">The x coordinate of vertex b</param>
    /// <param name="by">The y coordinate of vertex b</param>
    /// <param name="cx">The x coordinate of vertex c</param>
    /// <param name="cy">The y coordinate of vertex c</param>
    /// <param name="dx">The x coordinate of vertex d</param>
    /// <param name="dy">The y coordinate of vertex d</param>
    /// <returns>
    ///     Positive if d is inside the circumcircle; negative if it is
    ///     outside; zero if it is on the edge.
    /// </returns>
    public double InCircle(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy)
    {
        _nInCircleCalls++;

        // Shewchuk's robust computation using differences to reduce
        // magnitude and improve numerical accuracy

        // Column 1: differences from point d
        var a11 = ax - dx;
        var a21 = bx - dx;
        var a31 = cx - dx;

        // Column 2: differences from point d
        var a12 = ay - dy;
        var a22 = by - dy;
        var a32 = cy - dy;

        // Calculate the determinant for the in-circle test
        // This is organized to group terms of similar magnitude
        var inCircle = (a11 * a11 + a12 * a12) * (a21 * a32 - a31 * a22)
                       + (a21 * a21 + a22 * a22) * (a31 * a12 - a11 * a32)
                       + (a31 * a31 + a32 * a32) * (a11 * a22 - a21 * a12);

        // Use extended precision when the result is close to zero
        if (-_inCircleThreshold < inCircle && inCircle < _inCircleThreshold)
        {
            _nExtendedPrecisionInCircle++;
            return InCircleExtendedPrecision(ax, ay, bx, by, cx, cy, dx, dy);
        }

        return inCircle;
    }

    /// <summary>
    ///     Integer-style in-circle test: returns 1 if inside, -1 if outside, 0 if on circle (within thresholds).
    ///     Mirrors OrientationTest style for predicates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int InCircleTest(IVertex a, IVertex b, IVertex c, IVertex d)
    {
        var v = InCircle(a, b, c, d);
        if (v > _inCircleThreshold) return 1;
        if (v < -_inCircleThreshold) return -1;
        return 0;
    }

    /// <summary>
    ///     Determines the orientation of three points (left turn, right turn, or collinear).
    /// </summary>
    /// <param name="ax">X coordinate of the first point</param>
    /// <param name="ay">Y coordinate of the first point</param>
    /// <param name="bx">X coordinate of the second point</param>
    /// <param name="by">Y coordinate of the second point</param>
    /// <param name="cx">X coordinate of the third point</param>
    /// <param name="cy">Y coordinate of the third point</param>
    /// <returns>
    ///     If the triangle has a counterclockwise order, a positive value;
    ///     if the triangle is degenerate, a zero value; if the triangle has
    ///     a clockwise order, a negative value.
    /// </returns>
    public double Orientation(double ax, double ay, double bx, double by, double cx, double cy)
    {
        var result = (ax - cx) * (by - cy) - (bx - cx) * (ay - cy);

        // Use extended precision for very small results
        if (result > _halfPlaneThresholdNeg && result < _halfPlaneThreshold)
            return OrientationExtendedPrecision(ax, ay, bx, by, cx, cy);

        return result;
    }

    /// <summary>
    ///     Determines the orientation of three vertices.
    /// </summary>
    /// <param name="a">The first vertex</param>
    /// <param name="b">The second vertex</param>
    /// <param name="c">The third vertex</param>
    /// <returns>Positive for counterclockwise, negative for clockwise, zero for collinear.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Orientation(IVertex a, IVertex b, IVertex c)
    {
        return Orientation(a.X, a.Y, b.X, b.Y, c.X, c.Y);
    }

    /// <summary>
    ///     Tests the orientation of three points.
    /// </summary>
    /// <param name="a">First point</param>
    /// <param name="b">Second point</param>
    /// <param name="c">Third point</param>
    /// <returns>1 for counterclockwise, -1 for clockwise, 0 for collinear</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int OrientationTest(IVertex a, IVertex b, IVertex c)
    {
        var result = Orientation(a, b, c);
        if (result > 0) return 1;
        if (result < 0) return -1;
        return 0;
    }

    /// <summary>
    ///     Computes the circumcircle using extended precision arithmetic for
    ///     nearly degenerate triangles.
    /// </summary>
    /// <param name="vax">The x coordinate of vertex A</param>
    /// <param name="vay">The y coordinate of vertex A</param>
    /// <param name="vbx">The x coordinate of vertex B</param>
    /// <param name="vby">The y coordinate of vertex B</param>
    /// <param name="vcx">The x coordinate of vertex C</param>
    /// <param name="vcy">The y coordinate of vertex C</param>
    /// <param name="result">A valid instance to store the result.</param>
    private void CircumcircleExtendedPrecision(
        double vax,
        double vay,
        double vbx,
        double vby,
        double vcx,
        double vcy,
        Circumcircle result)
    {
        // Extended precision circumcircle calculation
        var q11 = new DoubleDouble(vbx) - new DoubleDouble(vax); // bx
        var q12 = new DoubleDouble(vby) - new DoubleDouble(vay); // by
        var q21 = new DoubleDouble(vcx) - new DoubleDouble(vax); // cx
        var q22 = new DoubleDouble(vcy) - new DoubleDouble(vay); // cy

        var det = (q11 * q22 - q12 * q21) * 2.0; // determinant * 2

        if (det.ToDouble() == 0.0)
        {
            // Truly degenerate triangle
            result.SetCircumcenter(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
            return;
        }

        var b2 = q11 * q11 + q12 * q12;
        var c2 = q21 * q21 + q22 * q22;

        var xCenter = (q22 * b2 - q12 * c2) / det;
        var yCenter = (q11 * c2 - q21 * b2) / det;

        var radiusSq = xCenter * xCenter + yCenter * yCenter;

        xCenter = xCenter + new DoubleDouble(vax);
        yCenter = yCenter + new DoubleDouble(vay);

        result.SetCircumcenter(xCenter.ToDouble(), yCenter.ToDouble(), radiusSq.ToDouble());
    }

    /// <summary>
    ///     Uses extended precision arithmetic to compute the half-plane test.
    ///     This method provides higher accuracy for determining the orientation
    ///     of points that may be nearly collinear.
    /// </summary>
    private double HalfPlaneExtendedPrecision(double ax, double ay, double bx, double by, double cx, double cy)
    {
        // Use DoubleDouble arithmetic for cross product calculation
        var q11 = new DoubleDouble(cx) - new DoubleDouble(ax); // dx
        var q12 = new DoubleDouble(ay) - new DoubleDouble(by); // vy
        var q21 = new DoubleDouble(cy) - new DoubleDouble(ay); // dy
        var q22 = new DoubleDouble(cx) - new DoubleDouble(ax); // vx

        // Cross product: dx*vy - dy*vx
        var result = q11 * q12 + q21 * q22;
        return result.ToDouble();
    }

    /// <summary>
    ///     Uses extended precision arithmetic to determine if vertex d lies
    ///     within the circumcircle of triangle a,b,c. This method provides
    ///     higher accuracy but requires more processing.
    /// </summary>
    /// <param name="ax">The x coordinate of vertex a</param>
    /// <param name="ay">The y coordinate of vertex a</param>
    /// <param name="bx">The x coordinate of vertex b</param>
    /// <param name="by">The y coordinate of vertex b</param>
    /// <param name="cx">The x coordinate of vertex c</param>
    /// <param name="cy">The y coordinate of vertex c</param>
    /// <param name="dx">The x coordinate of vertex d</param>
    /// <param name="dy">The y coordinate of vertex d</param>
    /// <returns>
    ///     Positive if d is inside the circumcircle; negative if it is
    ///     outside; zero if it is on the edge.
    /// </returns>
    private double InCircleExtendedPrecision(
        double ax,
        double ay,
        double bx,
        double by,
        double cx,
        double cy,
        double dx,
        double dy)
    {
        // Extended precision version of the in-circle test
        // using DoubleDouble arithmetic for numerical stability
        var q11 = new DoubleDouble(ax) - new DoubleDouble(dx);
        var q21 = new DoubleDouble(bx) - new DoubleDouble(dx);
        var q31 = new DoubleDouble(cx) - new DoubleDouble(dx);

        var q12 = new DoubleDouble(ay) - new DoubleDouble(dy);
        var q22 = new DoubleDouble(by) - new DoubleDouble(dy);
        var q32 = new DoubleDouble(cy) - new DoubleDouble(dy);

        var q11s = q11 * q11;
        var q12s = q12 * q12;
        var q21s = q21 * q21;
        var q22s = q22 * q22;
        var q31s = q31 * q31;
        var q32s = q32 * q32;

        var q11_22 = q11 * q22;
        var q11_32 = q11 * q32;
        var q21_12 = q21 * q12;
        var q21_32 = q21 * q32;
        var q31_22 = q31 * q22;
        var q31_12 = q31 * q12;

        // Calculate the terms of the determinant
        var s1 = q11s + q12s;
        var s2 = q21s + q22s;
        var s3 = q31s + q32s;

        var t1 = q21_32 - q31_22;
        var t2 = q31_12 - q11_32;
        var t3 = q11_22 - q21_12;

        var result = s1 * t1 + s2 * t2 + s3 * t3;

        return result.ToDouble();
    }

    /// <summary>
    ///     Uses extended precision arithmetic to compute the orientation of three points.
    /// </summary>
    /// <param name="ax">X coordinate of the first point</param>
    /// <param name="ay">Y coordinate of the first point</param>
    /// <param name="bx">X coordinate of the second point</param>
    /// <param name="by">Y coordinate of the second point</param>
    /// <param name="cx">X coordinate of the third point</param>
    /// <param name="cy">Y coordinate of the third point</param>
    /// <returns>Extended precision orientation result</returns>
    private double OrientationExtendedPrecision(double ax, double ay, double bx, double by, double cx, double cy)
    {
        var q11 = new DoubleDouble(ax) - new DoubleDouble(cx);
        var q12 = new DoubleDouble(by) - new DoubleDouble(cy);
        var q21 = new DoubleDouble(bx) - new DoubleDouble(cx);
        var q22 = new DoubleDouble(ay) - new DoubleDouble(cy);

        var result = q11 * q12 - q21 * q22;
        return result.ToDouble();
    }
}