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
 * Date     Name        Description
 * ------   ---------   -------------------------------------------------
 * 02/2014  G. Lucas    Created
 * 08/2025 M.Fender    Ported to C# with special null vertex handling
 *
 * Notes:
 * In Java, Vertex was a class and could be null. In C#, we use a struct
 * for memory efficiency but provide a special NullVertex constant to
 * represent null vertices (ghost vertices) in the triangulation.
 * ----------------------------------------------------------------------- */

namespace Tinfour.Core.Common;

using System.Runtime.CompilerServices;

/// <summary>
///     Represents a point in a connected network on a planar surface.
/// </summary>
/// <remarks>
///     TEMPORARY: Changed from struct to class to test reference equality issues.
///     Original comment: This struct is intentionally implemented with memory efficiency in mind.
///     Using a struct rather than a class eliminates the overhead of heap allocation,
///     and by carefully selecting data types, we minimize the memory footprint.
///     For compatibility with the Java version which used nullable vertices,
///     we provide a special NullVertex constant to represent ghost vertices.
/// </remarks>
public sealed class Vertex : IVertex
{
    /// <summary>
    ///     A bit flag indicating that the vertex is synthetic and was created
    ///     through some form of mesh processing rather than being supplied
    ///     as a data sample.
    /// </summary>
    public const int BitSynthetic = 0x01;

    /// <summary>
    ///     A bit flag indicating that the vertex is a member of a constraint edge.
    /// </summary>
    public const int BitConstraint = 0x02;

    /// <summary>
    ///     A bit flag indicating that the vertex is to be treated as "withheld" and
    ///     should not be processed as part of a Delaunay triangulation (TIN).
    ///     This flag is used in support of data filtering and similar operations.
    /// </summary>
    public const int BitWithheld = 0x04;

    /// <summary>
    ///     A special marker bit indicating this is the null vertex.
    /// </summary>
    private const int BitNull = 0x80;

    /// <summary>
    ///     A special constant representing a null vertex (equivalent to null in Java).
    ///     This is used for ghost vertices in the triangulation.
    /// </summary>
    private static readonly Vertex _nullVertex = new(double.NaN, double.NaN, double.NaN, -1, BitNull, 0, 0, 0);

    /// <summary>
    ///     Gets the null vertex constant (equivalent to null in Java).
    ///     Used for ghost vertices in the triangulation.
    /// </summary>
    public IVertex NullVertex => _nullVertex;

    /// <summary>
    ///     Gets the static null vertex constant.
    ///     This is the canonical null vertex instance used throughout the library.
    /// </summary>
    public static IVertex Null => _nullVertex;

    /// <summary>
    ///     Gets the null vertex constant (legacy name for compatibility).
    /// </summary>
    [Obsolete("Use Vertex.Null instead. This property will be removed in a future version.")]
    public static IVertex _NullVertex => _nullVertex;

    /// <summary>
    ///     The Cartesian X coordinate of the vertex (immutable).
    /// </summary>
    public double X { get; }

    /// <summary>
    ///     The Cartesian Y coordinate of the vertex (immutable).
    /// </summary>
    public double Y { get; }

    /// <summary>
    ///     The Z coordinate of the vertex (immutable); treated as a dependent
    ///     variable of (X,Y).
    /// </summary>
    private float _z;

    /// <summary>
    ///     An indexing value assigned to the Vertex. Used primarily for
    ///     diagnostic purposes and labeling graphics.
    /// </summary>
    private int _index;

    /// <summary>
    ///     The bit-mapped status flags for the vertex. The assignment of meaning
    ///     to the bits for this field are defined by static members of this class.
    /// </summary>
    private byte _status;

    /// <summary>
    ///     An unused field reserved for use by applications and derived types
    /// </summary>
    private byte _reserved0;

    /// <summary>
    ///     An unused field reserved for use by applications and derived types
    /// </summary>
    private byte _reserved1;

    /// <summary>
    ///     The auxiliary index used for graph coloring algorithms
    ///     and other applications.
    /// </summary>
    private byte _auxiliary;

    /// <summary>
    ///     Constructs a vertex with the specified coordinates and z value.
    ///     If the z value is Double.NaN then the vertex
    ///     will be treated as a "null data value"
    /// </summary>
    /// <param name="x">The coordinate on the surface on which the vertex is defined</param>
    /// <param name="y">The coordinate on the surface on which the vertex is defined</param>
    /// <param name="z">The data value (z coordinate of the surface)</param>
    public Vertex(double x, double y, double z)
        : this(x, y, z, 0, 0, 0, 0, 0)
    {
    }

