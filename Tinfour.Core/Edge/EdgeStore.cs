/*
 * Copyright 2026 ReefMaster Software.
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
 * 07/2026  M. Fender    Created for ticket #832 (int-handle SoA core)
 *
 * Notes:
 * This store replaces the QuadEdge/QuadEdgePartner heap-object pair design
 * (~30 M objects per 5 M-vertex TIN) with structure-of-arrays storage
 * indexed by int handles. A directed half-edge handle h addresses one side
 * of an edge pair: pair = h >> 1, side = h & 1, dual = h ^ 1. The handle of
 * a directed edge IS its public index (base edges even, partners odd),
 * preserving the index semantics of the previous object design.
 *
 * Handles are stable for the lifetime of an allocation: deallocated pairs
 * are recycled through a free list rather than the swap-compaction the
 * page-based pool used (compaction renumbered live edges, which is
 * incompatible with handle identity).
 *
 * QuadEdge/QuadEdgePartner remain as canonical flyweight wrappers so the
 * public IQuadEdge object API keeps working; Wrap(h) returns the same
 * instance for a handle every time (required by consumers that compare
 * edges by reference).
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Edge;

using System.Runtime.CompilerServices;
using System.Threading;

using Tinfour.Core.Common;

/// <summary>
///     Structure-of-arrays backing store for the edges of a single TIN.
///     Edges are addressed by directed int handles; see file notes for the
///     handle encoding. Not thread-safe for mutation; concurrent read-only
///     traversal (including lazy wrapper materialization) is supported.
/// </summary>
internal sealed class EdgeStore
{
    /// <summary>
    ///     Sentinel for an unset edge link.
    /// </summary>
    internal const int NullHandle = -1;

    /// <summary>
    ///     The initial capacity in edge pairs.
    /// </summary>
    private const int InitialPairCapacity = 1024;

    /// <summary>
    ///     Forward link per directed handle; NullHandle when unset.
    /// </summary>
    private int[] _f;

    /// <summary>
    ///     Reverse link per directed handle; NullHandle when unset.
    /// </summary>
    private int[] _r;

    /// <summary>
    ///     The A vertex per directed handle (B is the A of the dual).
    ///     Vertex.Null for ghost sides; never a CLR null for a valid handle.
    /// </summary>
    private IVertex[] _v;

    /// <summary>
    ///     Coordinate mirrors of the A vertex per directed handle, written when
    ///     the vertex is assigned. Hot loops (walk, in-circle, flip) read these
    ///     instead of dereferencing IVertex, removing interface dispatch and
    ///     object chasing from the build path. NaN for ghost sides (the null
    ///     vertex's coordinates are NaN by construction, and no real vertex can
    ///     have a NaN X and participate in triangulation).
    /// </summary>
    private double[] _ax;

    private double[] _ay;

    /// <summary>
    ///     Packed constraint flags/indices per edge pair (the bits previously
    ///     held in QuadEdgePartner._index). Shared by both sides of the pair.
    /// </summary>
    private int[] _constraintBits;

    /// <summary>
    ///     Canonical flyweight wrapper per directed handle, materialized lazily.
    ///     Even handles wrap as QuadEdge, odd as QuadEdgePartner.
    /// </summary>
    private QuadEdge?[] _wrappers;

    /// <summary>
    ///     Allocation flag per edge pair.
    /// </summary>
    private bool[] _allocatedPairs;

    /// <summary>
    ///     Recycling generation per edge pair, incremented whenever a pair is
    ///     deallocated. Handles are stable while allocated, but a recycled pair
    ///     reuses its handle (and canonical wrappers); consumers that key
    ///     bookkeeping by edge index across TIN mutations (e.g. RuppertRefiner
    ///     work queues) combine the index with this generation so stale entries
    ///     never alias the pair's next occupant.
    /// </summary>
    private int[] _generation;

    /// <summary>
    ///     Stack of recycled pair indices available for reuse.
    /// </summary>
    private int[] _freePairs;

    private int _freeCount;

    /// <summary>
    ///     Pairs [0, _highWater) have been claimed at least once; new claims
    ///     beyond the free list come from here.
    /// </summary>
    private int _highWater;

    private int _allocatedCount;

    public EdgeStore()
    {
        _f = new int[InitialPairCapacity * 2];
        _r = new int[InitialPairCapacity * 2];
        _v = new IVertex[InitialPairCapacity * 2];
        _ax = new double[InitialPairCapacity * 2];
        _ay = new double[InitialPairCapacity * 2];
        _constraintBits = new int[InitialPairCapacity];
        _wrappers = new QuadEdge?[InitialPairCapacity * 2];
        _allocatedPairs = new bool[InitialPairCapacity];
        _generation = new int[InitialPairCapacity];
        _freePairs = new int[64];

        Array.Fill(_f, NullHandle);
        Array.Fill(_r, NullHandle);
        Array.Fill(_v, Vertex.Null);
        Array.Fill(_ax, double.NaN);
        Array.Fill(_ay, double.NaN);
    }

    /// <summary>
    ///     Gets the number of edge pairs currently allocated.
    /// </summary>
    internal int AllocatedCount => _allocatedCount;

    /// <summary>
    ///     Gets the capacity of the store in edge pairs.
    /// </summary>
    internal int PairCapacity => _allocatedPairs.Length;

    /// <summary>
    ///     Gets the number of pairs available without further array growth.
    /// </summary>
    internal int FreeCapacity => PairCapacity - _allocatedCount;

    /// <summary>
    ///     Gets the pair high-water mark: pair indices at or above this value
    ///     have never been allocated.
    /// </summary>
    internal int PairHighWater => _highWater;

    // ---------- Topology accessors (hot paths) ----------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Forward(int h) => _f[h];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Reverse(int h) => _r[h];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IVertex VertexA(int h) => _v[h];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IVertex VertexB(int h) => _v[h ^ 1];

    /// <summary>
    ///     X coordinate of the A vertex of the handle; NaN for a ghost side.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal double Ax(int h) => _ax[h];

    /// <summary>
    ///     Y coordinate of the A vertex of the handle; NaN for a ghost side.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal double Ay(int h) => _ay[h];

    /// <summary>
    ///     Indicates whether the A vertex of the handle is the ghost (null)
    ///     vertex. Uses the NaN coordinate mirror: the null vertex's X is NaN
    ///     by construction and no participating vertex can have a NaN X.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsNullA(int h) => double.IsNaN(_ax[h]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int ConstraintBits(int h) => _constraintBits[h >> 1];

    /// <summary>
    ///     Gets the recycling generation of the pair containing the handle.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GenerationOf(int h) => _generation[h >> 1];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetConstraintBits(int h, int bits) => _constraintBits[h >> 1] = bits;

    /// <summary>
    ///     Sets the forward link of h to f and the reciprocal reverse link of f to h.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetForward(int h, int f)
    {
        _f[h] = f;
        if (f >= 0) _r[f] = h;
    }

    /// <summary>
    ///     Sets the reverse link of h to r and the reciprocal forward link of r to h.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetReverse(int h, int r)
    {
        _r[h] = r;
        if (r >= 0) _f[r] = h;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetForwardDirect(int h, int f) => _f[h] = f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetReverseDirect(int h, int r) => _r[h] = r;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetVertexA(int h, IVertex a)
    {
        _v[h] = a;
        if (a is Vertex plain)
        {
            _ax[h] = plain.X;
            _ay[h] = plain.Y;
        }
        else
        {
            _ax[h] = a.X;
            _ay[h] = a.Y;
        }
    }

    /// <summary>
    ///     Sets both vertices of the pair containing h, relative to side h.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetVertices(int h, IVertex a, IVertex b)
    {
        SetVertexA(h, a);
        SetVertexA(h ^ 1, b);
    }

    // ---------- Wrappers ----------

    /// <summary>
    ///     Gets the canonical wrapper for the directed handle. The same
    ///     instance is returned for a handle every time; consumers rely on
    ///     reference identity of edges obtained through the object API.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal QuadEdge Wrap(int h)
    {
        return _wrappers[h] ?? WrapSlow(h);
    }

    /// <summary>
    ///     Gets the canonical wrapper for a handle, or null for NullHandle.
    /// </summary>
    internal QuadEdge? WrapOrNull(int h)
    {
        return h < 0 ? null : Wrap(h);
    }

    private QuadEdge WrapSlow(int h)
    {
        // Concurrent read-only traversals may race to materialize a wrapper;
        // CAS guarantees a single canonical instance per handle.
        QuadEdge created = (h & 1) == 0 ? new QuadEdge(this, h) : new QuadEdgePartner(this, h);
        var prior = Interlocked.CompareExchange(ref _wrappers[h], created, null);
        return prior ?? created;
    }

    // ---------- Allocation ----------

    /// <summary>
    ///     Indicates whether the pair containing the specified handle is allocated.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsAllocated(int h)
    {
        var pair = h >> 1;
        return pair >= 0 && pair < _allocatedPairs.Length && _allocatedPairs[pair];
    }

    /// <summary>
    ///     Allocates an edge pair and returns the even (base) handle of the pair.
    ///     Links are unset and both vertices are Vertex.Null.
    /// </summary>
    internal int AllocatePair()
    {
        int pair;
        if (_freeCount > 0)
        {
            pair = _freePairs[--_freeCount];
        }
        else
        {
            if (_highWater >= _allocatedPairs.Length) Grow(_allocatedPairs.Length * 2);
            pair = _highWater++;
        }

        _allocatedPairs[pair] = true;
        _allocatedCount++;
        return pair << 1;
    }

    /// <summary>
    ///     Returns the pair containing the specified handle to the free list
    ///     and resets its state.
    /// </summary>
    internal void DeallocatePair(int h)
    {
        var pair = h >> 1;
        ClearPairState(pair);
        _allocatedPairs[pair] = false;
        _generation[pair]++;
        _allocatedCount--;

        if (_freeCount >= _freePairs.Length) Array.Resize(ref _freePairs, _freePairs.Length * 2);
        _freePairs[_freeCount++] = pair;
    }

    /// <summary>
    ///     Resets the state of a pair without changing its allocation status.
    /// </summary>
    internal void ClearPairState(int pair)
    {
        var h = pair << 1;
        _f[h] = NullHandle;
        _r[h] = NullHandle;
        _f[h | 1] = NullHandle;
        _r[h | 1] = NullHandle;
        _v[h] = Vertex.Null;
        _v[h | 1] = Vertex.Null;
        _ax[h] = double.NaN;
        _ay[h] = double.NaN;
        _ax[h | 1] = double.NaN;
        _ay[h | 1] = double.NaN;
        _constraintBits[pair] = 0;
    }

    /// <summary>
    ///     Ensures capacity so that at least the specified number of additional
    ///     pairs can be allocated without array growth.
    /// </summary>
    internal void EnsureFreeCapacity(int pairs)
    {
        var required = _allocatedCount + pairs;
        if (required <= _allocatedPairs.Length) return;

        var newCapacity = _allocatedPairs.Length;
        while (newCapacity < required) newCapacity *= 2;
        Grow(newCapacity);
    }

    /// <summary>
    ///     Deallocates every pair and resets the store to its initial state,
    ///     retaining current capacity. Existing wrappers remain canonical for
    ///     their handles.
    /// </summary>
    internal void Clear()
    {
        for (var pair = 0; pair < _highWater; pair++)
        {
            if (_allocatedPairs[pair]) ClearPairState(pair);
            _allocatedPairs[pair] = false;
            _generation[pair]++;
        }

        _allocatedCount = 0;
        _freeCount = 0;
        _highWater = 0;
    }

    private void Grow(int newPairCapacity)
    {
        var oldPairCapacity = _allocatedPairs.Length;
        var oldHandleCapacity = oldPairCapacity * 2;
        var newHandleCapacity = newPairCapacity * 2;

        Array.Resize(ref _f, newHandleCapacity);
        Array.Resize(ref _r, newHandleCapacity);
        Array.Resize(ref _v, newHandleCapacity);
        Array.Resize(ref _ax, newHandleCapacity);
        Array.Resize(ref _ay, newHandleCapacity);
        Array.Resize(ref _constraintBits, newPairCapacity);
        Array.Resize(ref _wrappers, newHandleCapacity);
        Array.Resize(ref _allocatedPairs, newPairCapacity);
        Array.Resize(ref _generation, newPairCapacity);

        Array.Fill(_f, NullHandle, oldHandleCapacity, newHandleCapacity - oldHandleCapacity);
        Array.Fill(_r, NullHandle, oldHandleCapacity, newHandleCapacity - oldHandleCapacity);
        Array.Fill(_v, Vertex.Null, oldHandleCapacity, newHandleCapacity - oldHandleCapacity);
        Array.Fill(_ax, double.NaN, oldHandleCapacity, newHandleCapacity - oldHandleCapacity);
        Array.Fill(_ay, double.NaN, oldHandleCapacity, newHandleCapacity - oldHandleCapacity);
    }
}
