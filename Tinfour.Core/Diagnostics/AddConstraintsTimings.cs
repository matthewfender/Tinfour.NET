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
 * 07/2026  M. Fender    Created - AddConstraints phase timing attribution
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Diagnostics;

/// <summary>
///     Wall-clock timings for the internal phases of
///     <see cref="Standard.IncrementalTin.AddConstraints"/>, captured on every call.
/// </summary>
/// <remarks>
///     Constraint addition on a large TIN is a multi-phase operation whose cost was
///     historically reported as a single number. These timings give callers per-phase
///     attribution (for example, to surface one log line per phase in an application
///     generation log). Collection cost is a handful of stopwatch reads per call.
/// </remarks>
public sealed class AddConstraintsTimings
{
    /// <summary>
    ///     Phase 0: adding constraint vertices that already carry a real (non-NaN) Z to the
    ///     TIN so they are available as interpolation sources. Zero when pre-interpolation
    ///     is disabled.
    /// </summary>
    public TimeSpan SeedConstraintVertices { get; init; }

    /// <summary>
    ///     Building the transient interpolation TIN (vertex snapshot, Hilbert-ordered
    ///     re-triangulation, interpolator and navigator construction). Zero when
    ///     pre-interpolation is disabled.
    /// </summary>
    public TimeSpan InterpolationTinBuild { get; init; }

    /// <summary>
    ///     Number of vertices re-triangulated into the transient interpolation TIN.
    ///     Zero when pre-interpolation is disabled.
    /// </summary>
    public int InterpolationTinVertexCount { get; init; }

    /// <summary>
    ///     Phase 1–2: interpolating Z for NaN constraint vertices, inserting constraint
    ///     vertices into the TIN, remapping duplicates and assigning constraint indices.
    /// </summary>
    public TimeSpan InterpolateAndInsertVertices { get; init; }

    /// <summary>
    ///     Phase 3: full CDT processing of each constraint (cavity digging, edge wiring).
    /// </summary>
    public TimeSpan ProcessConstraints { get; init; }

    /// <summary>
    ///     Phase 4: restoring Delaunay conformity by subdividing constrained edges.
    ///     Zero when conformity restoration is not requested.
    /// </summary>
    public TimeSpan RestoreConformity { get; init; }

    /// <summary>
    ///     Number of synthetic vertices created while restoring conformity
    ///     (constrained-edge midpoint splits).
    /// </summary>
    public int RestoreConformitySyntheticVertices { get; init; }

    /// <summary>
    ///     Phase 5: flood-filling constrained-region interiors.
    /// </summary>
    public TimeSpan FloodFill { get; init; }

    /// <summary>
    ///     Total wall-clock time of the AddConstraints call.
    /// </summary>
    public TimeSpan Total { get; init; }
}
