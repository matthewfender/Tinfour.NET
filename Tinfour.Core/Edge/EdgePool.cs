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
 * 08/2025 M.Fender     Ported to C#
 *
 * Notes:
 * The memory in this container is organized into pages, each page
 * holding a fixed number of Edges. Some edges are committed to the TIN,
 * others are in an "available state". The pages include links so that the
 * container can maintain a single-direction linked list of pages which have
 * at least one QuadEdge in the "available" state.
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Edge;

using System.Collections;
using System.Diagnostics;

using Tinfour.Core.Common;

/// <summary>
///     Provides an object-pool implementation that manages the allocation,
///     deletion, and reuse of QuadEdges for efficient memory management.
/// </summary>
/// <remarks>
///     This class uses a very old-school approach to minimize the frequency
///     with which objects are garbage collected. Edges are extensively allocated
///     and freed as the TIN is built. This design provides better performance
///     than simple construction and disposal patterns.
///     <strong>Note:</strong> This class is not thread-safe.
/// </remarks>
public class EdgePool : IEnumerable<IQuadEdge>, IDisposable
{
    /// <summary>
    ///     The number of edges in an edge-pool page.
    /// </summary>
    private const int EdgePoolPageSize = 1024;

    /// <summary>
    ///     Maps constraint indices to constraint objects for linear constraints.
    /// </summary>
    private readonly Dictionary<int, IConstraint> _linearConstraintMap;

    /// <summary>
    ///     The number of edges stored in a page.
    /// </summary>
    private readonly int _pageSize;

    /// <summary>
    ///     The number of edge indices for a page (pageSize * 2).
    /// </summary>
    private readonly int _pageSize2;

    /// <summary>
    ///     Indicates whether this instance has been disposed.
    /// </summary>
    private bool _isDisposed;

    /// <summary>
    ///     Number of edges currently allocated.
    /// </summary>
    private int _nAllocated;

    /// <summary>
    ///     Total number of allocation operations performed.
    /// </summary>
    private int _nAllocationOperations;

    /// <summary>
    ///     The next page that includes available edges. This reference is never
    ///     null as there is always at least one page with at least one free edge.
    /// </summary>
    private EdgePage? _nextAvailablePage;

    /// <summary>
    ///     Number of edges currently free.
    /// </summary>
    private int _nFree;

    /// <summary>
    ///     Total number of free operations performed.
    /// </summary>
    private int _nFreeOperations;

    /// <summary>
    ///     Array of pages containing edge objects.
    /// </summary>
    private EdgePage[] _pages;

    /// <summary>
    ///     Initializes a new instance of the EdgePool class.
    /// </summary>
    public EdgePool()
    {
        _pageSize = EdgePoolPageSize;
        _pageSize2 = EdgePoolPageSize * 2;
        _pages = new EdgePage[1];
        _pages[0] = new EdgePage(0, _pageSize, _pageSize2);
        _nextAvailablePage = _pages[0];
        _nextAvailablePage.InitializeEdges();
        _nFree = _pageSize;
        _linearConstraintMap = new Dictionary<int, IConstraint>();
    }

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

        var page = _nextAvailablePage ?? throw new InvalidOperationException("No available pages");
        var edge = page.AllocateEdge();

        if (page.IsFullyAllocated())
        {
            _nextAvailablePage = page.NextPage;
            if (_nextAvailablePage == null) AllocatePage();
        }

        _nFree--;
        _nAllocated++;
        _nAllocationOperations++;

