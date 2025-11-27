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
 * 08/2025 M.Fender     Created for C# port
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Edge;

using System.Collections;

using Tinfour.Core.Common;

/// <summary>
///     Represents a page of QuadEdge objects in the EdgePool.
///     Each page contains a fixed number of edges and manages their allocation/deallocation.
/// </summary>
internal class EdgePage : IDisposable
{
    /// <summary>
    ///     The size of this page (number of edges it can hold).
    /// </summary>
    private readonly int _pageSize;

    /// <summary>
    ///     Page size multiplied by 2 (used for index calculations).
    /// </summary>
    private readonly int _pageSize2;

    /// <summary>
    ///     Array of QuadEdge objects managed by this page.
    /// </summary>
    private QuadEdge[] _edges;

    /// <summary>
    ///     Indicates whether this page has been disposed.
    /// </summary>
    private bool _isDisposed;

    /// <summary>
    ///     Initializes a new instance of the EdgePage class.
    /// </summary>
    /// <param name="pageId">The unique identifier for this page</param>
    /// <param name="pageSize">The number of edges this page can hold</param>
    /// <param name="pageSize2">Page size multiplied by 2</param>
    public EdgePage(int pageId, int pageSize, int pageSize2)
    {
        PageId = pageId;
        _pageSize = pageSize;
        _pageSize2 = pageSize2;
        PageOffset = pageId * pageSize2;
        _edges = new QuadEdge[pageSize];
    }

    /// <summary>
    ///     The number of currently allocated edges in this page.
    /// </summary>
    public int NAllocated { get; private set; }

    /// <summary>
    ///     Reference to the next page in the linked list of available pages.
    /// </summary>
    public EdgePage? NextPage { get; set; }

    /// <summary>
    ///     The unique identifier for this page.
    /// </summary>
    public int PageId { get; }

    /// <summary>
    ///     The offset for edge indices within this page.
    /// </summary>
    public int PageOffset { get; }

    /// <summary>
    ///     Allocates an edge from this page.
    /// </summary>
    /// <returns>A QuadEdge object ready for use</returns>
    public QuadEdge AllocateEdge()
    {
        ThrowIfDisposed();

        if (NAllocated >= _pageSize) throw new InvalidOperationException("Page is fully allocated");

        var edge = _edges[NAllocated];
        edge.SetIndex(PageId * _pageSize2 + NAllocated * 2);
        NAllocated++;
        return edge;
    }

    /// <summary>
    ///     Clears all edges in this page, returning them to the unallocated state.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();

        for (var i = 0; i < _pageSize; i++) _edges[i].Clear();

        NAllocated = 0;
    }

    /// <summary>
    ///     Deallocates the specified edge, returning it to the free pool.
    /// </summary>
    /// <param name="edge">The edge to deallocate</param>
    /// <param name="linearConstraintMap">The constraint map for updating indices</param>
    public void DeallocateEdge(QuadEdge edge, Dictionary<int, IConstraint> linearConstraintMap)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(edge);

        var baseEdge = (QuadEdge)edge.GetBaseReference();
        var arrayIndex = (baseEdge.GetIndex() - PageOffset) / 2;
        baseEdge.Clear();

        // Keep all allocated edges together at the beginning of the array
        // and all free edges together at the end
        NAllocated--;

        if (arrayIndex < NAllocated)
        {
            var swapEdge = _edges[NAllocated];
            _edges[arrayIndex] = swapEdge;
            var oldIndex = swapEdge.GetIndex();
            var newIndex = PageOffset + arrayIndex * 2;
            swapEdge.SetIndex(newIndex);
            _edges[NAllocated] = baseEdge;

            // Update constraint maps for the swapped edge
            if (swapEdge.IsConstraintLineMember())
                if (linearConstraintMap.TryGetValue(oldIndex, out var constraint))
                {
                    linearConstraintMap.Remove(oldIndex);
                    linearConstraintMap.Remove(oldIndex ^ 1);
                    linearConstraintMap[newIndex] = constraint;
                    linearConstraintMap[newIndex ^ 1] = constraint;
                }

            baseEdge.SetIndex(PageOffset + NAllocated * 2);
        }
    }

    /// <summary>
    ///     Disposes of this EdgePage instance.
    /// </summary>
    public void Dispose()
    {
        if (!_isDisposed)
        {
            NextPage = null;
            if (_edges != null)
            {
                for (var i = 0; i < _pageSize; i++) _edges[i]?.Clear();

                _edges = null!;
            }

            _isDisposed = true;
        }
    }

    /// <summary>
    ///     Gets the edge at the specified index within this page.
    /// </summary>
    /// <param name="index">The index of the edge within this page</param>
    /// <returns>The QuadEdge at the specified index</returns>
    public QuadEdge GetEdge(int index)
    {
        ThrowIfDisposed();

        if (index < 0 || index >= NAllocated) throw new ArgumentOutOfRangeException(nameof(index));

        return _edges[index];
    }

    /// <summary>
    ///     Initializes the array of QuadEdge objects.
    ///     This method is called when a new page is created.
    /// </summary>
    public void InitializeEdges()
    {
        for (var i = 0; i < _pageSize; i++) _edges[i] = new QuadEdge(PageOffset + i * 2);
    }

    /// <summary>
    ///     Determines whether this page is fully allocated.
    /// </summary>
    /// <returns>True if the page is fully allocated; otherwise false</returns>
    public bool IsFullyAllocated()
    {
        return NAllocated == _pageSize;
    }

    /// <summary>
    ///     Throws an ObjectDisposedException if this instance has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(EdgePage));
    }
}

