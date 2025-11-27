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
 * Date Name Description
 * ------   ---------   -------------------------------------------------
 * 04/2014  G. Lucas    Created
 * 08/2025 M.Fender    Ported to C#
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

using System.Runtime.CompilerServices;

/// <summary>
///     Provides center coordinates and radius for a circumcircle.
/// </summary>
public class Circumcircle
{
    /// <summary>
    ///     An arbitrary minimum area for which a circumcircle should be constructed
    ///     in order to avoid failures due to numerical precision issues.
    /// </summary>
    private const double MinTriangleArea = 1.0e-20;

    /// <summary>
    ///     The x coordinate of the center of the circumcircle.
    /// </summary>
    private double _centerX;

    /// <summary>
    ///     The y coordinate of the center of the circumcircle.
    /// </summary>
    private double _centerY;

    /// <summary>
    ///     The square of the radius of the center of the circumcircle.
    /// </summary>
    private double _r2;

    /// <summary>
    ///     Computes the circumcircle for the specified vertices.
    ///     Vertices are assumed to be given in counterclockwise order.
    ///     Any null inputs for the vertices results in an infinite circumcircle.
    ///     Vertices resulting in a degenerate (nearly zero area) triangle
    ///     result in an infinite circumcircle.
    /// </summary>
    /// <param name="a">The initial vertex.</param>
    /// <param name="b">The second vertex.</param>
    /// <param name="c">The third vertex.</param>
    /// <returns>A valid circumcircle.</returns>
    public static Circumcircle ComputeCircumcircle(Vertex? a, Vertex? b, Vertex? c)
    {
        var circle = new Circumcircle();
        if (a == null || b == null || c == null)
        {
            circle._centerX = double.PositiveInfinity;
            circle._centerY = double.PositiveInfinity;
            circle._r2 = double.PositiveInfinity;
            return circle;
        }

        circle.Compute(a.Value.X, a.Value.Y, b.Value.X, b.Value.Y, c.Value.X, c.Value.Y);
        return circle;
    }

    /// <summary>
    ///     Computes the circumcircle for the specified vertices and stores
    ///     results in elements of this instance.
    ///     Vertices are assumed to be given in counterclockwise order.
    ///     Any null inputs for the vertices results in an infinite circumcircle.
    ///     Vertices resulting in a degenerate (nearly zero area) triangle
    ///     result in an infinite circumcircle.
    /// </summary>
    /// <param name="a">The initial vertex.</param>
    /// <param name="b">The second vertex.</param>
    /// <param name="c">The third vertex.</param>
    /// <returns>
    ///     True if the computation successfully yields a circle of
    ///     finite radius; otherwise, false.
    /// </returns>
    public bool Compute(IVertex a, IVertex b, IVertex c)
    {
        if (a.IsNullVertex() || b.IsNullVertex() || c.IsNullVertex())
        {
            _centerX = double.PositiveInfinity;
            _centerY = double.PositiveInfinity;
            _r2 = double.PositiveInfinity;
            return false;
        }

        Compute(a.X, a.Y, b.X, b.Y, c.X, c.Y);
        return double.IsFinite(_r2); // also covers NaN case
    }

    /// <summary>
    ///     Computes the circumcircle for the specified vertices and stores
    ///     results in elements of this instance.
    ///     Vertices are assumed to be given in counterclockwise order.
    ///     Any null inputs for the vertices results in an infinite circumcircle.
    ///     Vertices resulting in a degenerate (nearly zero area) triangle
    ///     result in an infinite circumcircle.
    /// </summary>
    /// <param name="x0">The x coordinate of the first vertex</param>
    /// <param name="y0">The y coordinate of the first vertex</param>
    /// <param name="x1">The x coordinate of the second vertex</param>
    /// <param name="y1">The y coordinate of the second vertex</param>
    /// <param name="x2">The x coordinate of the third vertex</param>
    /// <param name="y2">The y coordinate of the third vertex</param>
    public void Compute(double x0, double y0, double x1, double y1, double x2, double y2)
    {
        var bx = x1 - x0;
        var by = y1 - y0;
        var cx = x2 - x0;
        var cy = y2 - y0;

        var d = 2 * (bx * cy - by * cx);
        if (Math.Abs(d) < MinTriangleArea)
        {
            // the triangle is close to the degenerate case
            // (all 3 points in a straight line)
            // even if determinant d is not zero, numeric precision
            // issues might lead to a very poor computation for
            // the circumcircle.
            _centerX = double.PositiveInfinity;
            _centerY = double.PositiveInfinity;
            _r2 = double.PositiveInfinity;
            return;
        }

        var b2 = bx * bx + by * by;
        var c2 = cx * cx + cy * cy;
        _centerX = (cy * b2 - by * c2) / d;
        _centerY = (bx * c2 - cx * b2) / d;
        _r2 = _centerX * _centerX + _centerY * _centerY;
        _centerX += x0;
        _centerY += y0;
    }

    /// <summary>
    ///     Copies the content of the specified circumcircle instance.
    /// </summary>
    /// <param name="c">A valid circumcircle instance.</param>
    public void Copy(Circumcircle c)
    {
        _centerX = c._centerX;
        _centerY = c._centerY;
        _r2 = c._r2;
    }

    /// <summary>
    ///     Gets the bounds of the circumcircle as a rectangle.
    /// </summary>
    /// <returns>A tuple containing (left, top, width, height) of the bounding rectangle.</returns>
    public (double Left, double Top, double Width, double Height) GetBounds()
    {
        var r = GetRadius();
        return (_centerX - r, _centerY - r, 2 * r, 2 * r);
    }

    /// <summary>
    ///     Gets the radius of the circumcircle.
    /// </summary>
    /// <returns>For a non-degenerate triangle, a positive floating point value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetRadius()
    {
        return Math.Sqrt(_r2);
    }

    /// <summary>
    ///     Gets the square of the radius of the circumcircle.
    /// </summary>
    /// <returns>
    ///     For a non-degenerate triangle, a positive floating point value
    ///     (potentially Infinity for a ghost triangle).
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetRadiusSq()
    {
        return _r2;
    }

    /// <summary>
    ///     Gets the x coordinate of the center of the circumcircle.
    /// </summary>
    /// <returns>A valid floating point value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetX()
    {
        return _centerX;
    }

    /// <summary>
    ///     Gets the y coordinate of the center of the circumcircle.
    /// </summary>
    /// <returns>A valid floating point value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetY()
    {
        return _centerY;
    }

    /// <summary>
    ///     Sets the coordinate for the circumcenter and radius for this instance.
    /// </summary>
    /// <param name="x">The x coordinate for the circumcenter</param>
    /// <param name="y">The y coordinate for the circumcenter</param>
    /// <param name="r2">The square of the radius for the circumcircle</param>
    public void SetCircumcenter(double x, double y, double r2)
    {
        _centerX = x;
        _centerY = y;
        _r2 = r2;
    }

    /// <summary>
    ///     Gets a string representation of the circumcircle.
    /// </summary>
    /// <returns>A formatted string containing center coordinates and radius.</returns>
    public override string ToString()
    {
        return $"({_centerX:F4},{_centerY:F4}) r={Math.Sqrt(_r2):F4}";
    }
}