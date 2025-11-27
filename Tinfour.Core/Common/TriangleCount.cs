/*
 * Copyright 2023 G.W. Lucas
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

namespace Tinfour.Core.Common;

/// <summary>
///     Provides a count of triangles in different categories for diagnostic
///     and statistical purposes.
/// </summary>
public class TriangleCount
{
    /// <summary>
    ///     Creates a new TriangleCount with all counts initialized to zero.
    /// </summary>
    public TriangleCount()
    {
        ValidTriangles = 0;
        GhostTriangles = 0;
        ConstrainedTriangles = 0;
    }

    /// <summary>
    ///     Creates a new TriangleCount with the specified counts.
    /// </summary>
    /// <param name="validTriangles">The number of valid triangles</param>
    /// <param name="ghostTriangles">The number of ghost triangles</param>
    /// <param name="constrainedTriangles">The number of constrained triangles</param>
    public TriangleCount(int validTriangles, int ghostTriangles, int constrainedTriangles)
    {
        ValidTriangles = validTriangles;
        GhostTriangles = ghostTriangles;
        ConstrainedTriangles = constrainedTriangles;
    }

    /// <summary>
    ///     The number of triangles that are part of constrained regions.
    ///     These are triangles that lie within user-specified constraint boundaries.
    /// </summary>
    public int ConstrainedTriangles { get; set; }

    /// <summary>
    ///     The number of ghost triangles in the TIN.
    ///     Ghost triangles are those that lie outside the convex hull of the input data
    ///     and contain at least one null vertex.
    /// </summary>
    public int GhostTriangles { get; set; }

    /// <summary>
    ///     The total number of triangles, including both valid and ghost triangles.
    /// </summary>
    public int TotalTriangles => ValidTriangles + GhostTriangles;

    /// <summary>
    ///     The total number of valid triangles in the TIN.
    ///     This includes both interior and perimeter triangles but excludes ghost triangles.
    /// </summary>
    public int ValidTriangles { get; set; }

    /// <summary>
    ///     Gets a string representation of the triangle counts.
    /// </summary>
    /// <returns>A formatted string showing the triangle counts.</returns>
    public override string ToString()
    {
        return
            $"TriangleCount[Valid: {ValidTriangles}, Ghost: {GhostTriangles}, Constrained: {ConstrainedTriangles}, Total: {TotalTriangles}]";
    }
}