        edge.SetVertices(a, b);
        return edge;
    }

    /// <summary>
    ///     Allocates a QuadEdge with undefined vertices.
    /// </summary>
    /// <returns>A valid QuadEdge with null vertices</returns>
    public IQuadEdge AllocateUndefinedEdge()
    {
        ThrowIfDisposed();

        var page = _nextAvailablePage ?? throw new InvalidOperationException("No available pages");
        var edge = page.AllocateEdge();

        if (page.IsFullyAllocated())
        {
            _nextAvailablePage = page.NextPage;
            if (_nextAvailablePage == null) AllocatePage();
        }

        _nFree--;
        _nAllocated++;
        _nAllocationOperations++;

        return edge;
    }

    /// <summary>
    ///     Deallocates all edges, returning them to the free list.
    ///     Does not delete any existing objects.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();

        foreach (var page in _pages) page.Clear();

        _nAllocated = 0;
        _nFree = _pages.Length * _pageSize;
        _nAllocationOperations = 0;
        _nFreeOperations = 0;
        _nextAvailablePage = _pages[0];

        for (var i = 0; i < _pages.Length - 1; i++) _pages[i].NextPage = _pages[i + 1];

        _pages[_pages.Length - 1].NextPage = null;

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

        // Calculate page index from base edge index, following Java logic exactly
        var baseIndex = edge.GetBaseIndex();
        var pageIndex = baseIndex / _pageSize2;

        // Add bounds checking to prevent array access exceptions
        if (pageIndex < 0 || pageIndex >= _pages.Length)
            throw new InvalidOperationException(
                $"Invalid page index {pageIndex} calculated from edge index {baseIndex}. "
                + $"Edge pool has {_pages.Length} pages, _pageSize2={_pageSize2}");

        var page = _pages[pageIndex];

        if (page.IsFullyAllocated())
        {
            // Since it will no longer be fully allocated, add it to the linked list
            page.NextPage = _nextAvailablePage;
            _nextAvailablePage = page;
        }

        page.DeallocateEdge((QuadEdge)edge, _linearConstraintMap);
        _nAllocated--;
        _nFree++;
        _nFreeOperations++;
    }

    /// <summary>
    ///     Disposes of this EdgePool instance.
    /// </summary>
    public void Dispose()
    {
        if (!_isDisposed)
        {
            _nextAvailablePage = null;
            if (_pages != null)
            {
                foreach (var page in _pages) page?.Dispose();

                _pages = null!;
            }

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

        var e = (QuadEdge)edge;
        var ed = (QuadEdge)e.GetDual();

        // Edges around the two adjacent triangles
        var ef = (QuadEdge)e.GetForward(); // b->c
        var er = (QuadEdge)e.GetReverse(); // c->a
        var edf = (QuadEdge)ed.GetForward(); // a->d
        var edr = (QuadEdge)ed.GetReverse(); // d->b

        var c = ef.GetB();
        var d = edf.GetB();
        if (c.IsNullVertex() || d.IsNullVertex())

            // Cannot flip if one side is ghost
            return;

        // Pre-conditions (DEBUG only)
        AssertReciprocity(e);
        AssertReciprocity(ed);
        AssertReciprocity(ef);
        AssertReciprocity(er);
        AssertReciprocity(edf);
        AssertReciprocity(edr);

        // Reassign vertices so that e = c->d and ed = d->c
        e.SetVertices(c, d);
        ed.SetVertices(d, c);

        // Rewire forward cycles for the two new triangles
        // Triangle (c, d, b): e(c->d) -> edr(d->b) -> ef(b->c)
        e.SetForward(edr);
        edr.SetForward(ef);
        ef.SetForward(e);

        // Triangle (d, c, a): ed(d->c) -> er(c->a) -> edf(a->d)
        ed.SetForward(er);
        er.SetForward(edf);
        edf.SetForward(ed);

        // Post-conditions (DEBUG only)
        AssertReciprocity(e);
        AssertReciprocity(ed);
        AssertReciprocity(ef);
        AssertReciprocity(er);
        AssertReciprocity(edf);
        AssertReciprocity(edr);
    }

    /// <summary>
    ///     Gets the number of edges in the collection.
    /// </summary>
    /// <returns>The edge count</returns>
    public int GetEdgeCount()
    {
        return _nAllocated;
    }

    /// <summary>
    ///     Gets a list of edges currently stored in the collection.
    /// </summary>
    /// <returns>A valid, potentially empty list of edges</returns>
    public IList<IQuadEdge> GetEdges()
    {
        var edgeList = new List<IQuadEdge>(_nAllocated);

        if (_pages != null)
            foreach (var page in _pages)
                if (page != null)
                    for (var j = 0; j < page.NAllocated; j++)
                        edgeList.Add(page.GetEdge(j));

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
        return new EdgeIterator(_pages, includeGhostEdges);
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
        for (var pageIndex = _pages.Length - 1; pageIndex >= 0; pageIndex--)
        {
            var page = _pages[pageIndex];
            if (page.NAllocated > 0) return page.PageId * _pageSize2 + page.NAllocated * 2;
        }

        return 0;
    }

    /// <summary>
    ///     Gets the number of pages currently allocated.
    /// </summary>
    /// <returns>A value of 1 or greater</returns>
    public int GetPageCount()
    {
        return _pages.Length;
    }

    /// <summary>
    ///     Gets the number of edges allocated in a page.
    /// </summary>
    /// <returns>A value of 1 or greater, usually 1024</returns>
    public int GetPageSize()
    {
        return _pageSize;
    }

    /// <summary>
    ///     Gets the first valid, non-ghost QuadEdge in the collection.
    /// </summary>
    /// <returns>For a non-empty collection, a valid QuadEdge; otherwise null</returns>
    public IQuadEdge? GetStartingEdge()
    {
        foreach (var page in _pages)
            if (page.NAllocated > 0)
                for (var i = 0; i < page.NAllocated; i++)
                {
                    var edge = page.GetEdge(i);
                    if (!edge.GetA().IsNullVertex() && !edge.GetB().IsNullVertex()) return edge;
                }

        return null;
    }

    /// <summary>
    ///     Gets the first ghost edge in the collection.
    /// </summary>
    /// <returns>A ghost edge if found; otherwise null</returns>
    public IQuadEdge? GetStartingGhostEdge()
    {
        foreach (var page in _pages)
            if (page.NAllocated > 0)
                for (var i = 0; i < page.NAllocated; i++)
                {
                    var edge = page.GetEdge(i);
                    if (edge.GetB().IsNullVertex()) return edge;
                }

        return null;
    }

    /// <summary>
    ///     Synchronous version of PreAllocateEdgesAsync for compatibility.
    /// </summary>
    /// <param name="n">The number of edges to allocate</param>
    public void PreAllocateEdges(int n)
    {
        if (_nFree >= n) return;

        var availablePageId = _nextAvailablePage?.PageId ?? 0;
        var edgesNeeded = n - _nFree;
        var pagesNeeded = (edgesNeeded + _pageSize - 1) / _pageSize;
        var oldLen = _pages.Length;
        var nP = oldLen + pagesNeeded;

        AllocatePages(oldLen, nP, availablePageId);
    }

    /// <summary>
    ///     Gets the number of edges currently stored in the collection.
    /// </summary>
    /// <returns>An integer value of zero or more</returns>
    public int Size()
    {
        return _nAllocated;
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

        Debug.WriteLine(
            $"SplitEdge: Splitting edge {e.GetIndex()} ({e.GetA().GetIndex()}->{e.GetB().GetIndex()}) at vertex {m.GetIndex()}");

        // Get references to key edges
        var b = (QuadEdge)e.GetBaseReference();
        var d = (QuadEdge)e.GetDual();
        var eR = (QuadEdge)e.GetReverse();
        var dF = (QuadEdge)d.GetForward();

        Debug.WriteLine(
            $"SplitEdge: Base edge: {b.GetIndex()}, Dual: {d.GetIndex()}, Reverse: {eR.GetIndex()}, DualForward: {dF.GetIndex()}");

        // Save the original starting vertex
        var a = e.GetA();

        // Modify original edge to start at the insertion point
        e.SetA(m);

        // Create new edge from original start to insertion point
        var p = (QuadEdge)AllocateEdge(a, m);
        var q = (QuadEdge)p.GetDual();

        Debug.WriteLine(
            $"SplitEdge: Created new edge p={p.GetIndex()} ({a.GetIndex()}->{m.GetIndex()}) and dual q={q.GetIndex()}");

        // Fix #2: Establish proper forward/reverse links
        Debug.WriteLine("SplitEdge: Setting forward/reverse links");
        Debug.WriteLine("  p.Forward = e, p.Reverse = eR");
        p.SetForward(e);
        p.SetReverse(eR);

        Debug.WriteLine("  q.Forward = dF, q.Reverse = d");
        q.SetForward(dF);
        q.SetReverse(d);

        Debug.WriteLine("  eR.Forward = p, dF.Reverse = q");
        eR.SetForward(p);
        dF.SetReverse(q);

        p._dual._index = b._dual._index;

        // Handle region border constraints if present
        if ((e.GetIndex() & 1) != 0 && e.IsConstraintRegionBorder())
        {
            Debug.WriteLine("SplitEdge: Copying constraint border indices");
            p.SetConstraintBorderIndex(e.GetConstraintBorderIndex());
            q.SetConstraintBorderIndex(b.GetConstraintBorderIndex());
        }

        // Handle line constraints if present
        if (e.IsConstraintLineMember())
        {
            Debug.WriteLine("SplitEdge: Handling line constraint");
            if (_linearConstraintMap.TryGetValue(e.GetIndex(), out var constraint))
                AddLinearConstraintToMap(p, constraint);
        }

        Debug.WriteLine($"SplitEdge: Split complete. New edge p={p.GetIndex()}, modified edge e={e.GetIndex()}");

        // Validate triangle forward linkage after split
        ValidateConnections(p, e, eR, q, d, dF, "after split");

        return p;
    }

    /// <summary>
    ///     Returns a string representation of the EdgePool.
    /// </summary>
    /// <returns>A string containing edge pool statistics</returns>
    public override string ToString()
    {
        return $"nEdges={_nAllocated}, nPages={_pages?.Length ?? 0}, nFree={_nFree}";
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
    private static void AssertReciprocity(QuadEdge e)
    {
        // Forward/reverse must exist and be reciprocal
        Debug.Assert(e._f != null, "Edge forward link is null");
        Debug.Assert(e._r != null, "Edge reverse link is null");
        if (e._f != null) Debug.Assert(e._f._r == e, "Forward.reverse does not point back to edge");
        if (e._r != null) Debug.Assert(e._r._f == e, "Reverse.forward does not point back to edge");

        // Dual should never be null
        Debug.Assert(e._dual != null, "Dual link is null");
    }

    /// <summary>
    ///     Allocates a single page.
    /// </summary>
    private void AllocatePage()
    {
        var oldLength = _pages.Length;
        var newPages = new EdgePage[oldLength + 1];
        Array.Copy(_pages, 0, newPages, 0, _pages.Length);
        newPages[oldLength] = new EdgePage(oldLength, _pageSize, _pageSize2);
        newPages[oldLength].InitializeEdges();
        _pages = newPages;
        _nFree += _pageSize;
        _nextAvailablePage = _pages[oldLength];

        for (var i = 0; i < _pages.Length - 1; i++) _pages[i].NextPage = _pages[i + 1];
    }

    /// <summary>
    ///     Allocates pages for the edge pool.
    /// </summary>
    private void AllocatePages(int oldLen, int nP, int availablePageId)
    {
        var newPages = new EdgePage[nP];
        Array.Copy(_pages, 0, newPages, 0, oldLen);

        // Initialize new pages (can be parallelized for large allocations)
        if (nP - oldLen > 2)
            Parallel.For(
                oldLen,
                nP,
                (int i) =>
                    {
                        newPages[i] = new EdgePage(i, _pageSize, _pageSize2);
                        newPages[i].InitializeEdges();
                    });
        else
            for (var i = oldLen; i < nP; i++)
            {
                newPages[i] = new EdgePage(i, _pageSize, _pageSize2);
                newPages[i].InitializeEdges();
            }

        // Link pages
        for (var i = 0; i < nP - 1; i++) newPages[i].NextPage = newPages[i + 1];

        _pages = newPages;
        _nextAvailablePage = _pages[availablePageId];
        _nFree += (nP - oldLen) * _pageSize;
    }

    /// <summary>
    ///     Throws an ObjectDisposedException if this instance has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(EdgePool));
    }

    private void ValidateConnections(
        QuadEdge p,
        QuadEdge e,
        QuadEdge eR,
        QuadEdge q,
        QuadEdge d,
        QuadEdge dF,
        string context)
    {
        Debug.WriteLine($"Connection validation {context}:");
        Debug.WriteLine($"  p.Forward = {p.GetForward().GetIndex()}, should be {e.GetIndex()}");
        Debug.WriteLine($"  p.Reverse = {p.GetReverse().GetIndex()}, should be {eR.GetIndex()}");
        Debug.WriteLine($"  q.Forward = {q.GetForward().GetIndex()}, should be {dF.GetIndex()}");
        Debug.WriteLine($"  q.Reverse = {q.GetReverse().GetIndex()}, should be {d.GetIndex()}");
        Debug.WriteLine($"  eR.Forward = {eR.GetForward().GetIndex()}, should be {p.GetIndex()}");
        Debug.WriteLine($"  dF.Reverse = {dF.GetReverse().GetIndex()}, should be {q.GetIndex()}");
    }
}