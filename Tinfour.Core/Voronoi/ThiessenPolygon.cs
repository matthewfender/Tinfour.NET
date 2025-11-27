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
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Voronoi;

using System.Drawing;

using Tinfour.Core.Common;
using Tinfour.Core.Utils;

/// <summary>
///     Provides elements and methods for representing a Thiessen Polygon created by
///     the BoundedVoronoi class.
/// </summary>
public class ThiessenPolygon
{
    private readonly double _area;

    private readonly RectangleF _bounds;

    private readonly IQuadEdge[] _edges;

    private readonly bool _open;

    private readonly IVertex _vertex;

    /// <summary>
    ///     Constructs a Thiessen Polygon representation. The open flag is used to
    ///     indicate polygons of an infinite area that extend beyond the bounds of the
    ///     Delaunay Triangulation associated with the Voronoi Diagram
    /// </summary>
    /// <param name="vertex">The vertex at the center of the polygon</param>
    /// <param name="edgeList">A list of the edges comprising the polygon</param>
    /// <param name="open">Indicates whether the polygon is infinite (open) finite (closed).</param>
    public ThiessenPolygon(IVertex vertex, List<IQuadEdge> edgeList, bool open)
    {
        _vertex = vertex;
        _edges = edgeList.ToArray();
        _open = open;

        if (edgeList.Count > 0)
        {
            var v = edgeList[0].GetA();
            _bounds = new RectangleF((float)v.X, (float)v.Y, 0, 0);
            double s = 0;
            foreach (var e in edgeList)
            {
                var vertexA = e.GetA();
                var vertexB = e.GetB();
                s += vertexA.X * vertexB.Y - vertexA.Y * vertexB.X;
                _bounds = RectangleF.Union(_bounds, new RectangleF((float)vertexB.X, (float)vertexB.Y, 0, 0));
            }

            _area = s / 2;
        }
        else
        {
            // Empty polygon - use vertex location as bounds
            _bounds = new RectangleF((float)vertex.X, (float)vertex.Y, 0, 0);
            _area = 0;
        }
    }

    /// <summary>
    ///     Gets the area of the Voronoi polygon. If the polygon is an open polygon,
    ///     its actual area would be infinite, but the reported area matches
    ///     the domain of the Bounded Voronoi Diagram class.
    /// </summary>
    /// <returns>A valid, finite floating point value</returns>
    public double GetArea()
    {
        return _area;
    }

    /// <summary>
    ///     Gets the bounds of the polygon
    /// </summary>
    /// <returns>A safe copy of a rectangle instance.</returns>
    public RectangleF GetBounds()
    {
        return _bounds;
    }

    /// <summary>
    ///     Gets the edges that comprise the polygon
    /// </summary>
    /// <returns>A valid list of edges</returns>
    public List<IQuadEdge> GetEdges()
    {
        return new List<IQuadEdge>(_edges);
    }

    /// <summary>
    ///     Gets the index element of the defining vertex for this polygon.
    ///     The vertex index is under the control of the calling application
    ///     and is not modified by the Voronoi classes. Note that the
    ///     index of a vertex is not necessarily unique but left to the
    ///     requirements of the application that constructs it.
    /// </summary>
    /// <returns>An integer value</returns>
    public int GetIndex()
    {
        return _vertex.GetIndex();
    }

    /// <summary>
    ///     Gets the defining vertex of the polygon.
    /// </summary>
    /// <returns>The vertex</returns>
    public IVertex GetVertex()
    {
        return _vertex;
    }

    /// <summary>
    ///     Indicates that in a true Voronoi Diagram the polygon would
    ///     not form a closed polygon and would have an infinite domain.
    /// </summary>
    /// <returns>True if the polygon is open, otherwise false.</returns>
    public bool IsOpen()
    {
        return _open;
    }

    /// <summary>
    ///     Indicates whether the specified coordinate point lies inside or on an edge
    ///     of the polygon associated with this instance.
    /// </summary>
    /// <param name="x">The Cartesian x coordinate of the query point</param>
    /// <param name="y">The Cartesian y coordinate of the query point</param>
    /// <returns>True if the point is inside the polygon; otherwise, false</returns>
    public bool IsPointInPolygon(double x, double y)
    {
        var edgeList = new List<IQuadEdge>(_edges);
        var result = Polyside.IsPointInPolygon(edgeList, x, y);
        return result.IsCovered();
    }

    /// <summary>
    ///     Returns a string representation of this polygon.
    /// </summary>
    /// <returns>String representation</returns>
    public override string ToString()
    {
        return $"ThiessenPolygon vertex={_vertex.GetLabel()}";
    }
}