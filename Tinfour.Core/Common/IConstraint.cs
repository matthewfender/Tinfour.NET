/*
 * Copyright 2015-2025 Gary W. Lucas.
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
 * 02/2013  G. Lucas     Initial implementation
 * 08/2025 M.Fender     Ported to C# for Tinfour.Core
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

/// <summary>
///     Defines constraint types for Constrained Delaunay Triangulation (CDT).
///     Constraints are used to enforce specific edges in the triangulation
///     that might otherwise be excluded by the Delaunay criterion.
/// </summary>
public interface IConstraint : IPolyline
{
    /// <summary>
    ///     Indicates whether the constraint applies a constrained region behavior
    ///     when added to a TIN.
    /// </summary>
    /// <returns>
    ///     True if the constraint is a data-region definition; otherwise
    ///     false.
    /// </returns>
    bool DefinesConstrainedRegion();

    /// <summary>
    ///     Gets the application data object that was associated with the constraint,
    ///     if any.
    /// </summary>
    /// <returns>A valid application data object, or null if none was assigned.</returns>
    object? GetApplicationData();

    /// <summary>
    ///     Gets an index value used for internal bookkeeping by Tinfour code.
    /// </summary>
    /// <returns>The index of the constraint; undefined if not set.</returns>
    int GetConstraintIndex();

    /// <summary>
    ///     Gets a reference to an arbitrarily selected edge that was produced
    ///     when the constraint was added to a TIN.
    /// </summary>
    /// <returns>A valid edge reference if available; otherwise null.</returns>
    IQuadEdge? GetConstraintLinkingEdge();

    /// <summary>
    ///     Gets a new constraint that has the attributes of this constraint
    ///     and the specified geometry.
    /// </summary>
    /// <param name="geometry">A valid set of vertices.</param>
    /// <returns>A new constraint.</returns>
    IConstraint GetConstraintWithNewGeometry(IList<IVertex> geometry);

    /// <summary>
    ///     Gets the default Z value to use for interpolation within this constraint.
    /// </summary>
    /// <returns>The default Z value, or null if not set.</returns>
    double? GetDefaultZ();

    /// <summary>
    ///     Gets the instance of the incremental TIN interface that
    ///     is managing this constraint, if any.
    /// </summary>
    /// <returns>If under management, a valid instance; otherwise, null.</returns>
    IIncrementalTin? GetManagingTin();

    /// <summary>
    ///     Indicates if a point at the specified coordinates is unambiguously
    ///     inside the constraint.
    /// </summary>
    /// <param name="x">The Cartesian coordinate for the point</param>
    /// <param name="y">The Cartesian coordinate for the point</param>
    /// <returns>True if the point is in the interior of the constraint.</returns>
    bool IsPointInsideConstraint(double x, double y);

    /// <summary>
    ///     Sets the application data object for the constraint.
    ///     This method is intended to allow applications to associate arbitrary
    ///     data objects with constraints for their own processing purposes.
    /// </summary>
    /// <param name="applicationData">An arbitrary object or null.</param>
    void SetApplicationData(object? applicationData);

    /// <summary>
    ///     Sets an index value used for internal bookkeeping by Tinfour code.
    ///     Not intended for use by application code.
    /// </summary>
    /// <param name="tin">
    ///     The IIncrementalTin instance to which this constraint has been
    ///     added (or null if not applicable).
    /// </param>
    /// <param name="index">A positive integer.</param>
    void SetConstraintIndex(IIncrementalTin? tin, int index);

    /// <summary>
    ///     Sets a reference to an arbitrarily selected edge that was produced
    ///     when the constraint was added to a TIN.
    /// </summary>
    /// <param name="linkingEdge">A valid edge reference</param>
    void SetConstraintLinkingEdge(IQuadEdge linkingEdge);

    /// <summary>
    ///     Sets the default Z value to use for interpolation within this constraint.
    /// </summary>
    /// <param name="defaultZ">The default Z value, or null to clear.</param>
    void SetDefaultZ(double? defaultZ);
}