/*
 * Copyright (C) 2018  Gary W. Lucas.
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
 * 12/2020  G. Lucas     Add extended precision for area computation
 * 08/2025 M.Fender     Ported to C#
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

using System.Runtime.CompilerServices;

/// <summary>
///     Provides methods and elements for a simple representation of a triangle based
///     on IQuadEdge edges.
/// </summary>
public class SimpleTriangle
{
    private readonly IQuadEdge _edgeA;

    private readonly IQuadEdge _edgeB;

    private readonly IQuadEdge _edgeC;

    private readonly int _index;

    private Circumcircle? _circumcircle;

    /// <summary>
    ///     Construct a simple triangle from the specified edges. For efficiency
    ///     purposes, this constructor is very lean and does not perform sanity
    ///     checking on the inputs.
    /// </summary>
    /// <param name="a">A valid edge</param>
    /// <param name="b">A valid edge</param>
    /// <param name="c">A valid edge</param>
    public SimpleTriangle(IQuadEdge a, IQuadEdge b, IQuadEdge c)
    {
        _edgeA = a;
        _edgeB = b;
        _edgeC = c;
        _index = ComputeIndex();
    }

    /// <summary>
    ///     Construct a simple triangle from the specified edge. The other two edges
    ///     are obtained from the forward and reverse links of the specified edge.
    ///     For efficiency purposes, this constructor is very lean and does not perform
    ///     sanity checking on the inputs.
    /// </summary>
    /// <param name="a">A valid edge.</param>
    public SimpleTriangle(IQuadEdge a)
    {
        _edgeA = a;
        _edgeB = a.GetForward();
        _edgeC = a.GetReverse();
        _index = ComputeIndex();
    }

    /// <summary>
    ///     Gets the area of the triangle. This value is positive if the triangle is
    ///     given in counterclockwise order and negative if it is given in clockwise
    ///     order. A value of zero indicates a degenerate triangle.
    /// </summary>
    /// <returns>A valid floating point number.</returns>
    public double GetArea()
    {
        var a = _edgeA.GetA();
        var b = _edgeB.GetA();
        var c = _edgeC.GetA();

        var ax = a.X;
        var ay = a.Y;
        var bx = b.X;
        var by = b.Y;
        var cx = c.X;
        var cy = c.Y;

        // Standard area computation for a triangle
        // area = ( (c.y - a.y) * (b.x - a.x) - (c.x - a.x) * (b.y - a.y) ) / 2;
        return ((cy - ay) * (bx - ax) - (cx - ax) * (by - ay)) / 2.0;
    }

    /// <summary>
    ///     Gets the centroid for the triangle. The centroid is computed as the
    ///     simple average of the x, y, and z coordinates for the vertices that
    ///     define the triangle.
    /// </summary>
    /// <returns>A valid instance of a Vertex.</returns>
    public IVertex GetCentroid()
    {
        var a = _edgeA.GetA();
        var b = _edgeB.GetA();
        var c = _edgeC.GetA();

        var x = (a.X + b.X + c.X) / 3.0;
        var y = (a.Y + b.Y + c.Y) / 3.0;
        var z = (a.GetZ() + b.GetZ() + c.GetZ()) / 3.0;

        return new Vertex(x, y, z, 0).WithSynthetic(true);
    }

    /// <summary>
    ///     Obtains the circumcircle for a simple triangle.
    /// </summary>
    /// <remarks>
    ///     This method uses an ordinary-precision computation for circumcircles
    ///     that yields acceptable accuracy for well-formed triangles. Applications
    ///     that need more accuracy or may need to deal with nearly degenerate
    ///     triangles (nearly flat triangles) may prefer to use more robust
    ///     geometric operations for that purpose.
    /// </remarks>
    /// <returns>A valid instance.</returns>
    public Circumcircle GetCircumcircle()
    {
        if (_circumcircle == null)
        {
            var a = _edgeA.GetA();
            var b = _edgeB.GetA();
            var c = _edgeC.GetA();
            _circumcircle = new Circumcircle();
            _circumcircle.Compute(a, b, c);
        }

        return _circumcircle;
    }

    /// <summary>
    ///     Get edge a from the triangle.
    /// </summary>
    /// <returns>A valid edge.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuadEdge GetEdgeA()
    {
        return _edgeA;
    }

    /// <summary>
    ///     Get edge b from the triangle.
    /// </summary>
    /// <returns>A valid edge.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuadEdge GetEdgeB()
    {
        return _edgeB;
    }

    /// <summary>
    ///     Get edge c from the triangle.
    /// </summary>
    /// <returns>A valid edge.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuadEdge GetEdgeC()
    {
        return _edgeC;
    }

    /// <summary>
    ///     Gets a unique index value associated with the triangle.
    /// </summary>
    /// <remarks>
    ///     The index value for the triangle is taken from the lowest-value
    ///     index of the three edges that comprise the triangle. It will be
    ///     stable provided that the underlying Triangulated Irregular Network (TIN)
    ///     is not modified.
    /// </remarks>
    /// <returns>An arbitrary integer value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndex()
    {
        return _index;
    }

    /// <summary>
    ///     Gets vertex A of the triangle. The method names used in this class follow
    ///     the conventions of trigonometry. Vertices are labeled so that vertex A is
    ///     opposite edge a, vertex B is opposite edge b, etc. This approach is
    ///     slightly different than that used in other parts of the Tinfour API.
    /// </summary>
    /// <returns>A valid vertex.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IVertex GetVertexA()
    {
        return _edgeC.GetA();
    }

    /// <summary>
    ///     Gets vertex B of the triangle. The method names used in this class follow
    ///     the conventions of trigonometry. Vertices are labeled so that vertex A is
    ///     opposite edge a, vertex B is opposite edge b, etc. This approach is
    ///     slightly different than that used in other parts of the Tinfour API.
    /// </summary>
    /// <returns>A valid vertex.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IVertex GetVertexB()
    {
        return _edgeA.GetA();
    }

    /// <summary>
    ///     Gets vertex C of the triangle. The method names used in this class follow
    ///     the conventions of trigonometry. Vertices are labeled so that vertex A is
    ///     opposite edge a, vertex B is opposite edge b, etc. This approach is
    ///     slightly different than that used in other parts of the Tinfour API.
    /// </summary>
    /// <returns>A valid vertex.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IVertex GetVertexC()
    {
        return _edgeB.GetA();
    }

    /// <summary>
    ///     Indicates whether the triangle is a ghost triangle. A ghost triangle
    ///     is one that lies outside the bounds of a Delaunay triangulation and
    ///     contains an undefined vertex.
    /// </summary>
    /// <remarks>
    ///     The TriangleCollector class does not produce ghost triangles, but
    ///     those created from perimeter edges may be ghosts.
    /// </remarks>
    /// <returns>True if the triangle is a ghost triangle; otherwise, false.</returns>
    public bool IsGhost()
    {
        var a = _edgeA.GetB();
        var b = _edgeB.GetB();
        var c = _edgeC.GetB();
        return a.IsNullVertex() || b.IsNullVertex() || c.IsNullVertex();
    }

    /// <summary>
    ///     Gets a string representation of this triangle primarily for diagnostic purposes.
    /// </summary>
    /// <returns>A string with triangle information including vertices and index.</returns>
    public override string ToString()
    {
        var a = GetVertexA();
        var b = GetVertexB();
        var c = GetVertexC();

        return $"Triangle[{_index}]: A({a.X:F2},{a.Y:F2}) B({b.X:F2},{b.Y:F2}) C({c.X:F2},{c.Y:F2})";
    }

    private int ComputeIndex()
    {
        var aIndex = _edgeA.GetIndex();
        var bIndex = _edgeB.GetIndex();
        var cIndex = _edgeC.GetIndex();

        if (aIndex <= bIndex) return aIndex < cIndex ? aIndex : cIndex;

        return bIndex < cIndex ? bIndex : cIndex;
    }
}