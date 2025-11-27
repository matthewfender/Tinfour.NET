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
 * 08/2018  G. Lucas     Initial implementation
 * 08/2025 M.Fender     Ported to C# for Tinfour.Core
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Voronoi;

using System.Drawing;

/// <summary>
///     Specifies options for building a bounded Voronoi Diagram.
/// </summary>
public class BoundedVoronoiBuildOptions
{
    /// <summary>
    ///     The bounds for the bounded Voronoi diagram.
    /// </summary>
    protected RectangleF? Bounds { get; set; }

    /// <summary>
    ///     Whether to enable automatic color assignment to vertices.
    /// </summary>
    protected bool EnableAutomaticColorAssignment { get; set; }

    /// <summary>
    ///     Gets the bounds specification, if any.
    /// </summary>
    /// <returns>The bounds rectangle or null if not specified.</returns>
    public RectangleF? GetBounds()
    {
        return Bounds;
    }

    /// <summary>
    ///     Gets whether automatic color assignment is enabled.
    /// </summary>
    /// <returns>True if enabled, otherwise false.</returns>
    public bool IsAutomaticColorAssignmentEnabled()
    {
        return EnableAutomaticColorAssignment;
    }

    /// <summary>
    ///     Enable the automatic assignment of color values to input vertices.
    /// </summary>
    /// <param name="status">True if automatic coloring is enabled; otherwise false.</param>
    public void SetAutomaticColorAssignment(bool status)
    {
        EnableAutomaticColorAssignment = status;
    }

    /// <summary>
    ///     Sets the bounds for the Bounded Voronoi Diagram. The domain of a true
    ///     Voronoi Diagram is the entire coordinate plane. For practical purposes
    ///     the bounded Voronoi Diagram class limits the bounds to a finite
    ///     domain. By default, the constructor will create bounds that are
    ///     slightly larger than the bounds of the input sample data set.
    ///     However, if an application has a specific need, it can specify
    ///     an alternate bounds.
    /// </summary>
    /// <remarks>
    ///     <strong>Note:</strong> The alternate bounds must be at least as large
    ///     as the size of the sample data set.
    /// </remarks>
    /// <param name="bounds">A valid rectangle.</param>
    public void SetBounds(RectangleF bounds)
    {
        Bounds = bounds;
    }
}