    /// <summary>
    ///     Constructs a vertex with the specified coordinates and ID value.
    ///     If the z value is Double.NaN then the vertex
    ///     will be treated as a "null data value".
    /// </summary>
    /// <param name="x">The coordinate on the surface on which the vertex is defined</param>
    /// <param name="y">The coordinate on the surface on which the vertex is defined</param>
    /// <param name="z">The data value (z coordinate of the surface)</param>
    /// <param name="index">The ID of the vertex (intended as a diagnostic)</param>
    public Vertex(double x, double y, double z, int index)
        : this(x, y, z, index, 0, 0, 0, 0)
    {
    }

    /// <summary>
    ///     Internal constructor that allows setting all fields at once.
    /// </summary>
    private Vertex(double x, double y, double z, int index, byte status, byte auxiliary, byte reserved0, byte reserved1)
    {
        X = x;
        Y = y;
        _z = (float)z;
        _index = index;
        _status = status;
        _auxiliary = auxiliary;
        _reserved0 = reserved0;
        _reserved1 = reserved1;
    }

    /// <summary>
    ///     Creates a vertex for deserialization with all fields set directly.
    ///     This method is internal to support TIN serialization.
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="z">Z value (stored as float)</param>
    /// <param name="index">Vertex index</param>
    /// <param name="status">Status flags (BitSynthetic, BitConstraint, BitWithheld)</param>
    /// <param name="auxiliary">Auxiliary index (0-255)</param>
    /// <returns>A new vertex with all fields set</returns>
    internal static Vertex CreateForDeserialization(
        double x, double y, float z, int index, byte status, byte auxiliary)
    {
        return new Vertex(x, y, z, index, status, auxiliary, 0, 0);
    }

    /// <summary>
    ///     Gets the raw Z value as stored (float).
    ///     This is internal to support TIN serialization.
    /// </summary>
    internal float GetZAsFloat() => _z;

    /// <summary>
    ///     Tests if this vertex contains the specified vertex.
    ///     For regular vertices, this tests for equality.
    /// </summary>
    /// <param name="vertex">The vertex to test</param>
    /// <returns>True if this vertex equals the specified vertex</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(Vertex vertex)
    {
        return Equals(vertex);
    }

    /// <summary>
    ///     Gets this vertex as a Vertex struct.
    /// </summary>
    /// <returns>This vertex as a Vertex struct</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vertex AsVertex()
    {
        return this;
    }

    /// <summary>
    ///     Checks if this vertex represents a null vertex (ghost vertex).
    /// </summary>
    /// <returns>True if this is the null vertex; otherwise, false</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNullVertex()
    {
        return IsNull();
    }

    /// <summary>
    ///     Gets a string intended for labeling the vertex in images or reports.
    ///     The default label is the index of the vertex preceeded by the letter S if the vertex is synthetic.
    ///     Note that the index of a vertex is not necessarily unique but left to the requirements of the application that
    ///     constructs it.
    /// </summary>
    /// <returns>A valid, non-empty string.</returns>
    public string GetLabel()
    {
        if (IsNullVertex()) return "null";
        return IsSynthetic() ? $"S{_index}" : $"{_index}";
    }

    /// <summary>
    ///     Get the X coordinate associated with the vertex.
    /// </summary>
    /// <returns>A valid floating point value.</returns>
    public double GetX()
    {
        return X;
    }

    /// <summary>
    ///     Get the Y coordinate associated with the vertex.
    /// </summary>
    /// <returns>A valid floating point value.</returns>
    public double GetY()
    {
        return Y;
    }

    /// <summary>
    ///     Get the Z value associated with the vertex.
    ///     If the vertex is null, the return value for this method is Double.NaN ("not a number").
    /// </summary>
    /// <returns>A floating point value or Double.NaN if z value is null.</returns>
    public double GetZ()
    {
        return _z;
    }

    /// <summary>
    ///     Indicates whether the vertex has been marked as having a null data value.
    /// </summary>
    /// <returns>True if vertex is marked as null; otherwise, false.</returns>
    public bool IsNull()
    {
        return (double.IsNaN(GetX()) || double.IsNaN(GetY()));
    }

