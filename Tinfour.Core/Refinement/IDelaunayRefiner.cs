/*
 * Copyright 2025 Gary W. Lucas.
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
 * 10/2025  M. Carleton  Created (Java)
 * 12/2025  M. Fender    Ported to C#
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Refinement;

using Tinfour.Core.Common;

/// <summary>
///     Defines an interface for Delaunay mesh refinement algorithms.
/// </summary>
/// <remarks>
///     <para>
///         A <see cref="IDelaunayRefiner"/> refines a given triangulation to improve its
///         quality, for example by increasing minimum angles or enforcing other
///         geometric properties.
///     </para>
///     <para>
///         Refinement is performed in-place and may mutate the underlying triangulation.
///         Implementations should provide constructors for initializing with a specific
///         triangulation and any quality criteria or thresholds.
///     </para>
///     <para>
///         The <see cref="Refine"/> method applies the refinement logic, such as inserting
///         Steiner points or retriangulating, to meet the desired properties.
///     </para>
/// </remarks>
public interface IDelaunayRefiner
{
    /// <summary>
    ///     Performs a single refinement operation on the associated triangulation.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Implementations should perform one atomic refinement step: identify an
    ///         element that violates the configured quality criteria (for example a "bad"
    ///         triangle) and repair it (for example by inserting a Steiner vertex,
    ///         performing local retriangulation or edge flips). This method mutates the
    ///         underlying triangulation in-place.
    ///     </para>
    ///     <para>
    ///         <strong>Implementation Note:</strong>
    ///         Implementations are encouraged to make this method behave
    ///         predictably when invoked repeatedly: calling it repeatedly on an
    ///         unchanged triangulation should either return the same inserted
    ///         vertex until the local repair completes, or return <c>null</c>
    ///         once no further single-step refinements are required.
    ///     </para>
    /// </remarks>
    /// <returns>
    ///     The <see cref="IVertex"/> that was inserted as part of this refinement step,
    ///     or <c>null</c> if no refinement was necessary (the triangulation
    ///     already meets the quality criteria or no applicable operation was available).
    /// </returns>
    /// <seealso cref="Refine"/>
    IVertex? RefineOnce();

    /// <summary>
    ///     Refines the associated triangulation to improve its quality according to the
    ///     implementation's criteria (e.g., increasing minimum angles, removing skinny
    ///     triangles, etc).
    /// </summary>
    /// <remarks>
    ///     This method typically runs a loop that performs repeated single-step
    ///     refinements (for example by calling <see cref="RefineOnce"/>) until the
    ///     triangulation meets the quality criteria or a termination condition is reached.
    /// </remarks>
    /// <returns>
    ///     <c>true</c> if the refinement process terminated successfully because the
    ///     triangulation meets the configured quality criteria; <c>false</c> if the process
    ///     terminated early due to a hard iteration cap or other imposed stopping condition
    ///     before the criteria were satisfied.
    /// </returns>
    bool Refine();
}
