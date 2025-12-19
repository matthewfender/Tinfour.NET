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
    ///     The number of bits committed to the storage of the lower (region/polygon)
    ///     constraint index. Region constraints are more common in typical use cases,
    ///     so they get more bits (15) allowing up to 32,766 polygon constraints.
    /// </summary>
    public const int ConstraintLowerIndexBitSize = 15;

    /// <summary>
    ///     The number of bits committed to the storage of the upper (line)
    ///     constraint index. Line constraints are less common, so they get
    ///     fewer bits (12) allowing up to 4,094 line constraints.
    /// </summary>
    public const int ConstraintUpperIndexBitSize = 12;

    /// <summary>
    ///     The number of bits committed to the storage of a constraint index.
    ///     This is the lower (region/polygon) index bit size for backward compatibility.
    /// </summary>
    public const int ConstraintIndexBitSize = ConstraintLowerIndexBitSize;

    /// <summary>
    ///     The maximum value of a region/polygon constraint index based on the 15 bits
    ///     allocated for its storage. This would be a value of 32,767, or 2^15-1.
    ///     But QuadEdge reserves the value 0 to represent a null specification.
    ///     For valid constraint indices, the QuadEdge implementation stores the
    ///     constraint value plus one. That makes the maximum value 2^15-2 = 32,766.
    /// </summary>
    public const int ConstraintLowerIndexValueMax = (1 << ConstraintLowerIndexBitSize) - 2;

    /// <summary>
    ///     The maximum value of a line constraint index based on the 12 bits
    ///     allocated for its storage. This would be a value of 4,095, or 2^12-1.
    ///     But QuadEdge reserves the value 0 to represent a null specification.
    ///     That makes the maximum value 2^12-2 = 4,094.
    /// </summary>
    public const int ConstraintUpperIndexValueMax = (1 << ConstraintUpperIndexBitSize) - 2;

    /// <summary>
    ///     The maximum value of a constraint index. This is the lower (region/polygon)
    ///     index maximum for backward compatibility, supporting up to 32,766 polygon constraints.
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
    ///     bits that contain a constraint index (15 bits, 0-14).
    /// </summary>
    public const int ConstraintLowerIndexMask = (1 << ConstraintLowerIndexBitSize) - 1;  // 0x7FFF

    /// <summary>
    ///     A specification for using an AND operation to zero out the lower field of
    ///     bits that contain a constraint index. Used in preparation for storing a
    ///     new value.
    /// </summary>
    public const int ConstraintLowerIndexZero = unchecked((int)(0xffffffff << ConstraintLowerIndexBitSize));

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
    ///     bits that contain a constraint index (12 bits, 15-26).
    /// </summary>
    public const int ConstraintUpperIndexMask = ((1 << ConstraintUpperIndexBitSize) - 1) << ConstraintLowerIndexBitSize;

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

    #region Helper Methods

    /// <summary>
    ///     Extracts the lower (region/polygon) constraint index from the packed index value.
    /// </summary>
    /// <param name="index">The packed index containing flags and constraint indices.</param>
    /// <returns>The constraint index (0-based), or -1 if not set.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int ExtractLowerIndex(int index)
    {
        var c = index & ConstraintLowerIndexMask;
        return c != 0 ? c - 1 : -1;
    }

    /// <summary>
    ///     Extracts the upper (line) constraint index from the packed index value.
    /// </summary>
    /// <param name="index">The packed index containing flags and constraint indices.</param>
    /// <returns>The constraint index (0-based), or -1 if not set.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int ExtractUpperIndex(int index)
    {
        var c = (index & ConstraintUpperIndexMask) >> ConstraintLowerIndexBitSize;
        return c != 0 ? c - 1 : -1;
    }

    /// <summary>
    ///     Packs a constraint index into the lower field position (for region/polygon constraints).
    /// </summary>
    /// <param name="constraintIndex">The 0-based constraint index.</param>
    /// <returns>The value to OR into the packed index.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int PackLowerIndex(int constraintIndex)
    {
        return constraintIndex + 1;
    }

    /// <summary>
    ///     Packs a constraint index into the upper field position (for line constraints).
    /// </summary>
    /// <param name="constraintIndex">The 0-based constraint index.</param>
    /// <returns>The value to OR into the packed index.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int PackUpperIndex(int constraintIndex)
    {
        return (constraintIndex + 1) << ConstraintLowerIndexBitSize;
    }

    #endregion
}