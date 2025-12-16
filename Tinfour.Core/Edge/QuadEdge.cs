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
 * 07/2015  G. Lucas    Created
 * 12/2016  G. Lucas    Introduced support for constraints
 * 11/2017  G. Lucas    Added support for constraint regions
 * -----------------------------------------------------------------------
 *Notes:
 * The layout of this class is intended to accomplish the following:
 *  a) conserve memory in applications with a very large number of edges
 *  b) support the use of an edge pool by providing an application ID field.
 *
 * The original implementation prioritized memory efficiency, carefully
 * considering JVM object layout. In the C# port, we maintain a similar
 * structure while adapting to C# conventions and memory layout.
 */

namespace Tinfour.Core.Edge;

using System.Numerics;
using System.Runtime.CompilerServices;

using Tinfour.Core.Common;

/// <summary>
///     A representation of an edge with forward and reverse links on one
///     side and counterpart links attached to its dual (other side).
/// </summary>
/// <remarks>
///     <para>
///         This concept is based on the structure popularized by
///         Guibas, L. and Stolfi, J. (1985) "Primitives for the
///         manipulation of subdivisions and the computation of Voronoi diagrams"
///         ACM Transactions on Graphics, 4(2), 1985, p. 75-123.
///     </para>
/// </remarks>
public class QuadEdge : IQuadEdge
{
    /// <summary>
    ///     The dual of this edge (always valid after construction, never null in practice).
    /// </summary>
    /// <remarks>
    ///     Initialized to null! because the protected constructor is used by QuadEdgePartner
    ///     which sets _dual from its parent's constructor.
    /// </remarks>
    protected internal QuadEdge _dual = null!;

    /// <summary>
    ///     The forward link of this edge.
    /// </summary>
    protected internal QuadEdge? _f;

    /// <summary>
    ///     An arbitrary index value. For IncrementalTin, the index
    ///     is used to manage the edge pool.
    /// </summary>
    protected internal int _index;

    /// <summary>
    ///     The reverse link of this edge.
    /// </summary>
    protected internal QuadEdge? _r;

    /// <summary>
    ///     The initial IVertex of this edge, the second IVertex of
    ///     the dual. Can be null for ghost edges.
    /// </summary>
    protected internal IVertex _v = Vertex.Null;

    /// <summary>
    ///     Construct the edge and its dual assigning the pair the specified index.
    /// </summary>
    /// <param name="index">An arbitrary integer value.</param>
    public QuadEdge(int index)
    {
        _index = index;
        _dual = new QuadEdgePartner(this);
    }

    /// <summary>
    ///     Constructs the edge and its dual.
    /// </summary>
    /// <remarks>
    ///     This constructor is only called by QuadEdgePartner, which sets _dual from its parent.
    /// </remarks>
#pragma warning disable CS8618 // _dual is set by QuadEdgePartner constructor
    protected QuadEdge()
    {
    }
#pragma warning restore CS8618

    /// <summary>
    ///     Clears the edge, resetting it to an uninitialized state.
    /// </summary>
    public virtual void Clear()
    {
        _v = Vertex.Null;
        _f = null;
        _r = null;
        _dual._v = Vertex.Null;
        _dual._f = null;
        _dual._r = null;
        _dual._index = 0;
    }

    /// <summary>
    ///     Gets the initial IVertex for this edge.
    /// </summary>
    /// <returns>A valid reference.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IVertex GetA()
    {
        return _v;
    }

    /// <summary>
    ///     Gets the second IVertex for this edge.
    /// </summary>
    /// <returns>A valid reference or Vertex.Null for a ghost edge.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IVertex GetB()
    {
        return _dual._v;
    }

    /// <summary>
    ///     Gets the index of the "base" side of a bi-directional edge.
    /// </summary>
    /// <returns>A positive, even value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual int GetBaseIndex()
    {
        return _index;
    }

    /// <summary>
    ///     Gets the reference to the side-zero edge of the pair.
    /// </summary>
    /// <returns>A reference to the side-zero edge of the pair.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual IQuadEdge GetBaseReference()
    {
        return this;
    }

