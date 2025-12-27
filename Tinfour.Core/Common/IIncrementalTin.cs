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
 * 08/2025  M.Fender     Ported to C#
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

using Tinfour.Core.Interpolation;

/// <summary>
///     Defines an interface for classes that implement a Triangulated Irregular Network (TIN)
///     that can be built on an incremental basis.
/// </summary>
public interface IIncrementalTin : IDisposable
{
    /// <summary>
    ///     Adds a vertex to the TIN.
    /// </summary>
    /// <param name="v">The vertex to be added.</param>
    /// <returns>True if the TIN was modified; otherwise, false.</returns>
    bool Add(IVertex v);

    /// <summary>
    ///     Adds a vertex to the TIN and returns an edge connected to the inserted vertex.
    ///     This is useful for avoiding redundant point location after insertion.
    /// </summary>
    /// <param name="v">The vertex to be added.</param>
    /// <returns>An edge with A == v, or null if the TIN is not bootstrapped or vertex was merged.</returns>
    IQuadEdge? AddAndReturnEdge(IVertex v);

    /// <summary>
    ///     Adds a list of vertices to the TIN.
    /// </summary>
    /// <param name="vertices">A valid list of vertices.</param>
    /// <returns>True if the TIN was modified; otherwise, false.</returns>
    bool Add(IEnumerable<IVertex> vertices);

    /// <summary>
    ///     Adds a list of vertices to the TIN with an ordering hint.
    ///     Implementations may apply HilbertSort for improved locality.
    /// </summary>
    /// <param name="vertices">Vertices to insert.</param>
    /// <param name="order">Ordering hint (AsIs or Hilbert).</param>
    /// <returns>True if the TIN was modified; otherwise, false.</returns>
    bool Add(IEnumerable<IVertex> vertices, VertexOrder order);

    /// <summary>
    ///     Adds a list of constraints to the TIN.
    /// </summary>
    /// <param name="constraints">A valid list of constraints.</param>
    /// <param name="restoreConformity">Indicates whether to restore Delaunay conformity.</param>
    /// <param name="preInterpolateZ">Indicates whether to pre-interpolate Z values for constraint vertices.</param>
    void AddConstraints(IList<IConstraint> constraints, bool restoreConformity, bool preInterpolateZ = false);

    /// <summary>
    ///     Adds a list of vertices pre-sorted by Hilbert curve.
    ///     Convenience alias for Add(vertices, VertexOrder.Hilbert).
    /// </summary>
    /// <param name="vertices">Vertices to insert.</param>
    /// <returns>True if the TIN was modified; otherwise, false.</returns>
    bool AddSorted(IEnumerable<IVertex> vertices);

    /// <summary>
    ///     Clears all internal state data of the TIN.
    /// </summary>
    void Clear();

    /// <summary>
    ///     Gets a new instance of a triangle-count object.
    /// </summary>
    /// <returns>A valid instance of TriangleCount.</returns>
    TriangleCount CountTriangles();

    /// <summary>
    ///     Gets the bounds of the TIN.
    /// </summary>
    /// <returns>A valid instance of a bounding box.</returns>
    (double Left, double Top, double Width, double Height)? GetBounds();

    /// <summary>
    ///     Gets the constraint with the specified index.
    /// </summary>
    /// <param name="index">The index of the constraint.</param>
    /// <returns>A valid constraint, or null if no constraint with the specified index is found.</returns>
    IConstraint? GetConstraint(int index);

    /// <summary>
    ///     Gets a list of the constraints currently defined for the TIN.
    /// </summary>
    /// <returns>A valid, potentially empty, list of constraints.</returns>
    IList<IConstraint> GetConstraints();

    /// <summary>
    ///     Gets an enumerable over all edges in the TIN.
    /// </summary>
    /// <returns>An enumerable collection of edges</returns>
    IEnumerable<IQuadEdge> GetEdgeIterator();

    /// <summary>
    ///     Gets a list of the edges in the TIN.
    /// </summary>
    /// <returns>A valid, potentially empty, list of edges.</returns>
    IList<IQuadEdge> GetEdges();

