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
 * 08/2025  M. Fender    Ported to C#
 * 07/2026  M. Fender    Handle-native point location (#832)
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Interpolation;

using Tinfour.Core.Common;
using Tinfour.Core.Edge;

/// <summary>
///     Provides navigation services for interpolation operations using
///     StochasticLawsonsWalk for point location.
/// </summary>
public class IncrementalTinNavigator : IIncrementalTinNavigator
{
    private readonly IIncrementalTin _tin;

    private readonly StochasticLawsonsWalk _walker;

    private EdgeStore? _store;

    private int _searchHandle = EdgeStore.NullHandle;

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
        var h = GetNeighborEdgeHandle(x, y);
        return _store!.Wrap(h);
    }

    /// <summary>
    ///     Handle-native point location: returns the handle of an edge from the
    ///     triangle containing the coordinates (or the associated perimeter edge).
    ///     The search edge is retained between queries, so a series of nearby
    ///     requests starts each walk close to its target.
    /// </summary>
    /// <param name="x">The x coordinate for point location</param>
    /// <param name="y">The y coordinate for point location</param>
    /// <returns>The handle of an edge from the containing triangle or perimeter edge</returns>
    internal int GetNeighborEdgeHandle(double x, double y)
    {
        if (!_tin.IsBootstrapped()) throw new InvalidOperationException("TIN is not bootstrapped");

        if (_searchHandle < 0)
        {
            // Seed the search from the first available edge. Use the streaming
            // iterator rather than GetEdges(): the latter materialises a full
            // List of every edge just to read element 0 — and rasterization
            // creates one navigator per worker thread, multiplying that
            // transient by the core count.
            var first = (QuadEdge?)_tin.GetEdgeIterator().FirstOrDefault()
                        ?? throw new InvalidOperationException("No edges available in TIN");
            _store = first.GetStore();
            _searchHandle = first.GetHandle();
        }

        _searchHandle = _walker.FindAnEdgeFromEnclosingTriangle(_store!, _searchHandle, x, y);
        return _searchHandle;
    }

    /// <summary>
    ///     Gets the edge store used by this navigator. Valid after the first
    ///     point-location query.
    /// </summary>
    internal EdgeStore? GetStore()
    {
        return _store;
    }

    /// <summary>
    ///     Resets the navigator state for changes to the TIN.
    /// </summary>
    public void ResetForChangeToTin()
    {
        _searchHandle = EdgeStore.NullHandle;
        _store = null;
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
        var h = GetNeighborEdgeHandle(x, y);
        var s = _store!;

        // Check the three vertices of the containing triangle
        IVertex? nearest = null;
        var minDist = double.PositiveInfinity;

        var dax = s.Ax(h) - x;
        var day = s.Ay(h) - y;
        var dA = dax * dax + day * day;
        if (dA < minDist)
        {
            minDist = dA;
            nearest = s.VertexA(h);
        }

        var dbx = s.Ax(h ^ 1) - x;
        var dby = s.Ay(h ^ 1) - y;
        var dB = dbx * dbx + dby * dby;
        if (dB < minDist)
        {
            minDist = dB;
            nearest = s.VertexA(h ^ 1);
        }

        var c = s.Forward(h) ^ 1;
        var dcx = s.Ax(c) - x;
        var dcy = s.Ay(c) - y;
        var dC = dcx * dcx + dcy * dcy;
        if (dC < minDist)
        {
            nearest = s.VertexA(c);
        }

        return nearest;
    }
}
