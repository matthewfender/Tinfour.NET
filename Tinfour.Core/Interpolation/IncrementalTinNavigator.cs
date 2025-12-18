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
 * 08/2014  G. Lucas     Created
 * 08/2025 M.Fender     Ported to C#
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Interpolation;

using Tinfour.Core.Common;

/// <summary>
///     Provides navigation services for interpolation operations using
///     StochasticLawsonsWalk for point location.
/// </summary>
public class IncrementalTinNavigator : IIncrementalTinNavigator
{
    private readonly IIncrementalTin _tin;

    private readonly StochasticLawsonsWalk _walker;

    private IQuadEdge? _searchEdge;

    /// <summary>
    ///     Constructs a navigator for the specified TIN.
    /// </summary>
    /// <param name="tin">The TIN to navigate</param>
    public IncrementalTinNavigator(IIncrementalTin tin)
    {
        _tin = tin ?? throw new ArgumentNullException(nameof(tin));
        _walker = new StochasticLawsonsWalk(tin.GetThresholds());
    }

    /// <summary>
    ///     Finds an edge from the triangle that contains the specified coordinates.
    ///     If the coordinates are outside the TIN, returns an edge on the perimeter.
    /// </summary>
    /// <param name="x">The x coordinate for point location</param>
    /// <param name="y">The y coordinate for point location</param>
    /// <returns>An edge from the containing triangle or perimeter edge</returns>
    public IQuadEdge GetNeighborEdge(double x, double y)
    {
        if (!_tin.IsBootstrapped()) throw new InvalidOperationException("TIN is not bootstrapped");

        // Get a starting edge if we don't have one
        if (_searchEdge == null)
        {
            var edges = _tin.GetEdges();
            if (edges.Count == 0) throw new InvalidOperationException("No edges available in TIN");

            _searchEdge = edges[0];
        }

        // Use the walker to find the containing triangle
        // note that we save the returned edge since it is common during interpolations that we get a request
        // for a series of nearby points. Saving the edge allows us to start the search from a more optimal location.
        _searchEdge = _walker.FindAnEdgeFromEnclosingTriangle(_searchEdge, x, y);
        return _searchEdge;
    }

    /// <summary>
    ///     Resets the navigator state for changes to the TIN.
    /// </summary>
    public void ResetForChangeToTin()
    {
        _searchEdge = null;
        _walker.Reset();
    }

    /// <summary>
    ///     Finds the triangle that contains the specified coordinates.
    /// </summary>
    /// <param name="x">The x coordinate for point location</param>
    /// <param name="y">The y coordinate for point location</param>
    /// <returns>The containing triangle, or null if outside the TIN or ghost triangle</returns>
    public SimpleTriangle? GetContainingTriangle(double x, double y)
    {
        var edge = GetNeighborEdge(x, y);
        if (edge == null)
            return null;

        var triangle = new SimpleTriangle(edge);
        return triangle.IsGhost() ? null : triangle;
    }

    /// <summary>
    ///     Finds the vertex nearest to the specified coordinates.
    /// </summary>
    /// <param name="x">The x coordinate</param>
    /// <param name="y">The y coordinate</param>
    /// <returns>The nearest vertex, or null if the TIN is empty</returns>
    public IVertex? GetNearestVertex(double x, double y)
    {
        var edge = GetNeighborEdge(x, y);
        if (edge == null)
            return null;

        // Check the three vertices of the containing triangle
        var a = edge.GetA();
        var b = edge.GetB();
        var c = edge.GetForward().GetB();

        IVertex? nearest = null;
        var minDist = double.PositiveInfinity;

        if (!a.IsNullVertex())
        {
            var d = a.GetDistanceSq(x, y);
            if (d < minDist)
            {
                minDist = d;
                nearest = a;
            }
        }

        if (!b.IsNullVertex())
        {
            var d = b.GetDistanceSq(x, y);
            if (d < minDist)
            {
                minDist = d;
                nearest = b;
            }
        }

        if (!c.IsNullVertex())
        {
            var d = c.GetDistanceSq(x, y);
            if (d < minDist)
            {
                nearest = c;
            }
        }

        return nearest;
    }
}