    /// <summary>
    ///     Gets the maximum index of the currently allocated edges.
    /// </summary>
    /// <returns>A positive value or zero if the TIN is not bootstrapped</returns>
    int GetMaximumEdgeAllocationIndex();

    /// <summary>
    ///     Gets a navigator for interpolation operations.
    /// </summary>
    /// <returns>A navigator instance for the TIN</returns>
    IIncrementalTinNavigator GetNavigator();

    /// <summary>
    ///     Gets the nominal point spacing for the TIN.
    /// </summary>
    /// <returns>A positive floating-point value.</returns>
    double GetNominalPointSpacing();

    /// <summary>
    ///     Gets a list of the edges that form the perimeter of the TIN.
    /// </summary>
    /// <returns>A valid, potentially empty, list of edges.</returns>
    IList<IQuadEdge> GetPerimeter();

    /// <summary>
    ///     Gets the count of synthetic vertices in the TIN.
    /// </summary>
    /// <returns>A positive integer.</returns>
    int GetSyntheticVertexCount();

    /// <summary>
    ///     Gets the thresholds object associated with this TIN.
    /// </summary>
    /// <returns>A valid thresholds object.</returns>
    Thresholds GetThresholds();

    /// <summary>
    ///     Gets a new instance of a triangle iterator.
    /// </summary>
    /// <returns>A valid iterator.</returns>
    IEnumerable<SimpleTriangle> GetTriangles();

    /// <summary>
    ///     Gets a list of the vertices currently residing in the TIN.
    /// </summary>
    /// <returns>A valid, potentially empty, list of vertices.</returns>
    IList<IVertex> GetVertices();

    /// <summary>
    ///     Indicates whether the TIN has been bootstrapped.
    /// </summary>
    /// <returns>True if the TIN is bootstrapped; otherwise, false.</returns>
    bool IsBootstrapped();

    /// <summary>
    ///     Indicates if the TIN is Delaunay conformant.
    /// </summary>
    /// <returns>True if the TIN is conformant; otherwise, false.</returns>
    bool IsConformant();

    /// <summary>
    ///     Indicates whether the TIN is locked. A locked TIN cannot be modified.
    /// </summary>
    /// <returns>True if the TIN is locked; otherwise, false.</returns>
    bool IsLocked();

    /// <summary>
    ///     Indicates if a point is inside the TIN.
    /// </summary>
    /// <param name="x">The x coordinate of the point.</param>
    /// <param name="y">The y coordinate of the point.</param>
    /// <returns>True if the point is inside the TIN; otherwise, false.</returns>
    bool IsPointInsideTin(double x, double y);

    /// <summary>
    ///     Sets the TIN to a locked state.
    /// </summary>
    void Lock();

    /// <summary>
    ///     Provides a preallocation hint for bulk insertion.
    ///     Implementations may allocate approximately 3*n edges in advance.
    /// </summary>
    /// <param name="vertexCount">Expected number of vertices to be added soon.</param>
    void PreAllocateForVertices(int vertexCount);

    /// <summary>
    ///     Splits an existing edge at the specified parametric position.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method divides an existing edge at a parametric position t
    ///         (measured from origin A toward destination B). The inserted vertex
    ///         inherits the constraint status of the edge; if the input edge is
    ///         constrained, the new vertex is marked as a constraint vertex.
    ///     </para>
    ///     <para>
    ///         Implementations may clamp t to avoid zero-length subedges.
    ///         This method is primarily used by mesh refinement algorithms.
    ///     </para>
    /// </remarks>
    /// <param name="edge">The edge to split</param>
    /// <param name="t">Parametric position along edge (0.0 to 1.0, typically 0.5 for midpoint)</param>
    /// <param name="z">The Z coordinate for the new vertex</param>
    /// <returns>The newly created vertex at the split point, or null on failure</returns>
    IVertex? SplitEdge(IQuadEdge edge, double t, double z);

    /// <summary>
    ///     Releases the lock on a TIN.
    /// </summary>
    void Unlock();
}