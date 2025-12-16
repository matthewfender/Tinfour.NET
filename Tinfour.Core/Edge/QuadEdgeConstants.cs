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

namespace Tinfour.Core.Edge;

/// <summary>
///     Constants used by the QuadEdge implementation.
/// </summary>
internal static class QuadEdgeConstants
{
    /// <summary>
    ///     A bit indicating that an edge is constrained. This bit just happens
    ///     to be the sign bit, a feature that is exploited by the isConstrained()
    ///     method.
    /// </summary>
    public const int ConstraintEdgeFlag = 1 << 31;

    /// <summary>
    ///     A mask for preserving the bits allocated for edge-related flags.
    ///     At this time, there are definitions for 5 flags with one bit reserved
    ///     for future use.
    /// </summary>
    public const int ConstraintFlagMask = unchecked((int)0xf8000000);

    /// <summary>
    ///     The number of bits committed to the storage of a constraint index.
    ///     Tinfour reserves space to store the constraint index values for
    ///     the left and right side of a border constraint. Constraint indices
    ///     are stored in the "index" element of the QuadEdgePartner class.
    ///     The high order 5 bits are committed to various flags. So that
    ///     leaves 27 bits available for constraint information. Since storage is
    ///     required for two potential indices (left and right), thirteen bits
    ///     are available for each.
    /// </summary>
    public const int ConstraintIndexBitSize = 13;

    /// <summary>
    ///     The maximum value of a constraint index based on the 13 bits
    ///     allocated for its storage. This would be a value of 8191, or 2^13-1.
    ///     But QuadEdge reserves the value -1, bit state 0, to represent a null
    ///     specification. For valid constraint indices, the QuadEdge implementation
    ///     stores the constraint value plus one. That makes the maximum value 2^13-2
    /// </summary>
    public const int ConstraintIndexValueMax = (1 << ConstraintIndexBitSize) - 2;

    /// <summary>
    ///     A bit indicating that an edge is part of a non-region constraint line.
    ///     Edges are allowed to be both an interior and a line, so a separate flag bit
    ///     is required for both cases.
    /// </summary>
    public const int ConstraintLineMemberFlag = 1 << 28;

    /// <summary>
    ///     A specification for using an AND operation to extract the lower field of
    ///     bits that contain a constraint index.
    /// </summary>
    public const int ConstraintLowerIndexMask = ~ConstraintLowerIndexZero;

    /// <summary>
    ///     A specification for using an AND operation to zero out the lower field of
    ///     bits that contain a constraint index. Used in preparation for storing a
    ///     new value.
    /// </summary>
    public const int ConstraintLowerIndexZero = unchecked((int)(0xffffffff << ConstraintIndexBitSize));

    /// <summary>
    ///     A bit indicating that the edge is the border of a constrained region
    /// </summary>
    public const int ConstraintRegionBorderFlag = 1 << 30;

    /// <summary>
    ///     A bit indicating that an edge is in the interior of a constrained region.
    /// </summary>
    public const int ConstraintRegionInteriorFlag = 1 << 29;

    /// <summary>
    ///     A set of bits combining the constraint region interior and border flags.
    /// </summary>
    public const int ConstraintRegionMemberFlags = ConstraintRegionBorderFlag | ConstraintRegionInteriorFlag;

    /// <summary>
    ///     A specification for using an AND operation to extract the upper field of
    ///     bits that contain a constraint index.
    /// </summary>
    public const int ConstraintUpperIndexMask = ConstraintLowerIndexMask << ConstraintIndexBitSize;

    /// <summary>
    ///     A specification for using an AND operation to zero out the upper-field of
    ///     bits that contain a constraint index. Used in preparation for storing a
    ///     new value.
    /// </summary>
    public const int ConstraintUpperIndexZero = ~ConstraintUpperIndexMask;

    /// <summary>
    ///     Defines the bit that is not yet committed for representing edge status.
    ///     This value is equivalent to bit 26.
    /// </summary>
    public const int EdgeFlagReservedBit = 1 << 26;

    /// <summary>
    ///     A bit indicating that an edge has been marked as synthetic.
    /// </summary>
    public const int SyntheticEdgeFlag = 1 << 27;
}