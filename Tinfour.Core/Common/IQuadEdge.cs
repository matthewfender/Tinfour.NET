/*
 * Copyright 2014 Gary W. Lucas.
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
 * Date     Name        Description
 * ------   ---------   -------------------------------------------------
 * 11/2015  G. Lucas    Created
 * 12/2016  G. Lucas    Introduced support for constraints
 * 11/2017  G. Lucas    Refactored for constrained regions
 * 06/2025  G. Lucas    Refactored for better handling of constraint relationships
 * 08/2025 M.Fender    Ported to C#
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

using System.Numerics;

/// <summary>
///     Defines methods for accessing the data in a quad-edge implementation.
/// </summary>
/// <remarks>
///     <para>
///         Currently, Tinfour implements two kinds of quad-edge objects:
///         standard and semi-virtual. The standard implementation (the QuadEdge class)
///         is based on in-memory references and literal instances of objects.
///         The semi-virtual approach attempts to reduce the amount of memory used
///         by a Delaunay triangulation by maintaining some in-memory data in arrays
///         rather than objects. Edge-related objects are created as needed and
///         discarded when no longer required.
///     </para>
///     <para>
///         <strong>Memory considerations</strong>
///     </para>
///     <para>
///         For a Delaunay triangulation with a sufficiently large number of vertices, N,
///         the number of edges in the triangular mesh approaches 3*N. Since many of
///         the data sets that Tinfour processes can include millions of vertices,
///         memory management becomes an important issue.  Both the standard and
///         semi-virtual edge instances are designed to be conservative in their
///         use of memory. This approach has a significant influence on the organization
///         of the methods in the IQuadEdge interface.
///     </para>
///     <para>
///         <strong>Performance considerations</strong>
///     </para>
///     <para>
///         In general, get operations can be performed without
///         any degradation of performance. However, set operations on quad-edges
///         often require down casting (narrow casting) of object references.
///         In ordinary applications, the performance cost of down casting is small.
///         But for applications that require very large data sets and repeated
///         modifications to the edge structure of the TIN, this cost can degrade
///         processing rates by as much as 25 percent. Thus this interface avoids
///         specifying any methods that set edge relationships (connections).
///     </para>
///     <para>
///         <strong>Constraints and constrained edges</strong>
///     </para>
///     <para>
///         Normally, Tinfour is free to choose the geometry of the edges in a triangular
///         mesh based on the Delaunay criterion. But some applications require that
///         certain edges be preserved as specified. Therefore, Tinfour supports
///         the specification of constraint objects to create a Constrained Delaunay
///         Triangulations (CDT).
///     </para>
///     <para>
///         Tinfour supports two kinds of constraints: region (polygon) constraints,
///         and line constraints (chains of one or more connected edges not forming
///         a closed polygon).
///     </para>
///     <para>
///         <strong>Constraint assignment to edges</strong>
///     </para>
///     <para>
///         When constraint objects are added to an incremental TIN instance,
///         Tinfour assigns each object a unique integer index (currently, in the range
///         zero to 8190). IQuadEdge instances can store these indices for internal
///         or application use.
///     </para>
///     <para>
///         In a Delaunay triangulation, an edge is either constrained (fixed geometry)
///         or unconstrained (free to be modified to meet the Delaunay criterion).
///         In Tinfour, an edge can have some combination of the following constraint-related states:
///         <list type="number">
///             <item>Unconstrained</item>
///             <item>
///                 Border of a constrained region (polygon) or the common border
///                 of two adjacent regions (constrained)
///             </item>
///             <item>The interior of a region constraint (unconstrained)</item>
///             <item>A member of a line-based constraint (constrained)</item>
///         </list>
///     </para>
///     <para>
///         It is possible for an edge to belong to both a region-based constraint
///         (either as its border or its interior) and a line-based constraint.
///         Unfortunately, memory considerations for incremental TIN construction
///         limit the number of references to two. But an edge that is assigned both
///         as the border of two adjacent constraint regions and an independent
///         line constraint would require three. In such cases, the IQuadEdge instances
///         give priority to the region specifications. Tinfour's incremental TIN classes
///         implement logic for maintaining supplemental information so that they can
///         track linear constraint assignments when necessary.
///     </para>
/// </remarks>
public interface IQuadEdge
{
    /// <summary>
    ///     Clears the edge, resetting it to an uninitialized state.
    ///     This method is used for memory management in edge pools.
    /// </summary>
    void Clear();

    /// <summary>
    ///     Gets the initial vertex for this edge.
    /// </summary>
    /// <returns>A valid IVertex reference.</returns>
    IVertex GetA();

    /// <summary>
    ///     Gets the second vertex for this edge.
    /// </summary>
    /// <returns>A valid IVertex reference.</returns>
    IVertex GetB();

    /// <summary>
    ///     Gets the index of the "base" side of a bi-directional edge.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         In Tinfour implementations, edges are bi-directional. In effect,
    ///         the edge is implemented as a pair of unidirectional elements.
    ///         Each element is assigned a separate index. The first element in the
    ///         pair is designated as the "base" and is assigned an even-valued index.
    ///         Its dual is assigned a value one greater than the base index.
    ///         This method always returns an even value.
    ///     </para>
    ///     <para>
    ///         This method can be useful in cases where an application needs to track
    ///         a complete edge without regard to which side of the edge is being
    ///         considered.
    ///     </para>
    /// </remarks>
    /// <returns>A positive, even value.</returns>
    int GetBaseIndex();

    /// <summary>
    ///     Gets the reference to the side-zero edge of the pair.
    /// </summary>
    /// <remarks>
    ///     From the perspective of application code, the Tinfour implementations
    ///     of the two elements associated with a bi-directional edge are symmetrical.
    ///     Neither side of an edge is more significant than the other.
    /// </remarks>
    /// <returns>A reference to the side-zero edge of the pair.</returns>
    IQuadEdge GetBaseReference();

    /// <summary>
    ///     Gets the index of the region-based constraint associated with an edge
    ///     that serves as part of the polygon bounding that region.
    /// </summary>
    /// <returns>A positive integer or -1 if no constraint is specified.</returns>
    int GetConstraintBorderIndex();

    /// <summary>
    ///     Gets the index of the constraint associated with this edge.
    /// </summary>
    /// <returns>A positive value; may be zero if not specified.</returns>
    int GetConstraintIndex();

    /// <summary>
    ///     Gets the index of a line-based constraint associated with an edge.
    /// </summary>
    /// <returns>A positive integer or -1 is no constraint index is available.</returns>
    int GetConstraintLineIndex();

    /// <summary>
    ///     Gets the index of the region-based constraint associated with an
    ///     edge contained in the interior of a constraint polygon.
    /// </summary>
    /// <returns>A positive integer or -1 if no constraint is specified.</returns>
    int GetConstraintRegionInteriorIndex();

    /// <summary>
    ///     Gets the dual edge to this instance.
    /// </summary>
    /// <returns>A valid edge.</returns>
    IQuadEdge GetDual();

    /// <summary>
    ///     Gets the dual of the reverse reference of the edge.
    /// </summary>
    /// <returns>A valid reference, or null if the reverse reference is null.</returns>
    IQuadEdge? GetDualFromReverse();

    /// <summary>
    ///     Gets the forward reference of the edge.
    /// </summary>
    /// <returns>A valid reference.</returns>
    IQuadEdge GetForward();

    /// <summary>
    ///     Gets the forward reference of the dual.
    /// </summary>
    /// <returns>A valid reference</returns>
    IQuadEdge GetForwardFromDual();

    /// <summary>
    ///     Gets the index value for this edge.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         In general, the index value is intended for memory management and edge pools.
    ///         So while application code may read index values, it is not generally enabled to set them.
    ///     </para>
    ///     <para>
    ///         In Tinfour implementations, edges are bi-directional. In effect,
    ///         the edge is implemented as a pair of unidirectional elements.
    ///         Each element is assigned a separate index.
    ///     </para>
    ///     <para>
    ///         One common use for the index code by applications is to main a record
    ///         of processing performed using edge-traversal operations. For example,
    ///         some applications use the index to maintain a bitmap of visited edges
    ///         when performing surface analysis.
    ///     </para>
    ///     <para>
    ///         When an edge is allocated, it is set with an arbitrary index value.
    ///         This value will not change while the edge remains allocated by and
    ///         edge-pool instance. As soon as the edge is released, it is likely
    ///         to have its index value reassigned.
    ///     </para>
    /// </remarks>
    /// <returns>A positive integer value</returns>
    int GetIndex();

    /// <summary>
    ///     Gets the length of the edge.
    /// </summary>
    /// <returns>A positive floating point value</returns>
    double GetLength();

    /// <summary>
    ///     Gets an instance of an iterable that performs a pinwheel operation.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <strong>About the pinwheel operation:</strong> In the Tinfour library,
    ///         a pinwheel operation iterates over the set of edges that connect to the
    ///         initial IVertex of the current edge. The initial IVertex is the
    ///         one returned from a call to GetA(). Connected vertices may be obtained
    ///         through a call to GetB().
    ///     </para>
    ///     <para>
    ///         <strong>Null references for vertex:</strong>If vertex A lies on the
    ///         perimeter of the Delaunay mesh, one or more of the connected edges
    ///         may terminate on the "ghost vertex" which is used by Tinfour to
    ///         complete the triangulation. The ghost vertex is represented by
    ///         Vertex.Null. So applications performing a pinwheel on an
    ///         arbitrary edge should include logic to handle a null vertex from the
    ///         GetB() method.
    ///     </para>
    ///     <code>
    /// foreach(IQuadEdge e in startingEdge.GetPinwheel())
    /// {
    ///     Vertex B = e.GetB();
    ///     if(B.IsNullVertex())
    ///     {
    ///         // skip processing
    ///     }
    ///     else 
    ///     {
    ///         // perform processing using B
    ///     }
    /// }
    /// </code>
    /// </remarks>
    /// <returns>A valid enumerable collection.</returns>
    IEnumerable<IQuadEdge> GetPinwheel();

    /// <summary>
    ///     Gets the reverse reference of the edge.
    /// </summary>
    /// <returns>A valid reference.</returns>
    IQuadEdge GetReverse();

    /// <summary>
    ///     Gets the reverse link of the dual.
    /// </summary>
    /// <returns>A valid reference</returns>
    IQuadEdge GetReverseFromDual();

    /// <summary>
    ///     Indicates which side of an edge a particular IQuadEdge instance is
    ///     attached to. The side value is a strictly arbitrary index used for
    ///     algorithms that need to be able to assign a unique index to both sides of
    ///     an edge.
    /// </summary>
    /// <returns>A value of 0 or 1.</returns>
    int GetSide();

    /// <summary>
    ///     Indicates whether an edge is constrained.
    /// </summary>
    /// <returns>True if the edge is constrained; otherwise, false.</returns>
    bool IsConstrained();

    /// <summary>
    ///     Indicates whether the edge is a member of a constraint line.
    /// </summary>
    /// <remarks>
    ///     In some cases, a constraint line member edge may lie within a constrained region
    ///     but will not lie on one of its borders.
    /// </remarks>
    /// <returns>True if the edge is a member of an region; otherwise false.</returns>
    bool IsConstraintLineMember();

    /// <summary>
    ///     Indicates whether an edge represents the border of a constrained region.
    /// </summary>
    /// <remarks>
    ///     Border edges will always be constrained.  Border edges are also
    ///     classified as "member" edges of a constrained region.
    /// </remarks>
    /// <returns>
    ///     True if the edge is the border of the constrained region;
    ///     otherwise, false.
    /// </returns>
    bool IsConstraintRegionBorder();

    /// <summary>
    ///     Indicates whether the edge is in the interior of a constrained region.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Both sides of the edge lie within the interior of the region.
    ///         All points along the edge will lie within the interior of the region
    ///         with the possible exception of the endpoints. The endpoints may
    ///         lie on the border of the region.
    ///     </para>
    ///     <para>
    ///         An interior edge for a constrained region is not a constrained edge.
    ///         Interior edges are also classified as "member" edges of a constrained region.
    ///     </para>
    /// </remarks>
    /// <returns>True if the edge is in the interior of an region; otherwise false.</returns>
    bool IsConstraintRegionInterior();

    /// <summary>
    ///     Indicates whether the edge is a member of a constrained region
    ///     (is in the interior or serves as the border of a polygon-based constraint).
    /// </summary>
    /// <remarks>
    ///     A constrained region member is not necessarily a constrained edge.
    /// </remarks>
    /// <returns>True if the edge is a member of an region; otherwise false.</returns>
    bool IsConstraintRegionMember();

    /// <summary>
    ///     Indicates whether the synthetic flag is set for the edge.
    /// </summary>
    /// <returns>True if the edge is synthetic; otherwise, false.</returns>
    bool IsSynthetic();

    void SetA(IVertex a);

    /// <summary>
    ///     Sets a flag identifying the edge as the border of a region-based constraint
    ///     and stores the index for that constraint.
    /// </summary>
    /// <param name="constraintIndex">A positive integer in the range zero to 8190, or -1 for a null constraint.</param>
    void SetConstraintBorderIndex(int constraintIndex);

    /// <summary>
    ///     Sets the constraint index for this edge.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method does not necessarily set an edge to a constrained status.
    ///         In some implementations the constraint index may be used as a way of
    ///         associating ordinary edges with a neighboring constraint.
    ///         Constraint index values must be positive integers. The
    ///         range of supported values will depend on the specific class that
    ///         implements this interface. Please refer to the class documentation
    ///         for specific values.
    ///     </para>
    /// </remarks>
    /// <param name="constraintIndex">
    ///     A positive number indicating which constraint
    ///     a particular edge is associated with.
    /// </param>
    void SetConstraintIndex(int constraintIndex);

    /// <summary>
    ///     Sets a flag identifying the edge as the border of a line-based constraint
    ///     and stores the index for that constraint.
    /// </summary>
    /// <param name="constraintIndex">A positive integer in range zero to 8190</param>
    void SetConstraintLineIndex(int constraintIndex);

    /// <summary>
    ///     Sets the constraint-line member flag for the edge to true.
    /// </summary>
    void SetConstraintLineMemberFlag();

    /// <summary>
    ///     Sets a flag indicating that the edge is an edge of a constrained region.
    /// </summary>
    void SetConstraintRegionBorderFlag();

    /// <summary>
    ///     Sets a flag identifying the edge as an interior member of a region-based
    ///     constraint and stores the index for that constraint.
    /// </summary>
    /// <param name="constraintIndex">A positive integer in the range 0 to 8190, or -1 for a null value</param>
    void SetConstraintRegionInteriorIndex(int constraintIndex);

    /// <summary>
    ///     Sets the forward reference for this edge and reciprocal reverse reference.
    /// </summary>
    /// <param name="forward">A valid edge reference</param>
    void SetForward(IQuadEdge forward);

    /// <summary>
    ///     Sets the index value for this edge. This method is used primarily
    ///     for memory management and debugging purposes.
    /// </summary>
    /// <param name="index">The index value to assign</param>
    void SetIndex(int index);

    /// <summary>
    ///     Sets the reverse reference for this edge and reciprocal forward reference.
    /// </summary>
    /// <param name="reverse">A valid edge reference</param>
    void SetReverse(IQuadEdge reverse);

    /// <summary>
    ///     Sets the synthetic flag for the edge.
    /// </summary>
    /// <remarks>
    ///     Synthetic edges are those that do not arise naturally from the TIN-building logic but
    ///     are created by special operations.
    /// </remarks>
    /// <param name="status">True if the edge is synthetic; otherwise, false.</param>
    void SetSynthetic(bool status);

    /// <summary>
    ///     Sets the vertices for this edge (and its dual).
    /// </summary>
    /// <param name="a">The initial IVertex</param>
    /// <param name="b">The second IVertex</param>
    void SetVertices(IVertex a, IVertex b);

    /// <summary>
    ///     Provides a convenience method for rendering edges by setting the
    ///     specified coordinates with the edge's endpoints.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method transcribes the edge's coordinates to two Vector2 points.
    ///     </para>
    ///     <para>
    ///         This method is intended to support rendering operations that may
    ///         render a large number of edges.
    ///     </para>
    /// </remarks>
    /// <param name="startPoint">Output parameter to receive the starting point of the edge.</param>
    /// <param name="endPoint">Output parameter to receive the ending point of the edge.</param>
    void TranscribeTo(out Vector2 startPoint, out Vector2 endPoint);
}