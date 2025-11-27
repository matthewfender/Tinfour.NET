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

using Tinfour.Core.Common;

/// <summary>
///     Factory for creating interpolators based on interpolation type.
/// </summary>
public static class InterpolatorFactory
{
    /// <summary>
    ///     Creates an interpolator for the given TIN and interpolation type.
    /// </summary>
    /// <param name="tin">The TIN to interpolate over.</param>
    /// <param name="type">The interpolation type.</param>
    /// <param name="constrainedRegionsOnly">Flag indicating whether to use constrained regions only.</param>
    /// <returns>An IInterpolatorOverTin instance.</returns>
    public static IInterpolatorOverTin Create(
        IIncrementalTin tin,
        InterpolationType type,
        bool constrainedRegionsOnly = false)
    {
        return Create(tin, type, new InterpolatorOptions { ConstrainedRegionsOnly = constrainedRegionsOnly });
    }

    /// <summary>
    ///     Creates an interpolator for the given TIN, interpolation type, and options.
    /// </summary>
    /// <param name="tin">The TIN to interpolate over.</param>
    /// <param name="type">The interpolation type.</param>
    /// <param name="options">Configuration options for the interpolator. If null, default options are used.</param>
    /// <returns>An IInterpolatorOverTin instance.</returns>
    /// <remarks>
    ///     <para>
    ///         This overload provides full control over interpolator configuration,
    ///         including MaxInterpolationDistance and algorithm-specific parameters.
    ///     </para>
    ///     <para>
    ///         Example:
    ///         <code>
    ///         var options = new InterpolatorOptions
    ///         {
    ///             MaxInterpolationDistance = 500.0,
    ///             ConstrainedRegionsOnly = true,
    ///             IdwPower = 3.0  // Only used for IDW interpolation
    ///         };
    ///         var interpolator = InterpolatorFactory.Create(tin, InterpolationType.NaturalNeighbor, options);
    ///         </code>
    ///     </para>
    /// </remarks>
    public static IInterpolatorOverTin Create(
        IIncrementalTin tin,
        InterpolationType type,
        InterpolatorOptions? options)
    {
        var opts = options ?? new InterpolatorOptions();

        IInterpolatorOverTin interpolator = type switch
        {
            InterpolationType.TriangularFacet => new TriangularFacetInterpolator(tin, opts.ConstrainedRegionsOnly),
            InterpolationType.NaturalNeighbor => new NaturalNeighborInterpolator(tin, opts.ConstrainedRegionsOnly),
            InterpolationType.InverseDistanceWeighting => new InverseDistanceWeightingInterpolator(
                tin,
                opts.IdwPower,
                opts.IdwUseDistanceWeighting,
                opts.ConstrainedRegionsOnly),
            _ => throw new ArgumentException($"Unknown interpolation type: {type}", nameof(type))
        };

        // Apply common options
        interpolator.MaxInterpolationDistance = opts.MaxInterpolationDistance;

        return interpolator;
    }
}
