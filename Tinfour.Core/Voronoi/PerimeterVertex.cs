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
 * 09/2018  G. Lucas     Initial implementation
 * 08/2025 M.Fender     Ported to C# for Tinfour.Core
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Voronoi;

using Tinfour.Core.Common;

/// <summary>
///     Implements IVertex to add perimeter parameter functionality.
/// </summary>
internal class PerimeterVertex : IVertex
{
    /// <summary>
    ///     The underlying vertex data
    /// </summary>
    private readonly Vertex _vertex;

    /// <summary>
    ///     Construct a vertex at the specified horizontal Cartesian coordinates with
    ///     a z value indicating the parameterized position along the rectangular
    ///     bounds of the Voronoi Diagram. The parameter is a value in the
    ///     range 0 ? z &lt; 4.
    /// </summary>
    /// <param name="x">The x Cartesian coordinate of the vertex</param>
    /// <param name="y">The y Cartesian coordinate of the vertex</param>
    /// <param name="z">The parameterized position, in range 0 to 4.</param>
    /// <param name="index">An arbitrary index value</param>
    public PerimeterVertex(double x, double y, double z, int index)
    {
        _vertex = new Vertex(x, y, z, index);
        Z = z;
    }

    public IVertex NullVertex => _vertex.NullVertex;

    // IVertex implementation - forward to underlying vertex
    public double X => _vertex.X;

    public double Y => _vertex.Y;

    /// <summary>
    ///     The perimeter parameter
    /// </summary>
    public double Z { get; }

    public Vertex AsVertex()
    {
        return _vertex.AsVertex();
    }

    public bool Contains(Vertex vertex)
    {
        return _vertex.Contains(vertex);
    }

    public override bool Equals(object? obj)
    {
        if (obj is PerimeterVertex other) return X.Equals(other.X) && Y.Equals(other.Y);
        if (obj is IVertex otherVertex) return X.Equals(otherVertex.X) && Y.Equals(otherVertex.Y);
        return false;
    }

    public int GetAuxiliaryIndex()
    {
        return _vertex.GetAuxiliaryIndex();
    }

    public double GetDistance(IVertex v)
    {
        return _vertex.GetDistance(v);
    }

    public double GetDistance(double x, double y)
    {
        return _vertex.GetDistance(x, y);
    }

    public double GetDistanceSq(double x, double y)
    {
        return _vertex.GetDistanceSq(x, y);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public int GetIndex()
    {
        return _vertex.GetIndex();
    }

    public string GetLabel()
    {
        return _vertex.GetLabel();
    }

    public int GetStatus()
    {
        return _vertex.GetStatus();
    }

    public double GetX()
    {
        return _vertex.GetX();
    }

    public double GetY()
    {
        return _vertex.GetY();
    }

    public double GetZ()
    {
        return Z;

        // Override to return perimeter parameter
    }

    public bool IsConstraintMember()
    {
        return _vertex.IsConstraintMember();
    }

    public bool IsNullVertex()
    {
        return _vertex.IsNullVertex();
    }

    public bool IsSynthetic()
    {
        return _vertex.IsSynthetic();
    }

    public bool IsWithheld()
    {
        return _vertex.IsWithheld();
    }

    /// <summary>
    ///     Returns a string representation of this perimeter vertex.
    /// </summary>
    /// <returns>String representation</returns>
    public override string ToString()
    {
        if (IsSynthetic()) return $"Pv  {Z:F9}";

        // The rare case of a circumcenter lying on the perimeter
        return $"Pcc {Z:F9}, center {GetIndex()}";
    }

    public IVertex WithAuxiliaryIndex(int auxiliaryIndex)
    {
        var newVertex = _vertex.WithAuxiliaryIndex(auxiliaryIndex);
        return new PerimeterVertex(newVertex.X, newVertex.Y, Z, newVertex.GetIndex());
    }

    public IVertex WithConstraintMember(bool constraintMember)
    {
        var newVertex = _vertex.WithConstraintMember(constraintMember);
        return new PerimeterVertex(newVertex.X, newVertex.Y, Z, newVertex.GetIndex());
    }

    // IVertex modification methods - create new instances
    public IVertex WithIndex(int index)
    {
        return new PerimeterVertex(X, Y, Z, index);
    }

    public IVertex WithStatus(int status)
    {
        var newVertex = _vertex.WithStatus(status);
        return new PerimeterVertex(newVertex.X, newVertex.Y, Z, newVertex.GetIndex());
    }

    public IVertex WithSynthetic(bool synthetic)
    {
        var newVertex = _vertex.WithSynthetic(synthetic);
        return new PerimeterVertex(newVertex.X, newVertex.Y, Z, newVertex.GetIndex());
    }

    public IVertex WithWithheld(bool withheld)
    {
        var newVertex = _vertex.WithWithheld(withheld);
        return new PerimeterVertex(newVertex.X, newVertex.Y, Z, newVertex.GetIndex());
    }
}