    /// <summary>
    ///     Gets the index of the region-based constraint associated with an edge
    ///     that serves as part of the polygon bounding that region.
    ///     Delegates to the dual partner which stores constraint information.
    /// </summary>
    /// <returns>A positive integer or -1 if no constraint is specified.</returns>
    public virtual int GetConstraintBorderIndex()
    {
        return _dual.GetConstraintBorderIndex();
    }

    // ---------- Constraint support (flags/indices) ----------
    // IMPORTANT: In the Java implementation, all constraint information is stored
    // in the QuadEdgePartner (dual), and the base QuadEdge delegates to its dual.
    // This preserves the base edge's geometric index while allowing the dual to
    // store constraint flags and indices.

    /// <summary>
    ///     Gets the index of the constraint associated with this edge.
    ///     Delegates to the dual partner which stores constraint information.
    /// </summary>
    /// <returns>A positive value; may be zero if not specified.</returns>
    public virtual int GetConstraintIndex()
    {
        return _dual.GetConstraintIndex();
    }

    /// <summary>
    ///     Gets the index of a line-based constraint associated with an edge.
    ///     Delegates to the dual partner which stores constraint information.
    /// </summary>
    /// <returns>A positive integer or -1 is no constraint index is available.</returns>
    public virtual int GetConstraintLineIndex()
    {
        return _dual.GetConstraintLineIndex();
    }

    /// <summary>
    ///     Gets the index of the region-based constraint associated with an
    ///     edge contained in the interior of a constraint polygon.
    ///     Delegates to the dual partner which stores constraint information.
    /// </summary>
    /// <returns>A positive integer or -1 if no constraint is specified.</returns>
    public virtual int GetConstraintRegionInteriorIndex()
    {
        return _dual.GetConstraintRegionInteriorIndex();
    }

    /// <summary>
    ///     Gets the dual edge to this instance.
    /// </summary>
    /// <returns>A valid edge.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuadEdge GetDual()
    {
        return _dual;
    }

    /// <summary>
    ///     Gets the dual of the reverse reference of the edge.
    /// </summary>
    /// <returns>A valid reference or null if the reverse reference is null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuadEdge? GetDualFromReverse()
    {
        return _r?._dual;
    }

    /// <summary>
    ///     Gets the forward reference of the edge.
    /// </summary>
    /// <returns>A valid reference.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuadEdge GetForward()
    {
        return _f!;
    }

    /// <summary>
    ///     Gets the forward reference of the dual.
    /// </summary>
    /// <returns>A valid reference</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuadEdge GetForwardFromDual()
    {
        return _dual._f!;
    }

    /// <summary>
    ///     Gets the index value for this edge.
    /// </summary>
    /// <returns>A positive integer value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual int GetIndex()
    {
        return _index;
    }

    /// <summary>
    ///     Gets the length of the edge.
    /// </summary>
    /// <returns>A positive floating point value</returns>
    public double GetLength()
    {
        var a = _v;
        var b = GetB();
        if (a.IsNullVertex() || b.IsNullVertex()) return double.PositiveInfinity;
        return a.GetDistance(b);
    }

    /// <summary>
    ///     Gets an instance of an iterable that performs a pinwheel operation.
    /// </summary>
    /// <returns>A valid enumerable collection.</returns>
    public IEnumerable<IQuadEdge> GetPinwheel()
    {
        return new PinwheelIterator(this);
    }

    /// <summary>
    ///     Gets the reverse reference of the edge.
    /// </summary>
    /// <returns>A valid reference.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuadEdge GetReverse()
    {
        return _r!;
    }

    /// <summary>
    ///     Gets the reverse link of the dual.
    /// </summary>
    /// <returns>A valid reference</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuadEdge GetReverseFromDual()
    {
        return _dual._r!;
    }

    /// <summary>
    ///     Indicates which side of an edge a particular IQuadEdge instance is
    ///     attached to.
    /// </summary>
    /// <returns>A value of 0 or 1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual int GetSide()
    {
        return 0;
    }

