/*
 * Copyright (C) 2020  Gary W. Lucas.
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
 * 12/2020  G. Lucas     Created
 * 08/2025 M.Fender     Ported to C#
 * 11/2025  M. Fender    Added Span<T> optimizations and stackalloc for small arrays
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Utils;

using System.Runtime.CompilerServices;

using Tinfour.Core.Common;

/// <summary>
///     Implements utilities for computing Barycentric Coordinates.
///     The algorithm for computing coordinates is based on
///     Hormann, Kai. (2005). "Barycentric Coordinates for Arbitrary
///     Polygons in the Plane -- Technical Report IfI-05-05",
///     Institute fur Informatik, Technische Universitat Clausthal.
/// </summary>
/// <remarks>
///     <strong>Development Status:</strong> At this time, this class has
///     not been thoroughly reviewed and has undergone only superficial testing.
/// </remarks>
public class BarycentricCoordinates
{
    private double _barycentricCoordinateDeviation;

    /// <summary>
    ///     Gets the deviation of the computed equivalent of the input query (x,y)
    ///     coordinates based on barycentric coordinates.
    ///     While the computed equivalent should
    ///     be an exact match for the query point, errors in implementation
    ///     or numeric errors due to floating-point precision limitations would
    ///     result in a deviation. Thus, this method provides a diagnostic
    ///     on the most recent computation. A large non-zero value indicates a
    ///     potential implementation problem. A small non-zero value indicates an
    ///     error due to numeric issues.
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
    ///     Given a reference point inside a simple, but potentially non-convex
    ///     polygon, creates an array of barycentric coordinates for the point. The
    ///     coordinates are normalized, so that their sum is 1.0. This method
    ///     populates the barycentric deviation member element which may be
    ///     used as a figure of merit for evaluating the success of the
    ///     coordinate computation. If the point is not inside the polygon or if
    ///     the polygon is self-intersecting, the results are undefined
    ///     and the method may return a null array or a meaningless result.
    ///     If the point is on the perimeter of the polygon, this method will
    ///     return a null array.
    /// </summary>
    /// <param name="polygon">
    ///     List of vertices defining a non-self-intersecting,
    ///     potentially non-convex polygon.
    /// </param>
    /// <param name="x">The x coordinate of the reference point</param>
    /// <param name="y">The y coordinate of the reference point</param>
    /// <returns>If successful, a valid array; otherwise null.</returns>
    public double[]? GetBarycentricCoordinates(IList<Vertex> polygon, double x, double y)
    {
        var nVertices = polygon.Count;
        if (nVertices < 3) return null;

        var v0 = polygon[nVertices - 1];
        var v1 = polygon[0];

        // Some applications create polygons that give the same point
        // as the start and end of the polygon. In such cases, we
        // adjust nVertices down to simplify the arithmetic below.
        if (v0.Equals(v1))
        {
            nVertices--;
            if (nVertices < 3) return null;
            v0 = polygon[nVertices - 1];
        }

        // Use stackalloc for small polygons to avoid heap allocation
        const int stackAllocThreshold = 64;
        Span<double> weights = nVertices <= stackAllocThreshold 
            ? stackalloc double[nVertices] 
            : new double[nVertices];
        
        double wSum = 0;

        var x0 = v0.GetX() - x;
        var y0 = v0.GetY() - y;
        var x1 = v1.GetX() - x;
        var y1 = v1.GetY() - y;
        var r0 = Math.Sqrt(x0 * x0 + y0 * y0);
        var r1 = Math.Sqrt(x1 * x1 + y1 * y1);

        // Check for point on polygon perimeter
        if (r0 == 0.0 || r1 == 0.0) return null;

        var denominator = x0 * y1 - x1 * y0;
        if (Math.Abs(denominator) < 1e-15)

            // Point is on the perimeter or polygon is degenerate
            return null;

        var t1 = (r0 * r1 - (x0 * x1 + y0 * y1)) / denominator;

        for (var iEdge = 0; iEdge < nVertices; iEdge++)
        {
            var index = (iEdge + 1) % nVertices;
            v1 = polygon[index];

            var t0 = t1;
            x0 = x1;
            y0 = y1;
            r0 = r1;
            x1 = v1.GetX() - x;
            y1 = v1.GetY() - y;
            r1 = Math.Sqrt(x1 * x1 + y1 * y1);

            // Check for point on polygon perimeter
            if (r1 == 0.0) return null;

            denominator = x0 * y1 - x1 * y0;
            if (Math.Abs(denominator) < 1e-15)

                // Point is on the perimeter
                return null;

            t1 = (r0 * r1 - (x0 * x1 + y0 * y1)) / denominator;
            var w = (t0 + t1) / r0;
            wSum += w;
            weights[iEdge] = w;
        }

        // Check for valid weight sum
        if (Math.Abs(wSum) < 1e-15) return null;

        // Normalize the weights and copy to result array
        var result = new double[nVertices];
        for (var i = 0; i < nVertices; i++) result[i] = weights[i] / wSum;

        // Calculate deviation for quality assessment
        double xSum = 0;
        double ySum = 0;
        for (var i = 0; i < result.Length; i++)
        {
            var v = polygon[i];
            xSum += result[i] * (v.GetX() - x);
            ySum += result[i] * (v.GetY() - y);
        }

        _barycentricCoordinateDeviation = Math.Sqrt(xSum * xSum + ySum * ySum);

        return result;
    }

    /// <summary>
    ///     Interpolates a value at the specified point using barycentric coordinates
    ///     and the given polygon vertices with their associated values.
    /// </summary>
    /// <param name="polygon">List of vertices defining the polygon</param>
    /// <param name="values">Values associated with each vertex (must match polygon count)</param>
    /// <param name="x">X coordinate of the point to interpolate</param>
    /// <param name="y">Y coordinate of the point to interpolate</param>
    /// <returns>The interpolated value, or NaN if interpolation fails</returns>
    public double InterpolateValue(IList<Vertex> polygon, IList<double> values, double x, double y)
    {
        if (polygon.Count != values.Count)
            throw new ArgumentException("Polygon vertices and values must have the same count");

        var weights = GetBarycentricCoordinates(polygon, x, y);
        if (weights == null) return double.NaN;

        var result = 0.0;
        for (var i = 0; i < weights.Length; i++) result += weights[i] * values[i];

        return result;
    }
}