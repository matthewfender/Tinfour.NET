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
 * 07/2026  M. Fender   Converted to a flyweight over EdgeStore (#832)
 * -----------------------------------------------------------------------
 * Notes:
 * QuadEdge is a flyweight: all edge state (links, vertices, constraint
 * bits) lives in the owning EdgeStore's parallel arrays, addressed by this
 * instance's directed handle. Instances are canonical per handle (obtained
 * via EdgeStore.Wrap), so reference comparison of edges obtained from the
 * same TIN remains valid. The handle IS the public edge index: base edges
 * have even handles, their duals (QuadEdgePartner) odd handles.
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
    ///     The store holding this edge's state.
    /// </summary>
    private protected readonly EdgeStore Store;

    /// <summary>
    ///     The directed handle of this edge within the store.
    /// </summary>
    private protected readonly int Handle;

    internal QuadEdge(EdgeStore store, int handle)
    {
        Store = store;
        Handle = handle;
    }

    /// <summary>
    ///     Gets the directed handle of this edge (identical to its index).
    /// </summary>
    internal int GetHandle()
    {
        return Handle;
    }

    /// <summary>
    ///     Gets the store that owns this edge.
    /// </summary>
    internal EdgeStore GetStore()
    {
        return Store;
    }

    /// <summary>
    ///     Gets a token identifying this edge pair's current incarnation:
    ///     the base index combined with the pair's recycling generation.
    ///     Use this (not the bare index) to key bookkeeping that must survive
    ///     TIN mutations — a deallocated pair reuses its handle and canonical
    ///     wrappers, but never its token.
    /// </summary>
    internal long GetPairToken()
    {
        return ((long)Store.GenerationOf(Handle) << 32) | (uint)(Handle & ~1);
    }

    /// <summary>
    ///     Clears the edge pair, resetting it to an uninitialized state.
    /// </summary>
    public void Clear()
    {
        Store.ClearPairState(Handle >> 1);
    }

    /// <summary>
    ///     Gets the initial IVertex for this edge.
    /// </summary>
    /// <returns>A valid reference.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IVertex GetA()
    {
        return Store.VertexA(Handle);
    }

    /// <summary>
    ///     Gets the second IVertex for this edge.
    /// </summary>
    /// <returns>A valid reference or Vertex.Null for a ghost edge.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IVertex GetB()
    {
        return Store.VertexB(Handle);
    }

    /// <summary>
    ///     Gets the index of the "base" side of a bi-directional edge.
    /// </summary>
    /// <returns>A positive, even value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetBaseIndex()
    {
        return Handle & ~1;
    }

    /// <summary>
    ///     Gets the reference to the side-zero edge of the pair.
    /// </summary>
    /// <returns>A reference to the side-zero edge of the pair.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuadEdge GetBaseReference()
    {
        return Store.Wrap(Handle & ~1);
    }

    /// <summary>
    ///     Gets the index of the region-based constraint associated with an edge
    ///     that serves as part of the polygon bounding that region.
    /// </summary>
    /// <returns>A positive integer or -1 if no constraint is specified.</returns>
    public int GetConstraintBorderIndex()
    {
        var bits = Store.ConstraintBits(Handle);
        if ((bits & QuadEdgeConstants.ConstraintRegionBorderFlag) == 0) return -1;

        // Border index is stored in lower bits (same as interior, they're mutually exclusive)
        return QuadEdgeConstants.ExtractLowerIndex(bits);
    }

    /// <summary>
    ///     Gets the index of the constraint associated with this edge.
    /// </summary>
    /// <returns>A positive value; may be zero if not specified.</returns>
    public int GetConstraintIndex()
    {
        var bits = Store.ConstraintBits(Handle);
        if (bits < 0)
        {
            var c = QuadEdgeConstants.ExtractLowerIndex(bits);
            if (c >= 0) return c;
        }

        return 0;
    }

    /// <summary>
    ///     Gets the index of a line-based constraint associated with an edge.
    /// </summary>
    /// <returns>A positive integer or -1 is no constraint index is available.</returns>
    public int GetConstraintLineIndex()
    {
        var bits = Store.ConstraintBits(Handle);
        if ((bits & QuadEdgeConstants.ConstraintLineMemberFlag) != 0)
            return QuadEdgeConstants.ExtractUpperIndex(bits);

        return -1;
    }

    /// <summary>
    ///     Gets the index of the region-based constraint associated with an
    ///     edge contained in the interior of a constraint polygon.
    /// </summary>
    /// <returns>A positive integer or -1 if no constraint is specified.</returns>
    public int GetConstraintRegionInteriorIndex()
    {
        var bits = Store.ConstraintBits(Handle);
        if ((bits & QuadEdgeConstants.ConstraintRegionInteriorFlag) != 0)
            return QuadEdgeConstants.ExtractLowerIndex(bits);

        return -1;
    }

    /// <summary>
    ///     Gets the dual edge to this instance.
    /// </summary>
    /// <returns>A valid edge.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuadEdge GetDual()
    {
        return Store.Wrap(Handle ^ 1);
    }

    /// <summary>
    ///     Gets the dual of the reverse reference of the edge.
    /// </summary>
    /// <returns>A valid reference or null if the reverse reference is null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuadEdge? GetDualFromReverse()
    {
        var r = Store.Reverse(Handle);
        return r < 0 ? null : Store.Wrap(r ^ 1);
    }

    /// <summary>
    ///     Gets the forward reference of the edge.
    /// </summary>
    /// <returns>A valid reference.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuadEdge GetForward()
    {
        return Store.Wrap(Store.Forward(Handle));
    }

    /// <summary>
    ///     Gets the forward reference of the dual.
    /// </summary>
    /// <returns>A valid reference</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuadEdge GetForwardFromDual()
    {
        return Store.Wrap(Store.Forward(Handle ^ 1));
    }

    /// <summary>
    ///     Gets the index value for this edge. The index of an edge is
    ///     intrinsic: it is the directed handle of the edge within its store
    ///     (even for the base side, odd for the dual).
    /// </summary>
    /// <returns>A positive integer value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndex()
    {
        return Handle;
    }

    /// <summary>
    ///     Gets the length of the edge.
    /// </summary>
    /// <returns>A positive floating point value</returns>
    public double GetLength()
    {
        var a = GetA();
        var b = GetB();
        if (a.IsNullVertex() || b.IsNullVertex()) return double.PositiveInfinity;
        return a.GetDistance(b);
    }

    /// <summary>
    ///     Gets the squared length of the edge.
    ///     This is more efficient than GetLength() when comparing distances
    ///     since it avoids the square root computation.
    /// </summary>
    /// <returns>A positive floating point value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetLengthSquared()
    {
        var a = GetA();
        var b = GetB();
        if (a.IsNullVertex() || b.IsNullVertex()) return double.PositiveInfinity;
        return a.GetDistanceSq(b.X, b.Y);
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
        return Store.Wrap(Store.Reverse(Handle));
    }

    /// <summary>
    ///     Gets the reverse link of the dual.
    /// </summary>
    /// <returns>A valid reference</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IQuadEdge GetReverseFromDual()
    {
        return Store.Wrap(Store.Reverse(Handle ^ 1));
    }

    /// <summary>
    ///     Indicates which side of an edge a particular IQuadEdge instance is
    ///     attached to.
    /// </summary>
    /// <returns>A value of 0 or 1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetSide()
    {
        return Handle & 1;
    }

    /// <summary>
    ///     Indicates whether an edge is constrained.
    /// </summary>
    /// <returns>True if the edge is constrained; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConstrained()
    {
        return Store.ConstraintBits(Handle) < 0;
    }

    /// <summary>
    ///     Indicates whether the edge is a member of a constraint line.
    /// </summary>
    /// <returns>True if the edge is a member of an region; otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConstraintLineMember()
    {
        return (Store.ConstraintBits(Handle) & QuadEdgeConstants.ConstraintLineMemberFlag) != 0;
    }

    /// <summary>
    ///     Indicates whether an edge represents the border of a constrained region.
    /// </summary>
    /// <returns>
    ///     True if the edge is the border of the constrained region;
    ///     otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConstraintRegionBorder()
    {
        return (Store.ConstraintBits(Handle) & QuadEdgeConstants.ConstraintRegionBorderFlag) != 0;
    }

    /// <summary>
    ///     Indicates whether the edge is in the interior of a constrained region.
    /// </summary>
    /// <returns>True if the edge is in the interior of an region; otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConstraintRegionInterior()
    {
        return (Store.ConstraintBits(Handle) & QuadEdgeConstants.ConstraintRegionInteriorFlag) != 0;
    }

    /// <summary>
    ///     Indicates whether the edge is a member of a constrained region.
    /// </summary>
    /// <returns>True if the edge is a member of an region; otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConstraintRegionMember()
    {
        return (Store.ConstraintBits(Handle) & QuadEdgeConstants.ConstraintRegionMemberFlags) != 0;
    }

    /// <summary>
    ///     Indicates whether the synthetic flag is set for the edge.
    /// </summary>
    /// <returns>True if the edge is synthetic; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSynthetic()
    {
        return (Store.ConstraintBits(Handle) & QuadEdgeConstants.SyntheticEdgeFlag) != 0;
    }

    public void SetA(IVertex a)
    {
        Store.SetVertexA(Handle, a);
    }

    /// <summary>
    ///     Sets a flag identifying the edge as the border of a region-based constraint
    ///     and stores the index for that constraint.
    /// </summary>
    /// <param name="constraintIndex">A positive integer in the range zero to 32766, or -1 for a null constraint.</param>
    public void SetConstraintBorderIndex(int constraintIndex)
    {
        Store.SetConstraintBorderIndex(Handle, constraintIndex);
    }

    /// <summary>
    ///     Sets the constraint index for this edge (stored in lower bits for region/polygon constraints).
    /// </summary>
    /// <param name="constraintIndex">
    ///     A positive number indicating which constraint
    ///     a particular edge is associated with (0 to 32766).
    /// </param>
    public void SetConstraintIndex(int constraintIndex)
    {
        var bits = Store.ConstraintBits(Handle);
        if (constraintIndex < 0)
        {
            bits &= ~QuadEdgeConstants.ConstraintLowerIndexMask;
            bits |= QuadEdgeConstants.ConstraintEdgeFlag;
            Store.SetConstraintBits(Handle, bits);
            return;
        }

        if (constraintIndex > QuadEdgeConstants.ConstraintLowerIndexValueMax)
            throw new ArgumentOutOfRangeException(
                nameof(constraintIndex),
                $"Constraint index out of range [0..{QuadEdgeConstants.ConstraintLowerIndexValueMax}]");

        bits = (bits & ~QuadEdgeConstants.ConstraintLowerIndexMask)
               | QuadEdgeConstants.PackLowerIndex(constraintIndex)
               | QuadEdgeConstants.ConstraintEdgeFlag;
        Store.SetConstraintBits(Handle, bits);
    }

    /// <summary>
    ///     Sets a flag identifying the edge as a member of a line-based constraint
    ///     and stores the index for that constraint.
    /// </summary>
    /// <param name="constraintIndex">A positive integer in range zero to 4094</param>
    public void SetConstraintLineIndex(int constraintIndex)
    {
        var bits = Store.ConstraintBits(Handle);
        if (constraintIndex < 0)
        {
            bits &= ~QuadEdgeConstants.ConstraintUpperIndexMask;
            bits &= ~QuadEdgeConstants.ConstraintLineMemberFlag;
            Store.SetConstraintBits(Handle, bits);
            return;
        }

        if (constraintIndex > QuadEdgeConstants.ConstraintUpperIndexValueMax)
            throw new ArgumentOutOfRangeException(
                nameof(constraintIndex),
                $"Line constraint index out of range [0..{QuadEdgeConstants.ConstraintUpperIndexValueMax}]");

        bits = (bits & ~QuadEdgeConstants.ConstraintUpperIndexMask)
               | QuadEdgeConstants.PackUpperIndex(constraintIndex)
               | QuadEdgeConstants.ConstraintLineMemberFlag
               | QuadEdgeConstants.ConstraintEdgeFlag;
        Store.SetConstraintBits(Handle, bits);
    }

    /// <summary>
    ///     Sets the constraint-line member flag for the edge to true.
    /// </summary>
    public void SetConstraintLineMemberFlag()
    {
        Store.SetConstraintBits(Handle, Store.ConstraintBits(Handle) | QuadEdgeConstants.ConstraintLineMemberFlag);
    }

    /// <summary>
    ///     Sets a flag indicating that the edge is an edge of a constrained region.
    /// </summary>
    public void SetConstraintRegionBorderFlag()
    {
        Store.SetConstraintBits(
            Handle,
            Store.ConstraintBits(Handle)
            | QuadEdgeConstants.ConstraintRegionBorderFlag
            | QuadEdgeConstants.ConstraintEdgeFlag);
    }

    /// <summary>
    ///     Sets a flag identifying the edge as an interior member of a region-based
    ///     constraint and stores the index for that constraint.
    /// </summary>
    /// <param name="constraintIndex">A positive integer in the range 0 to 32766, or -1 for a null value</param>
    public void SetConstraintRegionInteriorIndex(int constraintIndex)
    {
        Store.SetConstraintRegionInteriorIndex(Handle, constraintIndex);
    }

    /// <summary>
    ///     Sets the forward reference for this edge and reciprocal reverse reference
    /// </summary>
    /// <param name="forward">A valid reference.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetForward(QuadEdge forward)
    {
        Store.SetForward(Handle, forward?.Handle ?? EdgeStore.NullHandle);
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
    ///     Sets the reverse reference for this edge and reciprocal forward reference
    /// </summary>
    /// <param name="reverse">A valid reference.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetReverse(QuadEdge reverse)
    {
        Store.SetReverse(Handle, reverse?.Handle ?? EdgeStore.NullHandle);
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
    /// </summary>
    /// <param name="status">True if the edge is synthetic; otherwise, false.</param>
    public void SetSynthetic(bool status)
    {
        var bits = Store.ConstraintBits(Handle);
        if (status) bits |= QuadEdgeConstants.SyntheticEdgeFlag;
        else bits &= ~QuadEdgeConstants.SyntheticEdgeFlag;
        Store.SetConstraintBits(Handle, bits);
    }

    /// <summary>
    ///     Sets the vertices for this edge (and its dual).
    /// </summary>
    /// <param name="a">The initial IVertex, must be a valid reference.</param>
    /// <param name="b">The second IVertex, may be a valid reference or Vertex.Null for a ghost edge.</param>
    public void SetVertices(IVertex a, IVertex b)
    {
        Store.SetVertices(Handle, a, b);
    }

    /// <summary>
    ///     Clears the constraint region flags (border and interior) from this edge.
    ///     This is used when an edge is flipped and its constraint status becomes stale.
    ///     Preserves the ConstraintEdgeFlag (if edge is constrained) and ConstraintLineMemberFlag.
    /// </summary>
    public void ClearConstraintRegionFlags()
    {
        // Clear both border and interior flags, but preserve other flags and constraint indices
        // Note: We clear the lower index bits too since they store region constraint index
        var bits = Store.ConstraintBits(Handle);
        bits &= ~(QuadEdgeConstants.ConstraintRegionBorderFlag
                  | QuadEdgeConstants.ConstraintRegionInteriorFlag
                  | QuadEdgeConstants.ConstraintLowerIndexMask);
        Store.SetConstraintBits(Handle, bits);
    }

    /// <summary>
    ///     Gets a string representation of this edge primarily for diagnostic purposes.
    /// </summary>
    /// <returns>A string with IVertex coordinates and edge index.</returns>
    public override string ToString()
    {
        var a = GetA();
        var b = GetB();

        if (a.IsNullVertex() && b.IsNullVertex()) return $"Edge(null -> null) [{GetIndex()}]";

        if (a.IsNullVertex()) return $"Edge(ghost -> {b.X:F1},{b.Y:F1}) [{GetIndex()}]";

        if (b.IsNullVertex()) return $"Edge({a.X:F1},{a.Y:F1} -> ghost) [{GetIndex()}]";

        return $"Edge({a.X:F1},{a.Y:F1} -> {b.X:F1},{b.Y:F1}) [{GetIndex()}]";
    }

    /// <summary>
    ///     Provides a convenience method for rendering edges by setting the
    ///     specified coordinates with the edge's endpoints.
    /// </summary>
    /// <param name="startPoint">Output parameter to receive the starting point of the edge.</param>
    /// <param name="endPoint">Output parameter to receive the ending point of the edge.</param>
    public void TranscribeTo(out Vector2 startPoint, out Vector2 endPoint)
    {
        var a = GetA();
        var b = GetB();

        startPoint = a.IsNullVertex() ? new Vector2(float.NaN, float.NaN) : new Vector2((float)a.X, (float)a.Y);
        endPoint = b.IsNullVertex() ? new Vector2(float.NaN, float.NaN) : new Vector2((float)b.X, (float)b.Y);
    }

    #region Serialization Support

    /// <summary>
    ///     Gets the forward link as a QuadEdge (for serialization).
    /// </summary>
    internal QuadEdge? GetForwardInternal() => Store.WrapOrNull(Store.Forward(Handle));

    /// <summary>
    ///     Gets the reverse link as a QuadEdge (for serialization).
    /// </summary>
    internal QuadEdge? GetReverseInternal() => Store.WrapOrNull(Store.Reverse(Handle));

    /// <summary>
    ///     Gets the dual/partner edge (for serialization).
    /// </summary>
    internal QuadEdge GetDualInternal() => Store.Wrap(Handle ^ 1);

    /// <summary>
    ///     Gets the packed constraint bits of the pair.
    /// </summary>
    internal int GetPartnerConstraintBits() => Store.ConstraintBits(Handle);

    /// <summary>
    ///     Sets the forward link directly without setting the reciprocal (for deserialization).
    /// </summary>
    internal void SetForwardDirect(QuadEdge? forward) =>
        Store.SetForwardDirect(Handle, forward?.Handle ?? EdgeStore.NullHandle);

    /// <summary>
    ///     Sets the reverse link directly without setting the reciprocal (for deserialization).
    /// </summary>
    internal void SetReverseDirect(QuadEdge? reverse) =>
        Store.SetReverseDirect(Handle, reverse?.Handle ?? EdgeStore.NullHandle);

    /// <summary>
    ///     Sets the packed constraint bits of the pair directly (for deserialization).
    /// </summary>
    internal void SetPartnerConstraintBits(int bits) => Store.SetConstraintBits(Handle, bits);

    /// <summary>
    ///     Sets the B vertex directly (for deserialization).
    /// </summary>
    internal void SetBDirect(IVertex b) => Store.SetVertexA(Handle ^ 1, b);

    #endregion
}
