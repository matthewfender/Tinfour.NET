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

using System.Diagnostics;
using System.Runtime.CompilerServices;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;

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
/// </remarks>
public class SmoothingFilter : IVertexValuator
{
    /// <summary>
    ///     Default number of smoothing passes.
    /// </summary>
    public const int DefaultPasses = 25;

    private readonly Dictionary<IVertex, double> _smoothedValues;
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
        _smoothedValues = initializer.SmoothedValues;

        // Compute min/max Z values
        var z0 = double.PositiveInfinity;
        var z1 = double.NegativeInfinity;
        foreach (var z in _smoothedValues.Values)
        {
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
    public int VertexCount => _smoothedValues.Count;

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

        if (_smoothedValues.TryGetValue(v, out var smoothedZ))
            return smoothedZ;

        // Vertex not in our dictionary - return original Z value
        // This can happen for vertices added after the filter was built
        return v.GetZ();
    }
}

/// <summary>
///     Internal class that performs the actual smoothing filter initialization.
/// </summary>
internal class SmoothingFilterInitializer
{
    private readonly BarycentricCoordinates _barycentricCoords = new();

    /// <summary>
    ///     The resulting smoothed Z values, keyed by vertex.
    /// </summary>
    public Dictionary<IVertex, double> SmoothedValues { get; }

    public SmoothingFilterInitializer(IIncrementalTin tin, int nPasses)
    {
        // Build vertex list and mappings
        var vertices = tin.GetVertices().ToList();
        var nVertex = vertices.Count;

        // Create vertex-to-index mapping (using reference equality)
        var vertexToIndex = new Dictionary<IVertex, int>(ReferenceEqualityComparer.Instance);
        var indexToVertex = new IVertex[nVertex];

        for (var i = 0; i < nVertex; i++)
        {
            vertexToIndex[vertices[i]] = i;
            indexToVertex[i] = vertices[i];
        }

        // Initialize Z array from vertices
        var zArray = new double[nVertex];
        for (var i = 0; i < nVertex; i++)
        {
            zArray[i] = vertices[i].GetZ();
        }

        // Build neighbor weights for each vertex
        // neighborIndices[vertexIndex] = array of neighbor vertex indices
        // neighborWeights[vertexIndex] = array of corresponding weights
        var neighborIndices = new int[nVertex][];
        var neighborWeights = new float[nVertex][];

        var visited = new HashSet<IVertex>(ReferenceEqualityComparer.Instance);

        foreach (var e in tin.GetEdgeIterator())
        {
            InitForEdge(visited, e, vertexToIndex, neighborIndices, neighborWeights);
            InitForEdge(visited, e.GetDual(), vertexToIndex, neighborIndices, neighborWeights);
        }

        // Perform smoothing passes
        for (var pass = 0; pass < nPasses; pass++)
        {
            zArray = ProcessZ(zArray, neighborIndices, neighborWeights);
        }

        // Build result dictionary
        SmoothedValues = new Dictionary<IVertex, double>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < nVertex; i++)
        {
            SmoothedValues[indexToVertex[i]] = zArray[i];
        }
    }

    private List<Vertex>? GetConnectedPolygon(IQuadEdge e)
    {
        var vList = new List<Vertex>();
        foreach (var s in e.GetPinwheel())
        {
            // If this edge or its dual is constrained, the vertex is on a constraint boundary
            // and we shouldn't smooth across constraints
            if (s.IsConstrained() || s.GetDual().IsConstrained())
                return null;

            var b = s.GetB();
            // If any neighbor is null/ghost, the vertex is on the perimeter
            // and doesn't have valid barycentric coordinates
            if (b == null || b.IsNullVertex())
                return null;

            // Only include real Vertex instances
            if (b is Vertex v)
            {
                // Also skip if the neighbor is a constraint member - this ensures
                // we don't pull values from vertices on constraint boundaries
                if (v.IsConstraintMember())
                    return null;
                vList.Add(v);
            }
        }
        return vList;
    }

    private void InitForEdge(
        HashSet<IVertex> visited,
        IQuadEdge edge,
        Dictionary<IVertex, int> vertexToIndex,
        int[][] neighborIndices,
        float[][] neighborWeights)
    {
        var a = edge.GetA();
        if (a == null || a.IsNullVertex())
            return;

        if (visited.Contains(a))
            return;
        visited.Add(a);

        // Don't smooth constraint member vertices - check via Vertex cast
        if (a is Vertex vertex && vertex.IsConstraintMember())
            return;

        if (!vertexToIndex.TryGetValue(a, out var vertexIndex))
            return; // Vertex not in our mapping - skip it

        var x = a.X;
        var y = a.Y;

        var pList = GetConnectedPolygon(edge);
        if (pList == null || pList.Count < 3)
            return;

        var w = _barycentricCoords.GetBarycentricCoordinates(pList, x, y);
        if (w == null)
            return;

        Debug.Assert(w.Length == pList.Count, "Incorrect barycentric weights result");

        // Validate weights - they should sum to approximately 1.0 and all be finite
        double weightSum = 0;
        foreach (var weight in w)
        {
            if (double.IsNaN(weight) || double.IsInfinity(weight))
                return; // Invalid barycentric weights - skip this vertex
            weightSum += weight;
        }
        // Weights should sum to ~1.0; if not, the point is outside the polygon or there's an error
        if (weightSum < 0.5 || weightSum > 2.0)
            return;

        // Store neighbor indices and weights - skip if any neighbor is not in our mapping
        var indices = new int[pList.Count];
        var weights = new float[pList.Count];

        for (var i = 0; i < pList.Count; i++)
        {
            if (!vertexToIndex.TryGetValue(pList[i], out var neighborIndex))
                return; // Neighbor not in our mapping - skip this vertex entirely
            indices[i] = neighborIndex;
            weights[i] = (float)w[i];
        }

        neighborIndices[vertexIndex] = indices;
        neighborWeights[vertexIndex] = weights;
    }

    private static double[] ProcessZ(double[] zArray, int[][] neighborIndices, float[][] neighborWeights)
    {
        var z = new double[zArray.Length];

        for (var index = 0; index < zArray.Length; index++)
        {
            var indices = neighborIndices[index];
            var weights = neighborWeights[index];

            if (indices == null)
            {
                // No mapping - use original value (perimeter or constraint vertex)
                z[index] = zArray[index];
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
                    var neighborZ = zArray[indices[i]];
                    // Skip invalid Z values
                    if (double.IsNaN(neighborZ) || double.IsInfinity(neighborZ))
                        continue;
                    zSum += neighborZ * w;
                    wSum += w;
                }
                // Protect against division by zero or near-zero
                if (wSum > 1e-10)
                    z[index] = zSum / wSum;
                else
                    z[index] = zArray[index]; // Fall back to original
            }
        }
        return z;
    }
}
