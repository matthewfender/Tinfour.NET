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
 * 11/2017  G. Lucas    Refactored for constrained regions
 * 08/2025 M.Fender    Ported to C#
 * -----------------------------------------------------------------------
 */

using static Tinfour.Core.Edge.QuadEdgeConstants;

namespace Tinfour.Core.Edge;

using System.Runtime.CompilerServices;

using Tinfour.Core.Common;

/// <summary>
///     A specialization of the QuadEdge class that adds support for
///     constraint functionality and maintains all constraint-related flags and indices.
/// </summary>
public class QuadEdgePartner : QuadEdge
{
    /// <summary>
    ///     Constructs a QuadEdgePartner as the dual of the specified QuadEdge.
    /// </summary>
    /// <param name="primary">The primary QuadEdge for which this instance will be the dual.</param>
    public QuadEdgePartner(QuadEdge primary)
    {
        // The partner's _index field is used purely for constraint storage
        // The sequential index is derived from the dual's index via GetIndex()
        _index = 0;
        _dual = primary;
    }

    /// <summary>
    ///     Clears the edge, resetting it to an uninitialized state.
    /// </summary>
    public override void Clear()
    {
        _v = Vertex.Null;
        _f = null;
        _r = null;
        _dual._v = Vertex.Null;
        _dual._f = null;
        _dual._r = null;

        _index = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetBaseIndex()
    {
        return _dual._index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override IQuadEdge GetBaseReference()
    {
        return _dual;
    }

    /// <summary>
    ///     Gets the index of the region-based constraint associated with an edge
    ///     that serves as part of the polygon bounding that region.
    ///     IMPORTANT: This overrides the base implementation to provide direct constraint storage.
    /// </summary>
    /// <returns>A positive integer or -1 if no constraint is specified.</returns>
    public override int GetConstraintBorderIndex()
    {
        if ((_index & ConstraintRegionBorderFlag) == 0)
        {
            return -1;
        }

        // Border index is stored in lower bits (same as interior, they're mutually exclusive)
        return QuadEdgeConstants.ExtractLowerIndex(_index);
    }

    /// <summary>
    ///     Gets the index of the constraint associated with this edge.
    ///     IMPORTANT: This overrides the base implementation to provide direct constraint storage
    ///     in the QuadEdgePartner, breaking recursion and following the Java pattern.
    /// </summary>
    /// <returns>A positive value; may be zero if not specified.</returns>
    public override int GetConstraintIndex()
    {
        if (_index < 0)
        {
            var c = QuadEdgeConstants.ExtractLowerIndex(_index);
            if (c >= 0) return c;
        }

        return 0;
    }

    /// <summary>
    ///     Gets the index of a line-based constraint associated with an edge.
    ///     IMPORTANT: This overrides the base implementation to provide direct constraint storage.
    /// </summary>
    /// <returns>A positive integer or -1 is no constraint index is available.</returns>
    public override int GetConstraintLineIndex()
    {
        if ((_index & ConstraintLineMemberFlag) != 0)
        {
            return QuadEdgeConstants.ExtractUpperIndex(_index);
        }

        return -1;
    }

    /// <summary>
    ///     Gets the index of the region-based constraint associated with an
    ///     edge contained in the interior of a constraint polygon.
    ///     IMPORTANT: This overrides the base implementation to provide direct constraint storage.
    /// </summary>
    /// <returns>A positive integer or -1 if no constraint is specified.</returns>
    public override int GetConstraintRegionInteriorIndex()
    {
        if ((_index & ConstraintRegionInteriorFlag) != 0)
        {
            return QuadEdgeConstants.ExtractLowerIndex(_index);
        }

        return -1;
    }

    /// <summary>
    ///     Gets the index of this edge. For partner edges, the index is
    ///     computed as the base edge's index plus one. The partner's own
    ///     _index field is used purely for constraint storage.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetIndex()
    {
        return _dual._index + 1;
    }

    public override int GetSide()
    {
        return 1;
    }

    /// <summary>
    ///     Indicates whether an edge is constrained.
    ///     IMPORTANT: This overrides the base implementation to provide direct constraint storage.
    /// </summary>
    /// <returns>True if the edge is constrained; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool IsConstrained()
    {
        return _index < 0;
    }

    /// <summary>
    ///     Indicates whether the edge is a member of a constraint line.
    ///     IMPORTANT: This overrides the base implementation to provide direct constraint storage.
    /// </summary>
    /// <returns>True if the edge is a member of an region; otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool IsConstraintLineMember()
    {
        return (_index & ConstraintLineMemberFlag) != 0;
    }

    /// <summary>
    ///     Indicates whether an edge represents the border of a constrained region.
    ///     IMPORTANT: This overrides the base implementation to provide direct constraint storage.
    /// </summary>
    /// <returns>
    ///     True if the edge is the border of the constrained region;
    ///     otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool IsConstraintRegionBorder()
    {
        return (_index & ConstraintRegionBorderFlag) != 0;
    }

    /// <summary>
    ///     Indicates whether the edge is in the interior of a constrained region.
    ///     IMPORTANT: This overrides the base implementation to provide direct constraint storage.
    /// </summary>
    /// <returns>True if the edge is in the interior of an region; otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool IsConstraintRegionInterior()
    {
        return (_index & ConstraintRegionInteriorFlag) != 0;
    }

    /// <summary>
    ///     Indicates whether the edge is a member of a constrained region.
    ///     IMPORTANT: This overrides the base implementation to provide direct constraint storage.
    /// </summary>
    /// <returns>True if the edge is a member of an region; otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool IsConstraintRegionMember()
    {
        return (_index & ConstraintRegionMemberFlags) != 0;
    }

    /// <summary>
    ///     Indicates whether the synthetic flag is set for the edge.
    ///     IMPORTANT: This overrides the base implementation to provide direct constraint storage.
    /// </summary>
    /// <returns>True if the edge is synthetic; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool IsSynthetic()
    {
        return (_index & SyntheticEdgeFlag) != 0;
    }

    /// <summary>
    ///     Sets a flag identifying the edge as the border of a region-based constraint
    ///     and stores the index for that constraint.
    ///     IMPORTANT: This overrides the base implementation to provide direct constraint storage.
    /// </summary>
    /// <param name="constraintIndex">A positive integer in the range zero to 32766, or -1 for a null constraint.</param>
    public override void SetConstraintBorderIndex(int constraintIndex)
    {
        // Border index is stored in lower bits (same as interior - they're mutually exclusive)
        // This allows up to 32,766 polygon constraints
        if (constraintIndex < -1 || constraintIndex > ConstraintLowerIndexValueMax)
            throw new ArgumentOutOfRangeException(
                nameof(constraintIndex),
                $"Border constraint index out of range [0..{ConstraintLowerIndexValueMax}]");

        // Preserve line constraint info in upper bits, clear lower bits and set border
        _index = (_index & ConstraintUpperIndexMask) | ConstraintEdgeFlag | ConstraintRegionBorderFlag
                      | QuadEdgeConstants.PackLowerIndex(constraintIndex);
    }

    /// <summary>
    ///     Sets the constraint index for this edge (stored in lower bits for region/polygon constraints).
    ///     IMPORTANT: This overrides the base implementation to provide direct constraint storage.
    /// </summary>
    /// <param name="constraintIndex">
    ///     A positive number indicating which constraint
    ///     a particular edge is associated with (0 to 32766).
    /// </param>
    public override void SetConstraintIndex(int constraintIndex)
    {
        if (constraintIndex < 0)
        {
            _index &= ~ConstraintLowerIndexMask;
            _index |= ConstraintEdgeFlag;
            return;
        }

        if (constraintIndex > ConstraintLowerIndexValueMax)
            throw new ArgumentOutOfRangeException(
                nameof(constraintIndex),
                $"Constraint index out of range [0..{ConstraintLowerIndexValueMax}]");

        _index = (_index & ~ConstraintLowerIndexMask) | QuadEdgeConstants.PackLowerIndex(constraintIndex) | ConstraintEdgeFlag;
    }

    /// <summary>
    ///     Sets a flag identifying the edge as a member of a line-based constraint
    ///     and stores the index for that constraint.
    ///     IMPORTANT: This overrides the base implementation to provide direct constraint storage.
    /// </summary>
    /// <param name="constraintIndex">A positive integer in range zero to 4094</param>
    public override void SetConstraintLineIndex(int constraintIndex)
    {
        if (constraintIndex < 0)
        {
            _index &= ~ConstraintUpperIndexMask;
            _index &= ~ConstraintLineMemberFlag;
            return;
        }

        if (constraintIndex > ConstraintUpperIndexValueMax)
            throw new ArgumentOutOfRangeException(
                nameof(constraintIndex),
                $"Line constraint index out of range [0..{ConstraintUpperIndexValueMax}]");

        _index = (_index & ~ConstraintUpperIndexMask) | QuadEdgeConstants.PackUpperIndex(constraintIndex)
                      | ConstraintLineMemberFlag | ConstraintEdgeFlag;
    }

    /// <summary>
    ///     Sets the constraint-line member flag for the edge to true.
    ///     IMPORTANT: This overrides the base implementation to provide direct constraint storage.
    /// </summary>
    public override void SetConstraintLineMemberFlag()
    {
        _index |= ConstraintLineMemberFlag;
    }

    /// <summary>
    ///     Sets a flag indicating that the edge is an edge of a constrained region.
    ///     IMPORTANT: This overrides the base implementation to provide direct constraint storage.
    /// </summary>
    public override void SetConstraintRegionBorderFlag()
    {
        _index |= ConstraintRegionBorderFlag;
        _index |= ConstraintEdgeFlag;
    }

    /// <summary>
    ///     Sets a flag identifying the edge as an interior member of a region-based
    ///     constraint and stores the index for that constraint.
    ///     IMPORTANT: This overrides the base implementation to provide direct constraint storage.
    /// </summary>
    /// <param name="constraintIndex">A positive integer in the range 0 to 32766, or -1 for a null value</param>
    public override void SetConstraintRegionInteriorIndex(int constraintIndex)
    {
        if (constraintIndex < 0)
        {
            _index &= ~ConstraintLowerIndexMask;
            _index &= ~ConstraintRegionInteriorFlag;
            return;
        }

        // Border edges take precedence - don't overwrite them with interior status
        if (IsConstraintRegionBorder())
        {
            return;
        }

        if (constraintIndex > ConstraintLowerIndexValueMax)
            throw new ArgumentOutOfRangeException(
                nameof(constraintIndex),
                $"Constraint index out of range [0..{ConstraintLowerIndexValueMax}]");

        _index = (_index & ~ConstraintLowerIndexMask) | QuadEdgeConstants.PackLowerIndex(constraintIndex) | ConstraintRegionInteriorFlag;
    }

    /// <summary>
    ///     Sets the index value for this edge.
    /// </summary>
    /// <param name="index">The index value to assign</param>
    public override void SetIndex(int index)
    {
        _dual._index = index;

        // _index = index + 1;
    }

    /// <summary>
    ///     Sets the synthetic flag for the edge.
    ///     IMPORTANT: This overrides the base implementation to provide direct constraint storage.
    /// </summary>
    /// <param name="status">True if the edge is synthetic; otherwise, false.</param>
    public override void SetSynthetic(bool status)
    {
        if (status) _index |= SyntheticEdgeFlag;
        else _index &= ~SyntheticEdgeFlag;
    }

    public override void SetVertices(IVertex a, IVertex b)
    {
        _v = a;
        _dual._v = b;
    }

    /// <summary>
    ///     Clears the constraint region flags (border and interior) from this edge.
    ///     This is used when an edge is flipped and its constraint status becomes stale.
    ///     Preserves the ConstraintEdgeFlag (if edge is constrained) and ConstraintLineMemberFlag.
    /// </summary>
    public override void ClearConstraintRegionFlags()
    {
        // Clear both border and interior flags, but preserve other flags and constraint indices
        // Note: We clear the lower index bits too since they store region constraint index
        _index &= ~(ConstraintRegionBorderFlag | ConstraintRegionInteriorFlag | ConstraintLowerIndexMask);
    }
}