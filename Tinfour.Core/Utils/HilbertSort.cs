/*
 * Copyright 2014-2025 Gary W. Lucas, G.W. Lucas
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
 * 05/2014  G. Lucas     Java implementation created
 * 08/2025  M. Fender     C# port: non-mutating, IEnumerable-based API
 * 11/2025  M. Fender    Added Span<T> optimizations for better performance
 *
 * Notes:
 *  - This implementation does NOT mutate Vertex (struct) indices; instead it
 *    computes Hilbert keys externally and returns a new ordered sequence.
 *  - Null/ghost vertices are ignored for extent calculation but kept in output order
 *    if present.
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Utils;

using Tinfour.Core.Common;

/// <summary>
///     Utility to order vertices by their Hilbert space-filling curve rank to improve
///     spatial locality for incremental triangulation.
/// </summary>
public static class HilbertSort
{
    /// <summary>
    ///     Orders the given vertices by their Hilbert rank. If the input is too small
    ///     or degenerate (zero width/height), the original order is returned.
    /// </summary>
    /// <param name="vertices">Input vertices</param>
    /// <returns>Vertices ordered by Hilbert rank</returns>
    public static IEnumerable<IVertex> Sort(IEnumerable<IVertex> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);

        var list = vertices as IList<IVertex> ?? vertices.ToList();
        var n = list.Count;
        if (n == 0) return list;

        double xMin = double.PositiveInfinity, xMax = double.NegativeInfinity;
        double yMin = double.PositiveInfinity, yMax = double.NegativeInfinity;
        for (var i = 0; i < n; i++)
        {
            var v = list[i];
            if (v.IsNullVertex()) continue;
            if (v.X < xMin) xMin = v.X;
            if (v.X > xMax) xMax = v.X;
            if (v.Y < yMin) yMin = v.Y;
            if (v.Y > yMax) yMax = v.Y;
        }

        var xDelta = xMax - xMin;
        var yDelta = yMax - yMin;
        if (n < 24 || xDelta == 0.0 || yDelta == 0.0 || double.IsInfinity(xMin)) return list;

        var hn = Math.Log(n) / 0.693147180559945 / 2.0;
        var nHilbert = (int)Math.Floor(hn + 0.5);
        if (nHilbert < 4) nHilbert = 4;
        if (nHilbert > 15) nHilbert = 15;

        var hScale = (1 << nHilbert) - 1.0;

        var keyed = new (int key, int pos, IVertex v)[n];
        var keyedSpan = keyed.AsSpan();
        
        for (var i = 0; i < n; i++)
        {
            var v = list[i];
            if (v.IsNullVertex())
            {
                keyedSpan[i] = (int.MinValue, i, v);
                continue;
            }

            var ix = (int)(hScale * (v.X - xMin) / xDelta);
            var iy = (int)(hScale * (v.Y - yMin) / yDelta);
            var key = XyToHilbert(ix, iy, nHilbert);
            keyedSpan[i] = (key, i, v);
        }

        keyedSpan.Sort(static (a, b) =>
        {
            var d = a.key - b.key;
            return d != 0 ? d : a.pos - b.pos;
        });

        return Enumerate(keyed);

        static IEnumerable<IVertex> Enumerate((int key, int pos, IVertex v)[] data)
        {
            for (var i = 0; i < data.Length; i++)
                yield return data[i].v;
        }
    }

    /// <summary>
    ///     Lam &amp; Shapiro method for integer Hilbert index from Hacker's Delight.
    /// </summary>
    private static int XyToHilbert(int px, int py, int n)
    {
        int s = 0, x = px, y = py;
        for (var i = n - 1; i >= 0; i--)
        {
            var xi = (x >> i) & 1;
            var yi = (y >> i) & 1;
            if (yi == 0)
            {
                var temp = x;
                x = y ^ -xi;
                y = temp ^ -xi;
            }

            s = 4 * s + 2 * xi + (xi ^ yi);
        }

        return s;
    }
}