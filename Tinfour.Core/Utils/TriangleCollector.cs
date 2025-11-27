/*
 * Copyright 2017-2025 Gary W. Lucas.
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
 * 10/2017  M. Janda     Created
 * 11/2017  G. Lucas     Replaced recursion with deque
 * 08/2025 M.Fender     Ported to C# for Tinfour.Core
 *
 * Notes:
 *   This class was originally written by Martin Janda.
 *
 * Collecting triangles from constrained regions ------------------
 *  The triangle collector for a constrained region uses a mesh-traversal
 * operation where it traverses from edge to edge, identifying triangles and
 * calling the accept() method from a consumer.  In general, this
 * process is straightforward, though there is one special case.
 *  Recall that in Tinfour, the interior to a constrained region is always to
 * the left of an edge.  Thus, a polygon enclosing a region would be given
 * in counterclockwise order.  Conversely, if a polygon were given in
 * clockwise order such that the area it enclosed was always to the right
 * of the edges, the polygon would define a "hole" in the constrained
 * region. The region it enclosed would not belong to the constrained region.
 *   Now imaging a case where a constrained region is defined by a single
 * clockwise polygon somewhere within the overall domain of the Delaunay
 * Triangulation. The "constrained region" that it establishes is somewhat
 * counterintuitively defined as being outside the polygon and extendending
 * to the perimeter of the overall triangulation.
 *   In this case, if we attempt to use traversal, some of the triangles
 * we collect will actually be the "ghost" triangles that define the
 * exterior to the triangulation. Ghost triangles are those that include
 * the so-called "ghost" vertex.  Tinfour manages the ghost vertex using
 * a null vertex.  Thus it would be possible to collect triangles which
 * contain null vertices.   In order to avoid passing null vertices to
 * the accept() method, Tinfour must screen for this condition.
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Utils;

using Tinfour.Core.Common;

/// <summary>
///     Provides a utility for collecting triangles from a TIN.
/// </summary>
public static class TriangleCollector
{
    /// <summary>
    ///     Used to extract the low-order bit via a bitwise AND.
    /// </summary>
    private const int Bit1 = 0x01;

    /// <summary>
    ///     Number of shifts to divide an integer by 32.
    /// </summary>
    private const int DivBy32 = 5;

    /// <summary>
    ///     Number of bits in an integer.
    /// </summary>
    private const int IntBits = 32;

    /// <summary>
    ///     Used to perform a modulus 32 operation on an integer through a bitwise AND.
    /// </summary>
    private const int ModBy32 = 0x1f;

    /// <summary>
    ///     Identify all valid triangles in the specified TIN and
    ///     provide them to the application-supplied Consumer.
    ///     Triangles are provided as instances of the SimpleTriangle class. If the TIN
    ///     has not been bootstrapped, this routine exits without further processing.
    ///     This routine will not call the accept method for "ghost" triangles
    ///     (those triangles that include the ghost vertex).
    /// </summary>
    /// <param name="tin">A valid TIN</param>
    /// <param name="consumer">A valid consumer.</param>
    public static void VisitSimpleTriangles(IIncrementalTin tin, Action<SimpleTriangle> consumer)
    {
        if (!tin.IsBootstrapped()) return;

        foreach (var t in tin.GetTriangles()) consumer(t);
    }

    /// <summary>
    ///     Identify all valid triangles in the specified TIN and
    ///     provide them to the application-supplied Consumer.
    ///     Triangles are provided as an array of three vertices
    ///     given in clockwise order. If the TIN
    ///     has not been bootstrapped, this routine exits without further processing.
    ///     This routine will not call the accept method for "ghost" triangles
    ///     (those triangles that include the ghost vertex).
    /// </summary>
    /// <param name="tin">A valid TIN</param>
    /// <param name="consumer">A valid consumer.</param>
    public static void VisitTriangles(IIncrementalTin tin, Action<IVertex[]> consumer)
    {
        if (!tin.IsBootstrapped()) return;

        foreach (var t in tin.GetTriangles())
        {
            var v = new IVertex[3];
            v[0] = t.GetVertexA();
            v[1] = t.GetVertexB();
            v[2] = t.GetVertexC();
            consumer(v);
        }
    }

    /// <summary>
    ///     Traverses the TIN, visiting all triangles that are members of a constrained
    ///     region. As triangles are identified, this method calls the accept method of
    ///     a consumer. If the TIN has not been bootstrapped, this routine exits
    ///     without further processing.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         All triangles produced by this method are valid (non-ghost) triangles
    ///         with valid, non-null vertices.
    ///     </para>
    ///     <para>
    ///         <strong>Note:</strong> If no region-based constraints have been
    ///         added to the Delaunay triangulation, then none of the triangles in
    ///         the TIN are treated as being constrained. This method will
    ///         exit without further processing.
    ///     </para>
    /// </remarks>
    /// <param name="tin">A valid instance</param>
    /// <param name="consumer">An application-specific consumer.</param>
    public static void VisitTrianglesConstrained(
        IIncrementalTin tin,
        Action<IVertex[]> consumer,
        bool includeBorders = false)
    {
        if (tin.IsBootstrapped())
            foreach (var t in tin.GetTriangles())
            {
                // Iterate through the edges of the triangle to check
                // if any of them have a constraint
                var edgeA = t.GetEdgeA();
                var edgeB = t.GetEdgeB();
                var edgeC = t.GetEdgeC();

                var hasConstraintRegion = edgeA.IsConstraintRegionInterior() || edgeB.IsConstraintRegionInterior()
                                                                             || edgeC.IsConstraintRegionInterior();

                // by default don't include border edges
                if (includeBorders)
                    hasConstraintRegion = hasConstraintRegion || edgeA.IsConstraintRegionBorder()
                                                              || edgeB.IsConstraintRegionBorder()
                                                              || edgeC.IsConstraintRegionBorder();

                if (hasConstraintRegion)
                {
                    var v = new IVertex[3];
                    v[0] = t.GetVertexA();
                    v[1] = t.GetVertexB();
                    v[2] = t.GetVertexC();
                    consumer(v);
                }
            }
    }

    /// <summary>
    ///     Traverses the interior of a constrained region, visiting the triangles in
    ///     its interior. As triangles are identified, this method calls the accept
    ///     method of a consumer.
    /// </summary>
    /// <param name="constraint">
    ///     A valid instance defining a constrained region that has
    ///     been added to a TIN.
    /// </param>
    /// <param name="consumer">An application-specific consumer.</param>
    public static void VisitTrianglesForConstrainedRegion(IConstraint constraint, Action<IVertex[]> consumer)
    {
        var tin = constraint.GetManagingTin();
        if (tin == null)
            throw new ArgumentException("Constraint is not under TIN management");
        if (!constraint.DefinesConstrainedRegion())
            throw new ArgumentException("Constraint does not define constrained region");
        var linkEdge = constraint.GetConstraintLinkingEdge();
        if (linkEdge == null)
            throw new ArgumentException("Constraint does not have linking edge");

        var maxMapIndex = tin.GetMaximumEdgeAllocationIndex() + 2;
        var mapSize = (maxMapIndex + IntBits - 1) / IntBits;
        var map = new int[mapSize];

        if (GetMarkBit(map, linkEdge) == 0) VisitTrianglesUsingStack(linkEdge, map, consumer);
    }

    /// <summary>
    ///     Gets the edge mark bit. Each edge will have two mark bits, one for the base
    ///     reference and one for its dual.
    /// </summary>
    /// <param name="map">
    ///     An array at least as large as the largest edge index divided by
    ///     32, rounded up.
    /// </param>
    /// <param name="edge">A valid edge</param>
    /// <returns>If the edge is marked, a non-zero value; otherwise, a zero.</returns>
    private static int GetMarkBit(int[] map, IQuadEdge edge)
    {
        var index = edge.GetIndex();
        return (map[index >> DivBy32] >> (index & ModBy32)) & Bit1;
    }

    /// <summary>
    ///     Set the mark bit for an edge to 1. Each edge will have two mark bits, one
    ///     for the base reference and one for its dual.
    /// </summary>
    /// <param name="map">
    ///     An array at least as large as the largest edge index divided by
    ///     32, rounded up.
    /// </param>
    /// <param name="edge">A valid edge</param>
    private static void SetMarkBit(int[] map, IQuadEdge edge)
    {
        var index = edge.GetIndex();
        map[index >> DivBy32] |= Bit1 << (index & ModBy32);
    }

    private static void VisitTrianglesUsingStack(IQuadEdge firstEdge, int[] map, Action<IVertex[]> consumer)
    {
        var deque = new Stack<IQuadEdge>();
        deque.Push(firstEdge);
        while (deque.Count > 0)
        {
            var e = deque.Pop();
            if (GetMarkBit(map, e) == 0)
            {
                var f = e.GetForward();
                var r = e.GetReverse();
                SetMarkBit(map, e);
                SetMarkBit(map, f);
                SetMarkBit(map, r);

                // The rationale for the null check is given in the
                // discussion at the beginning of this file.
                var a = e.GetA();
                var b = f.GetA();
                var c = r.GetA();
                if (!a.IsNullVertex() && !b.IsNullVertex() && !c.IsNullVertex()) consumer(new[] { a, b, c });

                var df = f.GetDual();
                var dr = r.GetDual();
                if (GetMarkBit(map, df) == 0 && !f.IsConstraintRegionBorder()) deque.Push(df);
                if (GetMarkBit(map, dr) == 0 && !r.IsConstraintRegionBorder()) deque.Push(dr);
            }
        }
    }
}