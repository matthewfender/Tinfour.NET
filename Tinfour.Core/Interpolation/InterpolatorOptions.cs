/*
 * Copyright 2025 Gary W. Lucas / ReefMaster Software Ltd.
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

namespace Tinfour.Core.Interpolation;

/// <summary>
///     Configuration options for creating interpolators via <see cref="InterpolatorFactory"/>.
/// </summary>
/// <remarks>
///     <para>
///         This class provides a centralized way to configure interpolator behavior,
///         including distance limits and algorithm-specific parameters.
///     </para>
///     <para>
///         Example usage:
///         <code>
///         var options = new InterpolatorOptions
///         {
///             MaxInterpolationDistance = 500.0,  // meters
///             ConstrainedRegionsOnly = true
///         };
///         var interpolator = InterpolatorFactory.Create(tin, InterpolationType.NaturalNeighbor, options);
///         </code>
///     </para>
/// </remarks>
public class InterpolatorOptions
{
    /// <summary>
    ///     Gets or sets whether interpolation is restricted to constrained regions only.
    ///     When true, queries outside constrained regions return NaN.
    /// </summary>
    /// <remarks>
    ///     Default is false (interpolation performed everywhere within the TIN).
    /// </remarks>
    public bool ConstrainedRegionsOnly { get; set; } = false;

    /// <summary>
    ///     Gets or sets the maximum distance from a data point at which interpolation
    ///     will be performed. If the query point is further than this distance from the
    ///     nearest vertex of the enclosing triangle, NaN is returned.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A value of null (default) disables distance checking, allowing interpolation
    ///         at any distance from data points.
    ///     </para>
    ///     <para>
    ///         For bathymetric applications, typical values range from 200-500 meters,
    ///         preventing meaningless extrapolation far from actual soundings.
    ///     </para>
    /// </remarks>
    public double? MaxInterpolationDistance { get; set; } = null;

    /// <summary>
    ///     Gets or sets the power parameter for Inverse Distance Weighting interpolation.
    ///     Higher values give more weight to nearby points.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Only used when creating <see cref="InterpolationType.InverseDistanceWeighting"/> interpolators.
    ///     </para>
    ///     <para>
    ///         Default is 2.0, which is the most common choice. Values of 1.0 give linear
    ///         weighting, while values greater than 2.0 give increasingly more weight to
    ///         the nearest points.
    ///     </para>
    /// </remarks>
    public double IdwPower { get; set; } = 2.0;

    /// <summary>
    ///     Gets or sets whether the IDW interpolator uses distance weighting.
    /// </summary>
    /// <remarks>
    ///     Only used when creating <see cref="InterpolationType.InverseDistanceWeighting"/> interpolators.
    ///     Default is false.
    /// </remarks>
    public bool IdwUseDistanceWeighting { get; set; } = false;

    /// <summary>
    ///     Creates a copy of this options instance.
    /// </summary>
    /// <returns>A new InterpolatorOptions with the same settings.</returns>
    public InterpolatorOptions Clone()
    {
        return new InterpolatorOptions
        {
            ConstrainedRegionsOnly = ConstrainedRegionsOnly,
            MaxInterpolationDistance = MaxInterpolationDistance,
            IdwPower = IdwPower,
            IdwUseDistanceWeighting = IdwUseDistanceWeighting
        };
    }
}