    /// <summary>
    ///     Gets the arbitrary index associated with the vertex.
    /// </summary>
    /// <returns>An integer value.</returns>
    public int GetIndex()
    {
        return _index;
    }

    /// <summary>
    ///     Creates a new vertex with the specified index.
    /// </summary>
    /// <param name="index">An integer value.</param>
    /// <returns>A new vertex with the updated index.</returns>
    public Vertex WithIndex(int index)
    {
        return new Vertex(
            X,
            Y,
            _z,
            index,
            _status,
            _auxiliary,
            _reserved0,
            _reserved1);
    }

    /// <summary>
    ///     Indicates whether a vertex is synthetic (was created through
    ///     a Tinfour procedure rather than supplied by an application).
    /// </summary>
    /// <returns>True if vertex is synthetic; otherwise, false</returns>
    public bool IsSynthetic()
    {
        return (_status & BitSynthetic) != 0;
    }

    /// <summary>
    ///     Creates a new vertex with the specified synthetic status.
    /// </summary>
    /// <param name="synthetic">True if vertex is synthetic; otherwise, false</param>
    /// <returns>A new vertex with the updated synthetic status.</returns>
    public Vertex WithSynthetic(bool synthetic)
    {
        var status = _status;
        if (synthetic) status |= BitSynthetic;
        else status &= unchecked((byte)~BitSynthetic);
        return new Vertex(
            X,
            Y,
            _z,
            _index,
            status,
            _auxiliary,
            _reserved0,
            _reserved1);
    }

    /// <summary>
    ///     Creates a new vertex with the specified constraint member status.
    /// </summary>
    /// <param name="constraintMember">
    ///     True if vertex is a part of a constraint definition
    ///     or lies on the border of an area constraint; otherwise, false
    /// </param>
    /// <returns>A new vertex with the updated constraint member status.</returns>
    public Vertex WithConstraintMember(bool constraintMember)
    {
        var status = _status;
        if (constraintMember) status |= BitConstraint;
        else status &= unchecked((byte)~BitConstraint);
        return new Vertex(
            X,
            Y,
            _z,
            _index,
            status,
            _auxiliary,
            _reserved0,
            _reserved1);
    }

    /// <summary>
    ///     Indicates whether a vertex is marked as withheld.
    /// </summary>
    /// <returns>True if vertex is withheld; otherwise, false</returns>
    public bool IsWithheld()
    {
        return (_status & BitWithheld) != 0;
    }

    /// <summary>
    ///     Creates a new vertex with the specified withheld status.
    /// </summary>
    /// <param name="withheld">True if vertex is withheld; otherwise, false</param>
    /// <returns>A new vertex with the updated withheld status.</returns>
    public Vertex WithWithheld(bool withheld)
    {
        var status = _status;
        if (withheld) status |= BitWithheld;
        else status &= unchecked((byte)~BitWithheld);
        return new Vertex(
            X,
            Y,
            _z,
            _index,
            status,
            _auxiliary,
            _reserved0,
            _reserved1);
    }

    /// <summary>
    ///     Creates a new vertex with the specified status value.
    /// </summary>
    /// <param name="status">
    ///     A valid status value. Because the status is defined as
    ///     a single byte, higher-order bytes will be ignored.
    /// </param>
    /// <returns>A new vertex with the updated status.</returns>
    public Vertex WithStatus(int status)
    {
        return new Vertex(
            X,
            Y,
            _z,
            _index,
            (byte)status,
            _auxiliary,
            _reserved0,
            _reserved1);
    }

    /// <summary>
    ///     Gets the current value of the status flags for this vertex.
    /// </summary>
    /// <returns>A positive integer in the range 0 to 255.</returns>
    public int GetStatus()
    {
        return _status & 0xff;
    }

    /// <summary>
    ///     Indicates whether a vertex is part of a constraint definition or
    ///     lies on the border of an area constraint.
    /// </summary>
    /// <returns>True if vertex is a constraint member; otherwise, false</returns>
    public bool IsConstraintMember()
    {
        return (_status & BitConstraint) != 0;
    }

    /// <summary>
    ///     Gets the auxiliary index for the vertex.
    /// </summary>
    /// <returns>An integer value in the range 0 to 255</returns>
    public int GetAuxiliaryIndex()
    {
        return _auxiliary;
    }