/// <summary>
///     Provides an iterator for traversing QuadEdge objects across multiple EdgePage instances.
/// </summary>
internal class EdgeIterator : IEnumerator<IQuadEdge>
{
    private readonly bool _includeGhostEdges;

    private readonly EdgePage[] _pages;

    private IQuadEdge? _current;

    private int _currentEdge;

    private int _currentPage;

    private bool _hasNext;

    /// <summary>
    ///     Initializes a new instance of the EdgeIterator class.
    /// </summary>
    /// <param name="pages">The array of pages to iterate through</param>
    /// <param name="includeGhostEdges">Whether to include ghost edges in the iteration</param>
    public EdgeIterator(EdgePage[] pages, bool includeGhostEdges)
    {
        _pages = pages;
        _includeGhostEdges = includeGhostEdges;
        _currentPage = 0;
        _currentEdge = -1;
        _hasNext = FindNextEdge();
    }

    /// <summary>
    ///     Gets the current QuadEdge in the iteration.
    /// </summary>
    public IQuadEdge Current => _current ?? throw new InvalidOperationException("No current element");

    /// <summary>
    ///     Gets the current element in the iteration.
    /// </summary>
    object IEnumerator.Current => Current;

    /// <summary>
    ///     Disposes of the iterator.
    /// </summary>
    public void Dispose()
    {
        _current = null;
    }

    /// <summary>
    ///     Advances the iterator to the next edge.
    /// </summary>
    /// <returns>True if there is a next edge; otherwise false</returns>
    public bool MoveNext()
    {
        if (!_hasNext)
        {
            _current = null;
            return false;
        }

        _current = _pages[_currentPage].GetEdge(_currentEdge);
        _hasNext = FindNextEdge();
        return true;
    }

    /// <summary>
    ///     Resets the iterator to its initial position.
    /// </summary>
    public void Reset()
    {
        _currentPage = 0;
        _currentEdge = -1;
        _current = null;
        _hasNext = FindNextEdge();
    }

    /// <summary>
    ///     Finds the next valid edge in the iteration.
    /// </summary>
    /// <returns>True if a next edge was found; otherwise false</returns>
    private bool FindNextEdge()
    {
        while (_currentPage < _pages.Length)
        {
            _currentEdge++;
            if (_currentEdge < _pages[_currentPage].NAllocated)
            {
                if (!_includeGhostEdges)
                {
                    var edge = _pages[_currentPage].GetEdge(_currentEdge);
                    if (edge.GetA().IsNullVertex() || edge.GetB().IsNullVertex()) continue; // Skip ghost edges
                }

                return true;
            }

            _currentEdge = -1;
            _currentPage++;
        }

        return false;
    }
}