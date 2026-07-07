/*
 * Copyright 2015 Gary W. Lucas.
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
 * 06/2015  G. Lucas     Adapted from ProtoTIN implementation of TriangleManager
 * 08/2025  M. Fender    Ported to C#
 * 07/2026  M. Fender    Rebuilt as a facade over the SoA EdgeStore (#832)
 *
 * Notes:
 * The pool's public surface is retained, but storage is now the int-handle
 * structure-of-arrays EdgeStore rather than pages of QuadEdge objects.
 * Handles are stable for the lifetime of an allocation (free-list reuse,
 * no swap-compaction), so edge indices no longer change on deallocation of
 * other edges. Page-related accessors report synthetic values derived from
 * store capacity.
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Edge;

using System.Collections;
using System.Diagnostics;

using Tinfour.Core.Common;

/// <summary>
///     Manages the allocation, deletion, and reuse of edges for a TIN,
///     backed by the int-handle structure-of-arrays EdgeStore.
/// </summary>
/// <remarks>
///     <strong>Note:</strong> This class is not thread-safe for mutation.
/// </remarks>
public class EdgePool : IEnumerable<IQuadEdge>, IDisposable
{
    /// <summary>
    ///     The nominal number of edges in a reporting "page" (retained for
    ///     compatibility with the page-based pool's diagnostics).
    /// </summary>
    private const int NominalPageSize = 1024;

    /// <summary>
    ///     Maps constraint indices to constraint objects for linear constraints.
    /// </summary>
    private readonly Dictionary<int, IConstraint> _linearConstraintMap = new();

    private readonly EdgeStore _store = new();

    private bool _isDisposed;

    private int _nAllocationOperations;

    private int _nFreeOperations;

    /// <summary>
    ///     Gets the structure-of-arrays store backing this pool.
    /// </summary>
    internal EdgeStore Store => _store;

    /// <summary>
    ///     Adds the specified constraint to the linear constraint map.
    /// </summary>
    /// <param name="edge">A valid edge instance</param>
    /// <param name="constraint">A valid constraint instance</param>
    public void AddLinearConstraintToMap(IQuadEdge edge, IConstraint constraint)
    {
        var index = edge.GetIndex();
        _linearConstraintMap[index] = constraint;
        _linearConstraintMap[index ^ 1] = constraint;
    }

    /// <summary>
    ///     Allocates a QuadEdge with the specified vertices.
    /// </summary>
    /// <param name="a">The first vertex</param>
    /// <param name="b">The second vertex</param>
    /// <returns>A valid QuadEdge under management of this collection</returns>
    public IQuadEdge AllocateEdge(IVertex a, IVertex b)
    {
        ThrowIfDisposed();

        var h = _store.AllocatePair();
        _nAllocationOperations++;
        _store.SetVertices(h, a, b);
        return _store.Wrap(h);
    }

    /// <summary>
    ///     Allocates a QuadEdge with undefined vertices.
    /// </summary>
    /// <returns>A valid QuadEdge with null vertices</returns>
    public IQuadEdge AllocateUndefinedEdge()
    {
        ThrowIfDisposed();

        var h = _store.AllocatePair();
        _nAllocationOperations++;
        return _store.Wrap(h);
    }

    /// <summary>
    ///     Allocates an edge pair with the specified vertices and returns its
    ///     base handle. Handle-native counterpart of AllocateEdge for the hot
    ///     build paths; skips the disposed check (internal callers only operate
    ///     on live pools).
    /// </summary>
    internal int AllocateEdgeHandle(IVertex a, IVertex b)
    {
        var h = _store.AllocatePair();
        _nAllocationOperations++;
        _store.SetVertices(h, a, b);
        return h;
    }

    /// <summary>
    ///     Deallocates the pair containing the handle. Handle-native
    ///     counterpart of DeallocateEdge for the hot build paths.
    /// </summary>
    internal void DeallocateEdgeHandle(int h)
    {
        var baseIndex = h & ~1;
        _linearConstraintMap.Remove(baseIndex);
        _linearConstraintMap.Remove(baseIndex | 1);
        _store.DeallocatePair(baseIndex);
        _nFreeOperations++;
    }

    /// <summary>
    ///     Deallocates all edges, returning them to the free state.
    ///     Does not release storage.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();

        _store.Clear();
        _nAllocationOperations = 0;
        _nFreeOperations = 0;
        _linearConstraintMap.Clear();
    }

    /// <summary>
    ///     Deallocates the QuadEdge, returning it to the edge pool.
    /// </summary>
    /// <param name="edge">A valid QuadEdge to deallocate</param>
    public void DeallocateEdge(IQuadEdge edge)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(edge);

        var baseIndex = edge.GetBaseIndex();
        if (!_store.IsAllocated(baseIndex))
            throw new InvalidOperationException(
                $"Edge with base index {baseIndex} is not allocated in this pool");

        _linearConstraintMap.Remove(baseIndex);
        _linearConstraintMap.Remove(baseIndex ^ 1);

        _store.DeallocatePair(baseIndex);
        _nFreeOperations++;
    }

    /// <summary>
    ///     Disposes of this EdgePool instance.
    /// </summary>
    public void Dispose()
    {
        if (!_isDisposed)
        {
            _store.Clear();
            _linearConstraintMap.Clear();
            _isDisposed = true;
        }
    }

    /// <summary>
    ///     Performs an edge flip on the specified edge, replacing it with the
    ///     opposite diagonal of the convex quadrilateral formed by its adjacent triangles.
    /// </summary>
    /// <param name="edge">A valid interior edge with non-ghost neighbors</param>
    public void FlipEdge(IQuadEdge edge)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(edge);

        var s = _store;
        var e = ((QuadEdge)edge).GetHandle();
        var ed = e ^ 1;

        // Edges around the two adjacent triangles
        var ef = s.Forward(e); // b->c
        var er = s.Reverse(e); // c->a
        var edf = s.Forward(ed); // a->d
        var edr = s.Reverse(ed); // d->b

        var c = s.VertexB(ef);
        var d = s.VertexB(edf);
        if (c.IsNullVertex() || d.IsNullVertex())

            // Cannot flip if one side is ghost
            return;

        // Pre-conditions (DEBUG only)
        AssertReciprocity(s, e);
        AssertReciprocity(s, ed);
        AssertReciprocity(s, ef);
        AssertReciprocity(s, er);
        AssertReciprocity(s, edf);
        AssertReciprocity(s, edr);

        // Clear stale constraint region flags before vertex reassignment.
        // The edge is being flipped to a new diagonal, so its old region
        // membership (interior/border) is no longer valid.
        s.SetConstraintBits(
            e,
            s.ConstraintBits(e) & ~(QuadEdgeConstants.ConstraintRegionBorderFlag
                                    | QuadEdgeConstants.ConstraintRegionInteriorFlag
                                    | QuadEdgeConstants.ConstraintLowerIndexMask));

        // Reassign vertices so that e = c->d and ed = d->c
        s.SetVertices(e, c, d);

        // Note: The only caller is ExtendTin(), which must handle constraint flag
        // re-assignment after the flip loop completes.

        // Rewire forward cycles for the two new triangles
        // Triangle (c, d, b): e(c->d) -> edr(d->b) -> ef(b->c)
        s.SetForward(e, edr);
        s.SetForward(edr, ef);
        s.SetForward(ef, e);

        // Triangle (d, c, a): ed(d->c) -> er(c->a) -> edf(a->d)
        s.SetForward(ed, er);
        s.SetForward(er, edf);
        s.SetForward(edf, ed);

        // Post-conditions (DEBUG only)
        AssertReciprocity(s, e);
        AssertReciprocity(s, ed);
        AssertReciprocity(s, ef);
        AssertReciprocity(s, er);
        AssertReciprocity(s, edf);
        AssertReciprocity(s, edr);
    }

    /// <summary>
    ///     Gets the number of edges in the collection.
    /// </summary>
    /// <returns>The edge count</returns>
    public int GetEdgeCount()
    {
        return _store.AllocatedCount;
    }

    /// <summary>
    ///     Gets a list of edges currently stored in the collection.
    /// </summary>
    /// <returns>A valid, potentially empty list of edges</returns>
    public IList<IQuadEdge> GetEdges()
    {
        var edgeList = new List<IQuadEdge>(_store.AllocatedCount);
        if (_isDisposed) return edgeList;

        for (var pair = 0; pair < _store.PairHighWater; pair++)
        {
            var h = pair << 1;
            if (_store.IsAllocated(h)) edgeList.Add(_store.Wrap(h));
        }

        return edgeList;
    }

    /// <summary>
    ///     Returns an enumerator that iterates through the edge collection.
    /// </summary>
    /// <returns>An enumerator for the edge collection</returns>
    public IEnumerator<IQuadEdge> GetEnumerator()
    {
        return GetIterator(true);
    }

    /// <summary>
    ///     Constructs an iterator that will optionally skip ghost edges.
    /// </summary>
    /// <param name="includeGhostEdges">Indicates that ghost edges are to be included</param>
    /// <returns>A valid instance of an iterator</returns>
    public IEnumerator<IQuadEdge> GetIterator(bool includeGhostEdges)
    {
        return EnumerateBaseEdges(includeGhostEdges).GetEnumerator();
    }

    /// <summary>
    ///     Gets the linear constraint associated with the edge, if any.
    /// </summary>
    /// <param name="edge">A valid edge instance</param>
    /// <returns>If a linear constraint is associated with the edge, a valid instance; otherwise null</returns>
    public IConstraint? GetLinearConstraint(IQuadEdge edge)
    {
        if (edge.IsConstraintLineMember())
        {
            _linearConstraintMap.TryGetValue(edge.GetIndex(), out var constraint);
            return constraint;
        }

        return null;
    }

    /// <summary>
    ///     Gets the maximum value of an edge index currently allocated.
    /// </summary>
    /// <returns>A positive number or zero if the pool is unallocated</returns>
    public int GetMaximumAllocationIndex()
    {
        return _store.PairHighWater * 2;
    }

    /// <summary>
    ///     Gets the number of nominal pages spanned by the store's capacity.
    /// </summary>
    /// <returns>A value of 1 or greater</returns>
    public int GetPageCount()
    {
        return Math.Max(1, (_store.PairCapacity + NominalPageSize - 1) / NominalPageSize);
    }

    /// <summary>
    ///     Gets the nominal page size used for diagnostics.
    /// </summary>
    /// <returns>A value of 1 or greater, usually 1024</returns>
    public int GetPageSize()
    {
        return NominalPageSize;
    }

    /// <summary>
    ///     Gets the first valid, non-ghost QuadEdge in the collection.
    /// </summary>
    /// <returns>For a non-empty collection, a valid QuadEdge; otherwise null</returns>
    public IQuadEdge? GetStartingEdge()
    {
        for (var pair = 0; pair < _store.PairHighWater; pair++)
        {
            var h = pair << 1;
            if (_store.IsAllocated(h)
                && !_store.VertexA(h).IsNullVertex()
                && !_store.VertexB(h).IsNullVertex())
                return _store.Wrap(h);
        }

        return null;
    }

    /// <summary>
    ///     Gets the first ghost edge in the collection.
    /// </summary>
    /// <returns>A ghost edge if found; otherwise null</returns>
    public IQuadEdge? GetStartingGhostEdge()
    {
        for (var pair = 0; pair < _store.PairHighWater; pair++)
        {
            var h = pair << 1;
            if (_store.IsAllocated(h) && _store.VertexB(h).IsNullVertex()) return _store.Wrap(h);
        }

        return null;
    }

    /// <summary>
    ///     Ensures that at least the specified number of edges can be
    ///     allocated without further storage growth.
    /// </summary>
    /// <param name="n">The number of edges to allocate</param>
    public void PreAllocateEdges(int n)
    {
        ThrowIfDisposed();
        _store.EnsureFreeCapacity(n);
    }

    /// <summary>
    ///     Gets the number of edges currently stored in the collection.
    /// </summary>
    /// <returns>An integer value of zero or more</returns>
    public int Size()
    {
        return _store.AllocatedCount;
    }

    /// <summary>
    ///     Splits an edge by inserting a new vertex, creating two edges where there was one.
    ///     The edge e(a,b) becomes p(a,m) and e(m,b), with appropriate topology adjustments.
    ///     Constraint flags and other attributes are preserved during the split.
    /// </summary>
    /// <param name="e">The edge to be split</param>
    /// <param name="m">The insertion vertex</param>
    /// <returns>A new edge p(a,m); the original edge e becomes e(m,b)</returns>
    public IQuadEdge SplitEdge(QuadEdge e, IVertex m)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(m);

        var s = _store;
        var eh = e.GetHandle();
        var b = eh & ~1; // base reference
        var d = eh ^ 1; // dual
        var eR = s.Reverse(eh);
        var dF = s.Forward(d);

        // Save the original starting vertex
        var a = s.VertexA(eh);

        // Modify original edge to start at the insertion point
        s.SetVertexA(eh, m);

        // Create new edge from original start to insertion point
        var p = s.AllocatePair();
        _nAllocationOperations++;
        s.SetVertices(p, a, m);
        var q = p ^ 1;

        // Establish proper forward/reverse links
        s.SetForward(p, eh);
        s.SetReverse(p, eR);

        s.SetForward(q, dF);
        s.SetReverse(q, d);

        s.SetForward(eR, p);
        s.SetReverse(dF, q);

        // Preserve the constraint flags of the split edge on the new edge
        s.SetConstraintBits(p, s.ConstraintBits(b));

        var pWrapped = s.Wrap(p);

        // Handle region border constraints if present.
        // The bit copy above preserves the constraint flags, but we explicitly
        // set the border index to ensure it's correct on the new edge.
        if (e.IsConstraintRegionBorder())
        {
            var borderIdx = e.GetConstraintBorderIndex();
            if (borderIdx >= 0) pWrapped.SetConstraintBorderIndex(borderIdx);
        }

        // Handle line constraints if present
        if (e.IsConstraintLineMember())
        {
            if (_linearConstraintMap.TryGetValue(e.GetIndex(), out var constraint))
                AddLinearConstraintToMap(pWrapped, constraint);
        }

        return pWrapped;
    }

    /// <summary>
    ///     Returns a string representation of the EdgePool.
    /// </summary>
    /// <returns>A string containing edge pool statistics</returns>
    public override string ToString()
    {
        return $"nEdges={_store.AllocatedCount}, nPages={GetPageCount()}, nFree={_store.FreeCapacity}";
    }

    /// <summary>
    ///     Returns an enumerator that iterates through the edge collection.
    /// </summary>
    /// <returns>An enumerator for the edge collection</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    [Conditional("DEBUG")]
    private static void AssertReciprocity(EdgeStore s, int h)
    {
        Debug.Assert(s.Forward(h) >= 0, "Edge forward link is unset");
        Debug.Assert(s.Reverse(h) >= 0, "Edge reverse link is unset");
        if (s.Forward(h) >= 0)
            Debug.Assert(s.Reverse(s.Forward(h)) == h, "Forward.reverse does not point back to edge");
        if (s.Reverse(h) >= 0)
            Debug.Assert(s.Forward(s.Reverse(h)) == h, "Reverse.forward does not point back to edge");
    }

    private IEnumerable<IQuadEdge> EnumerateBaseEdges(bool includeGhostEdges)
    {
        for (var pair = 0; pair < _store.PairHighWater; pair++)
        {
            var h = pair << 1;
            if (!_store.IsAllocated(h)) continue;

            if (!includeGhostEdges
                && (_store.VertexA(h).IsNullVertex() || _store.VertexB(h).IsNullVertex()))
                continue;

            yield return _store.Wrap(h);
        }
    }

    /// <summary>
    ///     Throws an ObjectDisposedException if this instance has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(EdgePool));
    }

    #region Serialization Support

    /// <summary>
    ///     Gets all allocated edges as QuadEdge instances in ascending base-index
    ///     order (for serialization).
    /// </summary>
    internal IEnumerable<QuadEdge> GetAllocatedEdgesInOrder()
    {
        for (var pair = 0; pair < _store.PairHighWater; pair++)
        {
            var h = pair << 1;
            if (_store.IsAllocated(h)) yield return _store.Wrap(h);
        }
    }

    /// <summary>
    ///     Gets the number of allocated edges (for serialization).
    /// </summary>
    internal int GetAllocatedCount() => _store.AllocatedCount;

    /// <summary>
    ///     Pre-allocates the specified number of edges for deserialization.
    ///     After calling this, the edges will have sequential base indices (0, 2, 4, ...).
    /// </summary>
    /// <param name="count">Number of edges to allocate</param>
    /// <returns>Array of allocated edges in order</returns>
    internal QuadEdge[] AllocateEdgesForDeserialization(int count)
    {
        ThrowIfDisposed();

        _store.EnsureFreeCapacity(count);

        var edges = new QuadEdge[count];
        for (var i = 0; i < count; i++) edges[i] = (QuadEdge)AllocateUndefinedEdge();

        return edges;
    }

    /// <summary>
    ///     Gets an edge by its base index (for deserialization link resolution).
    ///     The base index must be an even number (0, 2, 4, ...).
    /// </summary>
    /// <param name="baseIndex">The base index of the edge (must be even)</param>
    /// <returns>The edge at the specified index, or null if out of range</returns>
    internal QuadEdge? GetEdgeByBaseIndex(int baseIndex)
    {
        if (baseIndex < 0 || (baseIndex & 1) != 0) return null;
        if (!_store.IsAllocated(baseIndex)) return null;

        return _store.Wrap(baseIndex);
    }

    /// <summary>
    ///     Adds a mapping from edge index to linear constraint (for deserialization).
    /// </summary>
    internal void AddLinearConstraintMapping(int edgeIndex, IConstraint constraint)
    {
        _linearConstraintMap[edgeIndex] = constraint;
    }

    #endregion
}
