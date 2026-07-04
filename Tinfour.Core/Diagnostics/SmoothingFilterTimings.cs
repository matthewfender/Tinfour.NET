/*
 * Copyright 2015-2026 Gary W. Lucas.
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
 * 07/2026  M. Fender    Created - SmoothingFilter phase timing attribution
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Diagnostics;

/// <summary>
///     Wall-clock timings for the internal phases of
///     <see cref="Utils.SmoothingFilter"/> construction, captured on every construction.
/// </summary>
/// <remarks>
///     Smoothing-filter construction on a large TIN is a multi-phase operation whose cost
///     was historically reported as a single number. These timings give callers per-phase
///     attribution (for example, to surface one log line per phase in an application
///     generation log). Collection cost is a handful of stopwatch reads per construction.
/// </remarks>
public sealed class SmoothingFilterTimings
{
    /// <summary>
    ///     Collecting the TIN vertices, assigning dense indices, capturing original Z
    ///     values and recording each vertex's first incident half-edge (the pinwheel
    ///     starting edge used by the neighbor build).
    /// </summary>
    public TimeSpan VertexCollection { get; init; }

    /// <summary>
    ///     Building the per-vertex neighbor index: pinwheel enumeration of each vertex's
    ///     connected polygon and computation of its barycentric weights.
    /// </summary>
    public TimeSpan NeighborBuild { get; init; }

    /// <summary>
    ///     Wall-clock time of each individual smoothing pass, in pass order.
    /// </summary>
    public IReadOnlyList<TimeSpan> Passes { get; init; } = Array.Empty<TimeSpan>();

    /// <summary>
    ///     Building the vertex-to-smoothed-Z result dictionary consumed by
    ///     <see cref="Utils.SmoothingFilter.Value"/>.
    /// </summary>
    public TimeSpan ResultMap { get; init; }

    /// <summary>
    ///     Total wall-clock time of the initializer (excludes the final min/max scan
    ///     performed by the <see cref="Utils.SmoothingFilter"/> constructor).
    /// </summary>
    public TimeSpan Total { get; init; }

    /// <summary>
    ///     Number of vertices collected from the TIN.
    /// </summary>
    public int VertexCount { get; init; }

    /// <summary>
    ///     Number of vertices that received a neighbor index (interior, unconstrained
    ///     vertices with valid barycentric weights); the remainder pass their original Z
    ///     through unchanged.
    /// </summary>
    public int SmoothedVertexCount { get; init; }

    /// <summary>
    ///     Formats the timings as a single human-readable line for log output.
    /// </summary>
    public override string ToString()
    {
        var passes = string.Join("/", Passes.Select(p => p.TotalMilliseconds.ToString("F0")));
        return $"vertices={VertexCount:N0} (smoothed {SmoothedVertexCount:N0}) "
               + $"collect={VertexCollection.TotalMilliseconds:F0}ms "
               + $"neighbors={NeighborBuild.TotalMilliseconds:F0}ms "
               + $"passes[{Passes.Count}]={passes}ms "
               + $"resultMap={ResultMap.TotalMilliseconds:F0}ms "
               + $"total={Total.TotalMilliseconds:F0}ms";
    }
}
