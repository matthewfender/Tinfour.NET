namespace Tinfour.Core.Tests.Utils;

using System.Diagnostics;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Utils;

/// <summary>
///     Frozen copy of the pre-829 sequential <see cref="SmoothingFilter" /> implementation
///     (Tinfour.NET @ 9d95d52). Kept verbatim (classes renamed only) as the reference oracle
///     for <see cref="SmoothingFilterEquivalenceTests" />: the parallelized production filter
///     must produce bit-identical smoothed Z values for identical inputs. Do not modify.
/// </summary>
internal class LegacySmoothingFilter : IVertexValuator
{
    private readonly Dictionary<IVertex, double> _smoothedValues;
    private readonly double _zMin;
    private readonly double _zMax;

    public LegacySmoothingFilter(IIncrementalTin tin, int nPasses)
    {
        ArgumentNullException.ThrowIfNull(tin);
        if (nPasses < 1)
            throw new ArgumentOutOfRangeException(nameof(nPasses), "Number of passes must be at least 1");

        var initializer = new LegacySmoothingFilterInitializer(tin, nPasses);
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
    }

    public double MinZ => _zMin;

    public double MaxZ => _zMax;

    public int VertexCount => _smoothedValues.Count;

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
///     Frozen copy of the pre-829 sequential initializer. Do not modify.
/// </summary>
internal class LegacySmoothingFilterInitializer
{
    private readonly BarycentricCoordinates _barycentricCoords = new();

    public Dictionary<IVertex, double> SmoothedValues { get; }

    public LegacySmoothingFilterInitializer(IIncrementalTin tin, int nPasses)
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