    /// <summary>
    ///     Indicates whether an edge is constrained.
    ///     Delegates to the dual partner which stores constraint information.
    /// </summary>
    /// <returns>True if the edge is constrained; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual bool IsConstrained()
    {
        return _dual.IsConstrained();
    }

    /// <summary>
    ///     Indicates whether the edge is a member of a constraint line.
    ///     Delegates to the dual partner which stores constraint information.
    /// </summary>
    /// <returns>True if the edge is a member of an region; otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual bool IsConstraintLineMember()
    {
        return _dual.IsConstraintLineMember();
    }

    /// <summary>
    ///     Indicates whether an edge represents the border of a constrained region.
    ///     Delegates to the dual partner which stores constraint information.
    /// </summary>
    /// <returns>
    ///     True if the edge is the border of the constrained region;
    ///     otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual bool IsConstraintRegionBorder()
    {
        return _dual.IsConstraintRegionBorder();
    }

    /// <summary>
    ///     Indicates whether the edge is in the interior of a constrained region.
    ///     Delegates to the dual partner which stores constraint information.
    /// </summary>
    /// <returns>True if the edge is in the interior of an region; otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual bool IsConstraintRegionInterior()
    {
        return _dual.IsConstraintRegionInterior();
    }

    /// <summary>
    ///     Indicates whether the edge is a member of a constrained region.
    ///     Delegates to the dual partner which stores constraint information.
    /// </summary>
    /// <returns>True if the edge is a member of an region; otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual bool IsConstraintRegionMember()
    {
        return _dual.IsConstraintRegionMember();
    }

    /// <summary>
    ///     Indicates whether the synthetic flag is set for the edge.
    ///     Delegates to the dual partner which stores constraint information.
    /// </summary>
    /// <returns>True if the edge is synthetic; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual bool IsSynthetic()
    {
        return _dual.IsSynthetic();
    }

    public void SetA(IVertex a)
    {
        _v = a;
    }

    /// <summary>
    ///     Sets a flag identifying the edge as the border of a region-based constraint
    ///     and stores the index for that constraint.
    ///     Delegates to the dual partner which stores constraint information.
    /// </summary>
    /// <param name="constraintIndex">A positive integer in the range zero to 32766, or -1 for a null constraint.</param>
    public virtual void SetConstraintBorderIndex(int constraintIndex)
    {
        _dual.SetConstraintBorderIndex(constraintIndex);
    }

    /// <summary>
    ///     Sets the constraint index for this edge.
    ///     Delegates to the dual partner which stores constraint information.
    /// </summary>
    /// <param name="constraintIndex">
    ///     A positive number indicating which constraint
    ///     a particular edge is associated with.
    /// </param>
    public virtual void SetConstraintIndex(int constraintIndex)
    {
        _dual.SetConstraintIndex(constraintIndex);
    }

    /// <summary>
    ///     Sets a flag identifying the edge as the border of a line-based constraint
    ///     and stores the index for that constraint.
    ///     Delegates to the dual partner which stores constraint information.
    /// </summary>
    /// <param name="constraintIndex">A positive integer in range zero to 4094</param>
    public virtual void SetConstraintLineIndex(int constraintIndex)
    {
        _dual.SetConstraintLineIndex(constraintIndex);
    }

    /// <summary>
    ///     Sets the constraint-line member flag for the edge to true.
    ///     Delegates to the dual partner which stores constraint information.
    /// </summary>
    public virtual void SetConstraintLineMemberFlag()
    {
        _dual.SetConstraintLineMemberFlag();
    }

    /// <summary>
    ///     Sets a flag indicating that the edge is an edge of a constrained region.
    ///     Delegates to the dual partner which stores constraint information.
    /// </summary>
    public virtual void SetConstraintRegionBorderFlag()
    {
        _dual.SetConstraintRegionBorderFlag();
    }

