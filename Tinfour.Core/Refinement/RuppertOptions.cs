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
 * 12/2025  M. Fender    Created for C# port
 *
 * Notes:
 *   Configuration options for Ruppert's Delaunay refinement algorithm.
 *   Provides a clean way to configure the refiner without complex
 *   constructor overloads.
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Refinement;

using Tinfour.Core.Interpolation;

/// <summary>
///     Configuration options for Ruppert's Delaunay refinement algorithm.
/// </summary>
/// <remarks>
///     <para>
///         This class provides a convenient way to configure the refinement
///         parameters without using complex constructor overloads.
///     </para>
///     <para>
///         The most important parameter is <see cref="MinimumAngleDegrees"/>,
///         which specifies the target minimum angle for triangles. The theoretical
///         maximum is approximately 33.8° (corresponding to ρ = √2), though in
///         practice values up to 30° work well. The default of 20° provides good
///         results for most applications.
///     </para>
/// </remarks>
public class RuppertOptions
{
    /// <summary>
    ///     Gets or sets the minimum angle threshold in degrees.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The refinement algorithm attempts to eliminate triangles with
    ///         angles smaller than this threshold by inserting Steiner points.
    ///     </para>
    ///     <para>
    ///         The theoretical maximum is approximately 33.8° (when ρ = √2).
    ///         In practice, values up to 30° work well. Higher values may cause
    ///         the algorithm to run indefinitely in some cases.
    ///     </para>
    /// </remarks>
    /// <value>The minimum angle in degrees. Default is 20°.</value>
    public double MinimumAngleDegrees { get; set; } = 20.0;

    /// <summary>
    ///     Gets or sets the minimum triangle area below which triangles are skipped.
    /// </summary>
    /// <remarks>
    ///     Triangles with area smaller than this threshold will not be refined,
    ///     preventing the algorithm from attempting to refine degenerate triangles.
    /// </remarks>
    /// <value>The minimum triangle area. Default is 1e-3.</value>
    public double MinimumTriangleArea { get; set; } = 1e-3;

    /// <summary>
    ///     Gets or sets whether to enforce the √2 guard for the circumradius-to-edge ratio.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         When true, the algorithm clamps ρ_min to max(√2, 1/(2·sin(θ_min)))
    ///         to ensure termination. When false, it uses 1/(2·sin(θ_min)) directly.
    ///     </para>
    ///     <para>
    ///         Setting this to false allows smaller minimum angles but may risk
    ///         non-termination in pathological cases.
    ///     </para>
    /// </remarks>
    /// <value><c>true</c> to enforce the √2 guard; otherwise <c>false</c>. Default is <c>false</c>.</value>
    public bool EnforceSqrt2Guard { get; set; } = false;

