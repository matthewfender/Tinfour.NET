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
 * 08/2018  G. Lucas     Initial implementation
 * 08/2025 M.Fender     Ported to C# for Tinfour.Core
 *
 * Notes:
 *  The algorithm used in this class is taken from
 * "Computational Geometry in C (2nd Edition)", Joseph O'Rourke,
 * Cambridge University Press, 1998.  Page 239 ("Point in Polygon").
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Utils;

using Tinfour.Core.Common;

/// <summary>
///     A utility for determining whether a specified coordinate is inside a polygon
///     defined by IQuadEdge instances.
/// </summary>
public static class Polyside
{
    /// <summary>
    ///     An enumeration indicating the result of a point-in-polygon test
    /// </summary>
    public enum Result
    {
        /// <summary>
        ///     The point is unambiguously outside the polygon
        /// </summary>
        Outside = 0,

        /// <summary>
        ///     The point is unambiguously inside the polygon
        /// </summary>
        Inside = 1,

        /// <summary>
        ///     The point is on the edge of the polygon
        /// </summary>
        Edge = 2
    }

    /// <summary>
    ///     Extension method to check if the result indicates the point is covered by the polygon.
    /// </summary>
    /// <param name="result">The result to check</param>
    /// <returns>True if the polygon covers the specified coordinates (inside or on edge); false if outside.</returns>
    public static bool IsCovered(this Result result)
    {
        return result == Result.Inside || result == Result.Edge;
    }

    /// <summary>
    ///     Determines if a point is inside a polygon. The polygon must be a simple
    ///     (non-self-intersecting) loop, but may be either convex or non-convex.
    ///     The polygon must have complete closure so that the terminal vertex of
    ///     the last edge has the same coordinates as the initial vertex of the
    ///     first edge. The polygon must have a non-zero area.
    /// </summary>
    /// <param name="list">A list of edges.</param>
    /// <param name="x">The Cartesian coordinate of the query point</param>
    /// <param name="y">The Cartesian coordinate of the query point</param>
    /// <returns>A valid Result enumeration</returns>
    /// <exception cref="ArgumentException">Thrown if the polygon has fewer than 3 edges or is not closed</exception>
    public static Result IsPointInPolygon(List<IQuadEdge> list, double x, double y)
    {
        var n = list.Count;
        if (n < 3)
            throw new ArgumentException($"A polygon needs at least three edges, but the input size is {n}");

        var e0 = list[0];
        var e1 = list[n - 1];
        if (!e0.GetA().Equals(e1.GetB()))
            throw new ArgumentException("Input polygon is not closed " + "(last edge must at at start of first)");

        var rCross = 0;
        var lCross = 0;

        foreach (var e in list)
        {
            var v0 = e.GetA();
            var v1 = e.GetB();
            var x0 = v0.X;
            var y0 = v0.Y;
            var x1 = v1.X;
            var y1 = v1.Y;
            var yDelta = y0 - y1;

            if (y1 > y != y0 > y)
            {
                var xTest = (x1 * y0 - x0 * y1 + y * (x0 - x1)) / yDelta;
                if (xTest > x) rCross++;
            }

            if (y1 < y != y0 < y)
            {
                var xTest = (x1 * y0 - x0 * y1 + y * (x0 - x1)) / yDelta;
                if (xTest < x) lCross++;
            }
        }

        // (rCross%2) != (lCross%2)
        if (((rCross ^ lCross) & 0x01) == 1) return Result.Edge;

        if ((rCross & 0x01) == 1) return Result.Inside;

        return Result.Outside;
    }
}