    /// <summary>
    ///     Sets a flag identifying the edge as an interior member of a region-based
    ///     constraint and stores the index for that constraint.
    ///     Delegates to the dual partner which stores constraint information.
    /// </summary>
    /// <param name="constraintIndex">A positive integer in the range 0 to 32766, or -1 for a null value</param>
    public virtual void SetConstraintRegionInteriorIndex(int constraintIndex)
    {
        _dual.SetConstraintRegionInteriorIndex(constraintIndex);
    }

    /// <summary>
    ///     Sets the forward reference for this edge and reciprocal reverse reference
    /// </summary>
    /// <param name="forward">A valid reference.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetForward(QuadEdge forward)
    {
        _f = forward;
        if (forward != null) forward._r = this;
    }

    /// <summary>
    ///     Sets the forward reference for this edge and reciprocal reverse reference.
    /// </summary>
    /// <param name="forward">A valid edge reference</param>
    public void SetForward(IQuadEdge forward)
    {
        SetForward((QuadEdge)forward);
    }

    /// <summary>
    ///     Sets the index value for this edge.
    /// </summary>
    /// <param name="index">The index value to assign</param>
    public virtual void SetIndex(int index)
    {
        _index = index;
    }

    /// <summary>
    ///     Sets the reverse reference for this edge and reciprocal forward reference
    /// </summary>
    /// <param name="reverse">A valid reference.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetReverse(QuadEdge reverse)
    {
        _r = reverse;
        if (reverse != null) reverse._f = this;
    }

    /// <summary>
    ///     Sets the reverse reference for this edge and reciprocal forward reference.
    /// </summary>
    /// <param name="reverse">A valid edge reference</param>
    public void SetReverse(IQuadEdge reverse)
    {
        SetReverse((QuadEdge)reverse);
    }

    /// <summary>
    ///     Sets the synthetic flag for the edge.
    ///     Delegates to the dual partner which stores constraint information.
    /// </summary>
    /// <param name="status">True if the edge is synthetic; otherwise, false.</param>
    public virtual void SetSynthetic(bool status)
    {
        _dual.SetSynthetic(status);
    }

    /// <summary>
    ///     Sets the vertices for this edge (and its dual).
    /// </summary>
    /// <param name="a">The initial IVertex, must be a valid reference.</param>
    /// <param name="b">The second IVertex, may be a valid reference or Vertex.Null for a ghost edge.</param>
    public virtual void SetVertices(IVertex a, IVertex b)
    {
        _v = a;
        _dual._v = b;
    }

    /// <summary>
    ///     Gets a string representation of this edge primarily for diagnostic purposes.
    /// </summary>
    /// <returns>A string with IVertex coordinates and edge index.</returns>
    public override string ToString()
    {
        var a = _v;
        var b = GetB();

        if (a.IsNullVertex() && b.IsNullVertex()) return $"Edge(null -> null) [{_index}]";

        if (a.IsNullVertex()) return $"Edge(ghost -> {b.X:F1},{b.Y:F1}) [{_index}]";

        if (b.IsNullVertex()) return $"Edge({a.X:F1},{a.Y:F1} -> ghost) [{_index}]";

        return $"Edge({a.X:F1},{a.Y:F1} -> {b.X:F1},{b.Y:F1}) [{_index}]";
    }

    /// <summary>
    ///     Provides a convenience method for rendering edges by setting the
    ///     specified coordinates with the edge's endpoints.
    /// </summary>
    /// <param name="startPoint">Output parameter to receive the starting point of the edge.</param>
    /// <param name="endPoint">Output parameter to receive the ending point of the edge.</param>
    public void TranscribeTo(out Vector2 startPoint, out Vector2 endPoint)
    {
        var a = _v;
        var b = GetB();

        startPoint = a.IsNullVertex() ? new Vector2(float.NaN, float.NaN) : new Vector2((float)a.X, (float)a.Y);
        endPoint = b.IsNullVertex() ? new Vector2(float.NaN, float.NaN) : new Vector2((float)b.X, (float)b.Y);
    }
}