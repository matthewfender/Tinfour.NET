/*
 * Copyright (C) 2019  Gary W. Lucas.
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
 * 08/2019  G. Lucas     Created (Java)
 * 12/2025  M. Fender    Ported to C#
 * 07/2026  M. Fender    Parallelized neighbor build and smoothing passes
 *                       over the frozen TIN; per-phase timing attribution
 *
 * Notes:
 *
 *   This class creates a set of "smoothed" z values for a surface represented
 *   by a Delaunay Triangulation. The smoothing algorithm begins by generating
 *   a set of Barycentric weights that can be used to adjust the z values
 *   of the vertices in the TIN by combining them with the values of their
 *   immediate neighbors. The process then follows an iterative process
 *   performing several sets of combinations until the overall complexity
 *   of the surface is reduced. In effect, it is a low-pass filter, providing
 *   a smoother, less complex representation of the surface.
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Utils;

using System.Collections.Concurrent;
using System.Diagnostics;

using Tinfour.Core.Common;
using Tinfour.Core.Diagnostics;
using Tinfour.Core.Edge;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;

/// <summary>
///     An implementation of the vertex valuator that processes the vertices in a
///     Constrained Delaunay Triangulation and applies a low-pass filter over the
///     data.
/// </summary>
/// <remarks>
///     <para>
///         The vertices belonging to constraints are not smoothed, but are represented
///         with their original values by the smoothing filter.
///     </para>
///     <para>
///         Vertices on the perimeter of the TIN are also not smoothed since they
///         do not have a valid set of barycentric coordinates.
///     </para>
///     <para>
///         Construction reads the TIN from multiple threads; the TIN must not be
///         modified while the filter is being built.
///     </para>
/// </remarks>
public class SmoothingFilter : IVertexValuator
{
    /// <summary>
    ///     Default number of smoothing passes.
    /// </summary>
    public const int DefaultPasses = 25;

    private readonly Dictionary<IVertex, int> _vertexToIndex;
    private readonly double[] _finalZ;
    private readonly double _zMin;
    private readonly double _zMax;
    private readonly double _timeToConstructFilterMs;

    /// <summary>
    ///     Construct a smoothing filter with the default number of passes (25).
    /// </summary>
    /// <param name="tin">A valid Delaunay Triangulation</param>
    public SmoothingFilter(IIncrementalTin tin)
        : this(tin, DefaultPasses)
    {
    }

    /// <summary>
    ///     Construct a smoothing filter with the specified number of passes.
    /// </summary>
    /// <param name="tin">A valid Delaunay Triangulation</param>
    /// <param name="nPasses">
    ///     The number of passes the filter performs over the vertices during smoothing.
    ///     Values in the range 5 to 40 are good candidates for investigation.
    /// </param>
    /// <remarks>
    ///     <para>
    ///         The vertices belonging to constraints are not smoothed, but are represented
    ///         with their original values by the smoothing filter.
    ///     </para>
    ///     <para>
    ///         The number of passes determines the degree to which features are smoothed.
    ///         The best choice for this value depends on the requirements of the application.
    ///     </para>
    /// </remarks>
    public SmoothingFilter(IIncrementalTin tin, int nPasses)
    {
        ArgumentNullException.ThrowIfNull(tin);
        if (nPasses < 1)
            throw new ArgumentOutOfRangeException(nameof(nPasses), "Number of passes must be at least 1");

        var sw = Stopwatch.StartNew();

        var initializer = new SmoothingFilterInitializer(tin, nPasses);
        _vertexToIndex = initializer.VertexToIndex;
        _finalZ = initializer.FinalZ;
        Timings = initializer.Timings;

        // Compute min/max Z values (comparisons are order-independent)
        var finalZ = _finalZ;
        var z0 = double.PositiveInfinity;
        var z1 = double.NegativeInfinity;
        for (var i = 0; i < finalZ.Length; i++)
        {
            var z = finalZ[i];
            if (z < z0) z0 = z;
            if (z > z1) z1 = z;
        }
        _zMin = z0;
        _zMax = z1;

        sw.Stop();
        _timeToConstructFilterMs = sw.Elapsed.TotalMilliseconds;
    }

    /// <summary>
    ///     Gets the time required to construct the filter, in milliseconds.
    ///     Intended for diagnostic and development purposes.
    /// </summary>
    public double TimeToConstructFilterMs => _timeToConstructFilterMs;

    /// <summary>
    ///     Gets per-phase wall-clock timings for the filter construction.
    ///     Intended for diagnostic and development purposes.
    /// </summary>
    public SmoothingFilterTimings Timings { get; }

    /// <summary>
    ///     Gets the minimum value from the set of possible values. Due to the
    ///     smoothing, this value may be larger than the minimum input value.
    /// </summary>
    public double MinZ => _zMin;

    /// <summary>
    ///     Gets the maximum value from the set of possible values. Due to the
    ///     smoothing, this value may be smaller than the maximum input value.
    /// </summary>
    public double MaxZ => _zMax;

    /// <summary>
    ///     Gets the number of vertices in the smoothing filter.
    /// </summary>
    public int VertexCount => _vertexToIndex.Count;

    /// <summary>
    ///     Gets the smoothed Z value for the specified vertex.
    /// </summary>
    /// <param name="v">A valid vertex</param>
    /// <returns>The smoothed Z value, or NaN for null/ghost vertices</returns>
    public double Value(IVertex v)
    {
        // Handle null/ghost vertices the same way as DefaultValuator
        if (v == null || v.IsNullVertex())
            return double.NaN;

        if (_vertexToIndex.TryGetValue(v, out var index))
            return _finalZ[index];

        // Vertex not in our mapping - return original Z value
        // This can happen for vertices added after the filter was built
        return v.GetZ();
    }
}

/// <summary>
///     Internal class that performs the actual smoothing filter initialization.
/// </summary>
/// <remarks>
///     The neighbor-index build and the smoothing passes are parallelized over the frozen
///     TIN (the dominant costs at ReefMaster scale — see ticket 829 attribution). The
///     output is bit-identical to the historical sequential implementation: each vertex's
///     pinwheel starting edge is pinned to the one the sequential edge sweep would have
///     chosen, per-vertex weight computations are independent, and each smoothing pass
///     reads only the previous pass's buffer with an unchanged per-vertex accumulation
///     order.
/// </remarks>
internal class SmoothingFilterInitializer
{
    /// <summary>
    ///     Maps each TIN vertex (reference identity) to its index in <see cref="FinalZ" />.
    /// </summary>
    public Dictionary<IVertex, int> VertexToIndex { get; }

    /// <summary>
    ///     The smoothed Z values in vertex-index order.
    /// </summary>
    public double[] FinalZ { get; }

    /// <summary>
    ///     Per-phase wall-clock timings captured during construction.
    /// </summary>
    public SmoothingFilterTimings Timings { get; }

    public SmoothingFilterInitializer(IIncrementalTin tin, int nPasses)
    {
        var swTotal = Stopwatch.StartNew();
        var sw = Stopwatch.StartNew();

        // ------------------------------------------------------------------
        // Phase 1 (sequential): fused vertex collection + pinwheel-start claim sweep.
        // One pass over the edge pool replaces the historical GetVertices() sweep and the
        // separate visited-set sweep, deduping straight into the reference-keyed index map.
        // Dedup is by reference identity; the TIN merges coincident insertions into a
        // VertexMergerGroup, so edge-referenced instances have unique coordinates and this
        // is equivalent to GetVertices()'s value-equality dedup on any well-formed TIN.
        // Each retained vertex also records the half-edge whose GetA() first exposed it:
        // the same starting edge the sequential sweep fed to the pinwheel, which pins the
        // neighbor ordering and keeps weights bit-identical.
        // ------------------------------------------------------------------
        var estimatedVertices = Math.Max(16, tin.GetMaximumEdgeAllocationIndex() / 6);
        var vertexToIndex = new Dictionary<IVertex, int>(estimatedVertices, ReferenceEqualityComparer.Instance);
        var vertices = new List<IVertex>(estimatedVertices);
        var claimEdges = new List<int>(estimatedVertices);

        // Handle-native sweep over allocated pairs in ascending slot order —
        // the same enumeration order as GetEdgeIterator(), reading the store's
        // arrays directly. For a custom IIncrementalTin implementation the
        // store is resolved from the first edge (all edges are flyweights over
        // an EdgeStore), keeping parity with ContourBuilderForTin.
        var store = (tin as IncrementalTin)?.GetEdgePoolInternal().Store
                    ?? ((QuadEdge?)tin.GetEdgeIterator().FirstOrDefault())?.GetStore();
        if (store != null)
        {
            for (var pair = 0; pair < store.PairHighWater; pair++)
            {
                var h = pair << 1;
                if (!store.IsAllocated(h)) continue;

                if (!store.IsNullA(h))
                {
                    var a = store.VertexA(h);
                    if (vertexToIndex.TryAdd(a, vertices.Count))
                    {
                        vertices.Add(a);
                        claimEdges.Add(h);
                    }
                }

                if (!store.IsNullA(h | 1))
                {
                    var b = store.VertexA(h | 1);
                    if (vertexToIndex.TryAdd(b, vertices.Count))
                    {
                        vertices.Add(b);
                        claimEdges.Add(h | 1);
                    }
                }
            }
        }

        if (vertices.Count == 0)
        {
            // Degenerate (non-bootstrapped) TIN: no edges to sweep. Fall back to the raw
            // vertex list; with no edges there are no neighbors, so all values pass through.
            foreach (var v in tin.GetVertices())
            {
                if (vertexToIndex.TryAdd(v, vertices.Count))
                {
                    vertices.Add(v);
                    claimEdges.Add(EdgeStore.NullHandle);
                }
            }
        }

        // Partitioner.Create requires a non-empty range; an empty TIN short-circuits every
        // parallel phase below (all loops no-op, matching the sequential implementation).
        var nVertex = vertices.Count;
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
        var zArray = new double[nVertex];
        if (nVertex > 0)
        {
            Parallel.ForEach(
                Partitioner.Create(0, nVertex),
                parallelOptions,
                range =>
                {
                    for (var i = range.Item1; i < range.Item2; i++)
                        zArray[i] = vertices[i].GetZ();
                });
        }

        var vertexCollection = sw.Elapsed;
        sw.Restart();

        // ------------------------------------------------------------------
        // Phase 2 (parallel): per-vertex neighbor index + barycentric weights.
        // The TIN is read-only here; every vertex's computation is independent and starts
        // from its claimed edge, so the parallel schedule cannot affect the results.
        // ------------------------------------------------------------------
        var neighborIndices = new int[nVertex][];
        var neighborWeights = new float[nVertex][];
        var smoothedCount = 0;

        if (nVertex > 0)
        {
            Parallel.ForEach(
                Partitioner.Create(0, nVertex),
                parallelOptions,
                () => new NeighborScratch(),
                (range, _, scratch) =>
                {
                    for (var i = range.Item1; i < range.Item2; i++)
                    {
                        if (BuildNeighbors(i, vertices[i], claimEdges[i], store!, vertexToIndex, neighborIndices, neighborWeights, scratch))
                            scratch.BuiltCount++;
                    }

                    return scratch;
                },
                scratch => Interlocked.Add(ref smoothedCount, scratch.BuiltCount));
        }

        var neighborBuild = sw.Elapsed;

        // ------------------------------------------------------------------
        // Phase 3 (parallel): double-buffered smoothing passes. Each output element
        // depends only on the read buffer, with the same per-vertex accumulation order
        // as the sequential implementation.
        // ------------------------------------------------------------------
        var passTimes = new TimeSpan[nPasses];
        var zRead = zArray;
        var zWrite = new double[nVertex];
        for (var pass = 0; pass < nPasses && nVertex > 0; pass++)
        {
            sw.Restart();
            var read = zRead;
            var write = zWrite;
            Parallel.ForEach(
                Partitioner.Create(0, nVertex),
                parallelOptions,
                range => ProcessZRange(range.Item1, range.Item2, read, write, neighborIndices, neighborWeights));
            (zRead, zWrite) = (zWrite, zRead);
            passTimes[pass] = sw.Elapsed;
        }

        // The vertex-to-index map built in phase 1 doubles as the lookup for Value();
        // no separate result dictionary is needed.
        VertexToIndex = vertexToIndex;
        FinalZ = zRead;

        Timings = new SmoothingFilterTimings
        {
            VertexCollection = vertexCollection,
            NeighborBuild = neighborBuild,
            Passes = passTimes,
            Total = swTotal.Elapsed,
            VertexCount = nVertex,
            SmoothedVertexCount = smoothedCount,
        };
    }

    /// <summary>
    ///     Per-worker scratch state for the parallel neighbor build: a reusable polygon
    ///     buffer and a private <see cref="BarycentricCoordinates" /> instance (it carries
    ///     a mutable diagnostic field and must not be shared across threads).
    /// </summary>
    private sealed class NeighborScratch
    {
        public readonly BarycentricCoordinates Barycentric = new();
        public readonly List<Vertex> Polygon = new(16);
        public int BuiltCount;
    }

    /// <summary>
    ///     Computes the neighbor indices and barycentric weights for a single vertex.
    ///     Mirrors the historical sequential InitForEdge logic exactly.
    /// </summary>
    /// <returns>True if the vertex received a neighbor index; otherwise false.</returns>
    private static bool BuildNeighbors(
        int vertexIndex,
        IVertex a,
        int claimEdge,
        EdgeStore store,
        Dictionary<IVertex, int> vertexToIndex,
        int[][] neighborIndices,
        float[][] neighborWeights,
        NeighborScratch scratch)
    {
        if (claimEdge < 0)
            return false;

        // Don't smooth constraint member vertices - check via Vertex cast
        if (a is Vertex vertex && vertex.IsConstraintMember())
            return false;

        var pList = scratch.Polygon;
        pList.Clear();
        if (!TryGetConnectedPolygon(store, claimEdge, pList) || pList.Count < 3)
            return false;

        var w = scratch.Barycentric.GetBarycentricCoordinates(pList, a.X, a.Y);
        if (w == null)
            return false;

        Debug.Assert(w.Length == pList.Count, "Incorrect barycentric weights result");

        // Validate weights - they should sum to approximately 1.0 and all be finite
        double weightSum = 0;
        foreach (var weight in w)
        {
            if (double.IsNaN(weight) || double.IsInfinity(weight))
                return false; // Invalid barycentric weights - skip this vertex
            weightSum += weight;
        }

        // Weights should sum to ~1.0; if not, the point is outside the polygon or there's an error
        if (weightSum < 0.5 || weightSum > 2.0)
            return false;

        // Store neighbor indices and weights - skip if any neighbor is not in our mapping
        var indices = new int[pList.Count];
        var weights = new float[pList.Count];

        for (var i = 0; i < pList.Count; i++)
        {
            if (!vertexToIndex.TryGetValue(pList[i], out var neighborIndex))
                return false; // Neighbor not in our mapping - skip this vertex entirely
            indices[i] = neighborIndex;
            weights[i] = (float)w[i];
        }

        neighborIndices[vertexIndex] = indices;
        neighborWeights[vertexIndex] = weights;
        return true;
    }

    /// <summary>
    ///     Collects the connected polygon around the claimed edge's A vertex into
    ///     <paramref name="vList" />. A handle-native pinwheel walk (same traversal
    ///     and termination semantics as the historical enumerator).
    /// </summary>
    /// <returns>False when the vertex must not be smoothed (constrained or perimeter).</returns>
    private static bool TryGetConnectedPolygon(EdgeStore store, int start, List<Vertex> vList)
    {
        var s = start;
        while (true)
        {
            // If the edge is constrained, the vertex is on a constraint boundary
            // and we shouldn't smooth across constraints. (Constraint bits are
            // shared by both sides of a pair, so this also covers the dual.)
            if (store.ConstraintBits(s) < 0)
                return false;

            // If any neighbor is null/ghost, the vertex is on the perimeter
            // and doesn't have valid barycentric coordinates
            if (store.IsNullA(s ^ 1))
                return false;

            // Only include real Vertex instances
            if (store.VertexA(s ^ 1) is Vertex v)
            {
                // Also skip if the neighbor is a constraint member - this ensures
                // we don't pull values from vertices on constraint boundaries
                if (v.IsConstraintMember())
                    return false;
                vList.Add(v);
            }

            // Dual-from-reverse; an unset reverse link ends the iteration and the
            // polygon collected so far is used as-is (historical behavior).
            var r = store.Reverse(s);
            if (r < 0)
                return true;

            var next = r ^ 1;
            if (next == start)
                return true;

            s = next;
        }
    }

    /// <summary>
    ///     Applies one smoothing pass over the index range [from, to). Identical
    ///     per-vertex arithmetic and accumulation order to the historical sequential
    ///     ProcessZ; reads only <paramref name="zRead" />, writes only its own slice of
    ///     <paramref name="zWrite" />.
    /// </summary>
    private static void ProcessZRange(
        int from,
        int to,
        double[] zRead,
        double[] zWrite,
        int[][] neighborIndices,
        float[][] neighborWeights)
    {
        for (var index = from; index < to; index++)
        {
            var indices = neighborIndices[index];
            var weights = neighborWeights[index];

            if (indices == null)
            {
                // No mapping - use original value (perimeter or constraint vertex)
                zWrite[index] = zRead[index];
            }
            else
            {
                double zSum = 0;
                double wSum = 0;
                for (var i = 0; i < indices.Length; i++)
                {
                    var w = weights[i];
                    // Skip invalid weights
                    if (float.IsNaN(w) || float.IsInfinity(w))
                        continue;
                    var neighborZ = zRead[indices[i]];
                    // Skip invalid Z values
                    if (double.IsNaN(neighborZ) || double.IsInfinity(neighborZ))
                        continue;
                    zSum += neighborZ * w;
                    wSum += w;
                }

                // Protect against division by zero or near-zero
                if (wSum > 1e-10)
                    zWrite[index] = zSum / wSum;
                else
                    zWrite[index] = zRead[index]; // Fall back to original
            }
        }
    }
}
