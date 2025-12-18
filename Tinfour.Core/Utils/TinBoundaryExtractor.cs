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
 *   Utility class for extracting boundary vertices from a TIN.
 *   Provides workarounds for perimeter topology issues.
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Utils;

using Tinfour.Core.Common;

/// <summary>
///     Provides methods for extracting boundary vertices from a TIN.
/// </summary>
/// <remarks>
///     <para>
///         This utility class provides reliable methods for extracting boundary
///         vertices from a TIN, including workarounds for cases where the standard
///         <see cref="IIncrementalTin.GetPerimeter"/> method may have topology issues.
///     </para>
/// </remarks>
public static class TinBoundaryExtractor
{
    /// <summary>
    ///     Extracts the external boundary vertices of a TIN by scanning all edges.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method provides a reliable way to get boundary vertices even when
    ///         the perimeter edge topology may be corrupted. It works by scanning all
    ///         edges and identifying those that border ghost triangles.
    ///     </para>
    ///     <para>
    ///         The returned vertices are in counter-clockwise order around the TIN boundary.
    ///     </para>
    /// </remarks>
    /// <param name="tin">The TIN to extract boundary vertices from.</param>
    /// <returns>A list of vertices forming the external boundary, or an empty list if the TIN is not bootstrapped.</returns>
    public static List<Vertex> GetBoundaryVertices(IIncrementalTin tin)
    {
        if (!tin.IsBootstrapped())
            return new List<Vertex>();

        // Find all perimeter edges by scanning for edges with ghost vertex on one side
        var perimeterEdges = new List<IQuadEdge>();
        foreach (var edge in tin.GetEdges())
        {
            // A perimeter edge has a ghost vertex (null vertex) on one side
            var b = edge.GetB();
            if (b.IsNullVertex())
            {
                // This edge's dual is the interior perimeter edge
                perimeterEdges.Add(edge.GetDual());
            }
        }

        if (perimeterEdges.Count < 3)
            return new List<Vertex>();

        // Build a map from vertex to the edge that starts at that vertex
        var edgeMap = new Dictionary<int, IQuadEdge>();
        foreach (var edge in perimeterEdges)
        {
            var a = edge.GetA();
            if (!a.IsNullVertex())
                edgeMap[a.GetIndex()] = edge;
        }

        // Walk the perimeter starting from the first edge
        var result = new List<Vertex>();
        var startEdge = perimeterEdges[0];
        var currentEdge = startEdge;
        var maxIterations = perimeterEdges.Count + 10;
        var iterations = 0;

        do
        {
            var v = currentEdge.GetA();
            if (!v.IsNullVertex())
            {
                // Create a copy of the vertex to avoid reference issues
                result.Add(new Vertex(v.X, v.Y, v.GetZ(), v.GetIndex()));
            }

            // Move to next edge
            var nextVertex = currentEdge.GetB();
            if (nextVertex.IsNullVertex() || !edgeMap.TryGetValue(nextVertex.GetIndex(), out var nextEdge))
                break;

            currentEdge = nextEdge;
            iterations++;
        }
        while (currentEdge != startEdge && iterations < maxIterations);

        return result;
    }

    /// <summary>
    ///     Creates a polygon constraint from the TIN's external boundary.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method extracts the boundary vertices and creates a polygon constraint
    ///         that can be used to constrain operations to the original data extent.
    ///     </para>
    ///     <para>
    ///         The constraint vertices are assigned new indices starting from the specified
    ///         base index to avoid conflicts with existing vertices.
    ///     </para>
    /// </remarks>
    /// <param name="tin">The TIN to extract the boundary from.</param>
    /// <param name="startingIndex">The starting index for new constraint vertices.</param>
    /// <returns>A polygon constraint representing the TIN boundary, or null if extraction fails.</returns>
    public static PolygonConstraint? CreateBoundaryConstraint(IIncrementalTin tin, int startingIndex = 1_000_000)
    {
        var boundaryVertices = GetBoundaryVertices(tin);
        if (boundaryVertices.Count < 3)
            return null;

        // Create new vertices with unique indices
        var constraintVertices = new List<Vertex>();
        var index = startingIndex;
        foreach (var v in boundaryVertices)
        {
            constraintVertices.Add(new Vertex(v.X, v.Y, v.GetZ(), index++));
        }

        return new PolygonConstraint(constraintVertices);
    }

    /// <summary>
    ///     Gets the convex hull vertices of the TIN.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         For a Delaunay triangulation without constraints, the external boundary
    ///         is the convex hull of the input points. This method is equivalent to
    ///         <see cref="GetBoundaryVertices"/> for unconstrained triangulations.
    ///     </para>
    /// </remarks>
    /// <param name="tin">The TIN to extract the convex hull from.</param>
    /// <returns>A list of vertices forming the convex hull.</returns>
    public static List<Vertex> GetConvexHullVertices(IIncrementalTin tin)
    {
        // For a Delaunay triangulation, the boundary is the convex hull
        return GetBoundaryVertices(tin);
    }
}
