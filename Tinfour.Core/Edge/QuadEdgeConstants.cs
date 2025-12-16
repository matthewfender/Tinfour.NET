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
    ///     At this time, there are definitions for 5 flags (bits 27-31).
    /// </summary>
    public const int ConstraintFlagMask = unchecked((int)0xf8000000);

    /// <summary>
    ///     The number of bits committed to the storage of the lower constraint index
    ///     (used for region/polygon constraints). The bit allocation is asymmetric:
    ///     15 bits for lower index (region constraints) and 12 bits for upper index
    ///     (line constraints), allowing up to 32,766 region constraints.
    /// </summary>
    public const int ConstraintLowerIndexBitSize = 15;

    /// <summary>
    ///     The number of bits committed to the storage of the upper constraint index
    ///     (used for line constraints). With 12 bits, supports up to 4,094 line constraints.
    /// </summary>
    public const int ConstraintUpperIndexBitSize = 12;

    /// <summary>
    ///     Legacy constant for backward compatibility. Equal to ConstraintLowerIndexBitSize.
    /// </summary>
    public const int ConstraintIndexBitSize = ConstraintLowerIndexBitSize;

    /// <summary>
    ///     The maximum value of a lower constraint index (region/polygon constraints)
    ///     based on the 15 bits allocated for its storage. This would be a value of
    ///     32767 (2^15-1), but QuadEdge reserves the value 0 (bit state 0) to represent
    ///     a null specification. For valid constraint indices, the QuadEdge implementation
    ///     stores the constraint value plus one. That makes the maximum value 2^15-2 = 32,766.
    /// </summary>
    public const int ConstraintLowerIndexValueMax = (1 << ConstraintLowerIndexBitSize) - 2;

    /// <summary>
    ///     The maximum value of an upper constraint index (line constraints)
    ///     based on the 12 bits allocated for its storage. This is 2^12-2 = 4,094.
    /// </summary>
    public const int ConstraintUpperIndexValueMax = (1 << ConstraintUpperIndexBitSize) - 2;

    /// <summary>
    ///     The maximum value of a constraint index. For general use, this is the
    ///     lower index maximum (32,766) which applies to region/polygon constraints.
    /// </summary>
    public const int ConstraintIndexValueMax = ConstraintLowerIndexValueMax;

    /// <summary>
    ///     A bit indicating that an edge is part of a non-region constraint line.
    ///     Edges are allowed to be both an interior and a line, so a separate flag bit
    ///     is required for both cases.
    /// </summary>
    public const int ConstraintLineMemberFlag = 1 << 28;

    /// <summary>
    ///     A specification for using an AND operation to extract the lower field of
    ///     bits that contain a constraint index (bits 0-14, 15 bits).
    /// </summary>
    public const int ConstraintLowerIndexMask = (1 << ConstraintLowerIndexBitSize) - 1;  // 0x7FFF

    /// <summary>
    ///     A specification for using an AND operation to zero out the lower field of
    ///     bits that contain a constraint index. Used in preparation for storing a
    ///     new value.
    /// </summary>
    public const int ConstraintLowerIndexZero = ~ConstraintLowerIndexMask;

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
    ///     bits that contain a constraint index (bits 15-26, 12 bits).
    /// </summary>
    public const int ConstraintUpperIndexMask = ((1 << ConstraintUpperIndexBitSize) - 1) << ConstraintLowerIndexBitSize;

    /// <summary>
    ///     A specification for using an AND operation to zero out the upper-field of
    ///     bits that contain a constraint index. Used in preparation for storing a
    ///     new value.
    /// </summary>
    public const int ConstraintUpperIndexZero = ~ConstraintUpperIndexMask;

    /// <summary>
    ///     A bit indicating that an edge has been marked as synthetic.
    /// </summary>
    public const int SyntheticEdgeFlag = 1 << 27;
}