    /// <summary>
    ///     Creates a new vertex with the specified auxiliary index.
    /// </summary>
    /// <param name="auxiliaryIndex">A value in the range 0 to 255</param>
    /// <returns>A new vertex with the updated auxiliary index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If the auxiliaryIndex is outside the valid range [0..255]</exception>
    public Vertex WithAuxiliaryIndex(int auxiliaryIndex)
    {
        if ((auxiliaryIndex & 0xffffff00) != 0)
            throw new ArgumentOutOfRangeException(
                nameof(auxiliaryIndex),
                "Auxiliary index out of valid range [0..255]");
        return new Vertex(
            X,
            Y,
            _z,
            _index,
            _status,
            (byte)(auxiliaryIndex & 0xff),
            _reserved0,
            _reserved1);
    }

    /// <summary>
    ///     Equality operator that properly handles null vertices and C# null references.
    /// </summary>
    public static bool operator ==(Vertex? left, Vertex? right)
    {
        // Handle C# null references first (needed since Vertex is now a class)
        var leftIsNull = ReferenceEquals(left, null);
        var rightIsNull = ReferenceEquals(right, null);

        if (leftIsNull && rightIsNull)
            return true;
        if (leftIsNull || rightIsNull)
            return false;

        // Now handle NullVertex (ghost vertex) semantics
        // If both are null vertices, they're equal
        if (left.IsNullVertex() && right.IsNullVertex())
            return true;

        // If one is null vertex and the other isn't, they're not equal
        if (left.IsNullVertex() || right.IsNullVertex())
            return false;

        // Both are real vertices, compare only the X and Y coords
        return left.X.Equals(right.X) && left.Y.Equals(right.Y);
    }

    /// <summary>
    ///     Inequality operator.
    /// </summary>
    public static bool operator !=(Vertex? left, Vertex? right)
    {
        return !(left == right);
    }

    /// <summary>
    ///     Override equals to work with our custom equality logic.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is Vertex other && this == other;
    }

    /// <summary>
    ///     Override GetHashCode to be consistent with equals.
    ///     Only uses X and Y since Equals only compares those coordinates.
    /// </summary>
    public override int GetHashCode()
    {
        if (IsNullVertex()) return int.MinValue; // Special hash for null vertex
        return HashCode.Combine(X, Y);
    }

    /// <summary>
    ///     Gets the square of the distance to the specified coordinates.
    /// </summary>
    /// <param name="x">X coordinate for distance calculation</param>
    /// <param name="y">Y coordinate for distance calculation</param>
    /// <returns>A positive floating-point value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDistanceSq(double x, double y)
    {
        if (IsNullVertex()) return double.PositiveInfinity;
        var dx = X - x;
        var dy = Y - y;
        return dx * dx + dy * dy;
    }

    /// <summary>
    ///     Gets the distance to the specified coordinates.
    /// </summary>
    /// <param name="x">X coordinate for distance calculation</param>
    /// <param name="y">Y coordinate for distance calculation</param>
    /// <returns>A positive floating-point value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDistance(double x, double y)
    {
        return Math.Sqrt(GetDistanceSq(x, y));
    }

    /// <summary>
    ///     Gets the distance to another vertex.
    /// </summary>
    /// <param name="v">A valid vertex</param>
    /// <returns>The distance to the vertex</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDistance(IVertex v)
    {
        return Math.Sqrt(GetDistanceSq(v.X, v.Y));
    }
}

/// <summary>
///     Extension methods for working with Vertex in contexts that expect nullable behavior.
/// </summary>
public static class VertexExtensions
{
    /// <summary>
    ///     Checks if a vertex is effectively null (either the NullVertex constant or has NaN coordinates).
    /// </summary>
    /// <param name="vertex">The vertex to check</param>
    /// <returns>True if the vertex represents null; otherwise, false</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEffectivelyNull(this IVertex vertex)
    {
        return vertex.IsNullVertex() || double.IsNaN(vertex.X) || double.IsNaN(vertex.Y);
    }

    /// <summary>
    ///     Returns the vertex if it's not null, otherwise returns the NullVertex constant.
    ///     This helps with porting Java code that checks for null.
    /// </summary>
    /// <param name="vertex">The vertex to check</param>
    /// <returns>The vertex or NullVertex</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IVertex OrNull(this IVertex vertex)
    {
        return vertex.IsEffectivelyNull() ? Vertex.Null : vertex;
    }
}