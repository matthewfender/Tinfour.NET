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
 * 11/2018  G. Lucas     Created
 * 08/2025  M.Fender     Ported to C#
 * 09/2025  M.Fender      Avoid index-based visitation (flags mutate indices)
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

using System.Collections;

/// <summary>
///     An iterator for enumerating the triangles in a TIN.
/// </summary>
public class SimpleTriangleIterator : IEnumerable<SimpleTriangle>
{
    private readonly IIncrementalTin _tin;

    /// <summary>
    ///     Initializes a new instance of the SimpleTriangleIterator class.
    /// </summary>
    /// <param name="tin">The TIN to iterate through</param>
    public SimpleTriangleIterator(IIncrementalTin tin)
    {
        ArgumentNullException.ThrowIfNull(tin);
        _tin = tin;
    }

    /// <summary>
    ///     Returns this instance as an enumerator.
    /// </summary>
    /// <returns>This instance as an enumerator.</returns>
    public IEnumerator<SimpleTriangle> GetEnumerator()
    {
        if (!_tin.IsBootstrapped()) yield break;

        // Use index-based visitation like Java implementation
        var maxIndex = _tin.GetMaximumEdgeAllocationIndex() + 2;
        var visited = new BitArray(maxIndex);
        var edgeIterator = _tin.GetEdgeIterator().GetEnumerator();
        IQuadEdge? nextEdge = null;

        while (true)
        {
            // Get the next edge to process
            if (nextEdge == null)
            {
                if (!edgeIterator.MoveNext())

                    // No more edges to process
                    break;
                nextEdge = edgeIterator.Current;
            }

            // Like Java implementation, advance nextEdge appropriately
            var e = nextEdge;
            var eIndex = e.GetIndex();

            // Process edge and dual like Java implementation
            if ((eIndex & 1) == 0)

                // If even index, process dual next
                nextEdge = e.GetDual();
            else

                // If odd index, get next from iterator
                nextEdge = null;

            // Check if this edge forms a triangle that hasn't been visited
            if (!visited[eIndex])
            {
                var ef = e.GetForward();
                var er = ef.GetForward(); // Follow Java pattern: ef.GetForward() should be the reverse of e

                // Critical: Validate this forms a proper triangle
                if (er.GetForward() == e)
                {
                    // Mark all edges as visited
                    visited[eIndex] = true;
                    visited[ef.GetIndex()] = true;
                    visited[er.GetIndex()] = true;

                    // Check for ghost triangles (vertices should not be null)
                    var a = e.GetA();
                    var b = ef.GetA();
                    var c = er.GetA();

                    if (!a.IsNullVertex() && !b.IsNullVertex() && !c.IsNullVertex())

                        // Valid triangle found
                        yield return new SimpleTriangle(e, ef, er);
                }
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}