    /// <summary>
    ///     Gets or sets whether to skip seditious triangles.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Seditious triangles are those formed by edges connecting midpoint vertices
    ///         on the same "shell" around an acute corner (angle &lt; 60°). These can cause
    ///         cascading splits that slow convergence or prevent termination.
    ///     </para>
    ///     <para>
    ///         When true, such triangles are skipped during the bad-triangle phase,
    ///         which improves convergence near acute constraint angles.
    ///     </para>
    /// </remarks>
    /// <value><c>true</c> to skip seditious triangles; otherwise <c>false</c>. Default is <c>true</c>.</value>
    public bool SkipSeditiousTriangles { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to ignore encroachments caused by seditious configurations.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         When true, encroachments where the encroaching vertex is a midpoint
    ///         on the same shell as the segment endpoints (relative to a critical corner)
    ///         are ignored. This prevents cascading segment splits near acute angles.
    ///     </para>
    /// </remarks>
    /// <value><c>true</c> to ignore seditious encroachments; otherwise <c>false</c>. Default is <c>true</c>.</value>
    public bool IgnoreSeditiousEncroachments { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to interpolate Z values for new vertices.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         When true, Z values for newly inserted Steiner points are computed
    ///         by interpolating from the surrounding triangle vertices using linear
    ///         (barycentric) interpolation.
    ///     </para>
    ///     <para>
    ///         When false, Z values are computed as the average of the two segment
    ///         endpoints (for midpoint insertions) or left as zero (for circumcenter
    ///         insertions).
    ///     </para>
    /// </remarks>
    /// <value><c>true</c> to interpolate Z values; otherwise <c>false</c>. Default is <c>false</c>.</value>
    public bool InterpolateZ { get; set; } = false;

    /// <summary>
    ///     Gets or sets whether to refine only triangles inside constraint regions.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         When true (default), only triangles that are members of a constraint region
    ///         (i.e., inside a polygon constraint) are candidates for refinement. Triangles
    ///         outside constraint regions are left as-is.
    ///     </para>
    ///     <para>
    ///         When false, all triangles in the TIN are candidates for refinement,
    ///         regardless of whether they are inside a constraint region. This is useful
    ///         when you want to improve mesh quality across the entire triangulation,
    ///         not just within constrained areas.
    ///     </para>
    ///     <para>
    ///         Note: This option only has an effect when the TIN contains polygon constraints.
    ///         If no constraints are present, all triangles are refined regardless of this setting.
    ///     </para>
    /// </remarks>
    /// <value><c>true</c> to refine only inside constraints; otherwise <c>false</c>. Default is <c>true</c>.</value>
    public bool RefineOnlyInsideConstraints { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to automatically add a bounding box constraint before refinement.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         When true, a rectangular polygon constraint is automatically added around the
    ///         existing vertices before refinement begins. This prevents the refinement algorithm
    ///         from expanding the mesh beyond the original data bounds.
    ///     </para>
    ///     <para>
    ///         The bounding box is created with a small buffer (controlled by <see cref="BoundingBoxBufferPercent"/>)
    ///         to ensure constraint edges don't coincide with perimeter edges.
    ///     </para>
    ///     <para>
    ///         This option is particularly useful when <see cref="RefineOnlyInsideConstraints"/> is false
    ///         and you want to refine the entire mesh while keeping it within original bounds.
    ///     </para>
    /// </remarks>
    /// <value><c>true</c> to add a bounding box constraint; otherwise <c>false</c>. Default is <c>false</c>.</value>
    public bool AddBoundingBoxConstraint { get; set; } = false;

    /// <summary>
    ///     Gets or sets the buffer percentage for the bounding box constraint.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         When <see cref="AddBoundingBoxConstraint"/> is true, this value determines how much
    ///         larger the bounding box is than the actual data bounds, as a percentage of the
    ///         bounds size. A small buffer ensures constraint edges don't coincide with perimeter edges.
    ///     </para>
    ///     <para>
    ///         For example, a value of 1.0 means the bounding box is 1% larger on each side.
    ///     </para>
    /// </remarks>
    /// <value>The buffer percentage. Default is 1.0 (1%).</value>
    public double BoundingBoxBufferPercent { get; set; } = 1.0;

    /// <summary>
    ///     Gets or sets the interpolation method to use for computing Z values of new vertices.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Only used when <see cref="InterpolateZ"/> is true. Determines which
    ///         interpolation algorithm is used to compute Z values for newly inserted
    ///         Steiner points (offcenter and circumcenter insertions).
    ///     </para>
    ///     <para>
    ///         <see cref="Interpolation.InterpolationType.TriangularFacet"/> (default): Fast linear
    ///         interpolation treating each triangle as a planar facet. Provides C0 continuity.
    ///     </para>
    ///     <para>
    ///         <see cref="Interpolation.InterpolationType.NaturalNeighbor"/>: Sibson's Natural Neighbor
    ///         interpolation. Slower but provides smoother C1 continuity (except at data vertices).
    ///         Better for 3D visualization and terrain modeling.
    ///     </para>
    /// </remarks>
    /// <value>The interpolation method. Default is <see cref="Interpolation.InterpolationType.TriangularFacet"/>.</value>
    public InterpolationType InterpolationType { get; set; } = InterpolationType.TriangularFacet;

    /// <summary>
    ///     Gets or sets the maximum number of iterations for the refinement loop.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This provides a safety limit to prevent infinite loops in pathological
    ///         cases. If the iteration count exceeds this value, <see cref="IDelaunayRefiner.Refine"/>
    ///         returns <c>false</c> to indicate incomplete refinement.
    ///     </para>
    ///     <para>
    ///         Set to 0 or a negative value to disable the limit (not recommended).
    ///     </para>
    /// </remarks>
    /// <value>The maximum iteration count. Default is 100,000.</value>
    public int MaxIterations { get; set; } = 100_000;

    /// <summary>
    ///     Creates a new instance of <see cref="RuppertOptions"/> with default values.
    /// </summary>
    public RuppertOptions()
    {
    }

    /// <summary>
    ///     Creates a new instance of <see cref="RuppertOptions"/> with the specified minimum angle.
    /// </summary>
    /// <param name="minimumAngleDegrees">The minimum angle threshold in degrees.</param>
    public RuppertOptions(double minimumAngleDegrees)
    {
        MinimumAngleDegrees = minimumAngleDegrees;
    }

    /// <summary>
    ///     Creates a new instance of <see cref="RuppertOptions"/> with the specified parameters.
    /// </summary>
    /// <param name="minimumAngleDegrees">The minimum angle threshold in degrees.</param>
    /// <param name="minimumTriangleArea">The minimum triangle area threshold.</param>
    public RuppertOptions(double minimumAngleDegrees, double minimumTriangleArea)
    {
        MinimumAngleDegrees = minimumAngleDegrees;
        MinimumTriangleArea = minimumTriangleArea;
    }
}
