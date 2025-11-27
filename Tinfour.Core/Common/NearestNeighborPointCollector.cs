/*
 * Copyright 2016 Gary W. Lucas.
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
 * 07/2016  G. Lucas     Created
 * 05/2021  G. Lucas     Updated to fix bugs and awkward coding.
 * 08/2025 M.Fender     Ported to C#
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

/// <summary>
///     Provides a utility for the efficient identification of the K-nearest points
///     to a specified set of query coordinates. This utility works by creating a
///     grid of bins into which the vertices are initially separated. The bins are
///     then searched to find the nearest vertices.
/// </summary>
/// <remarks>
///     This class is intended to support testing and verification. It has no direct
///     relationship with any of the graph-based structures produced by the Tinfour
///     library.
///     The design of this class is optimized for repeated searches of very large
///     vertex sets. While the up front processing is not trivial, the time cost is
///     compensated for by more efficient processing across multiple searches.
/// </remarks>
public class NearestNeighborPointCollector
{
    private const int MaxBinCount = 10000;

    private const int TargetSamplesPerBin = 200;

    private readonly IVertex[][] _bins;

    private readonly int _nCol;

    private readonly int _nRow;

    private readonly int _nTier;

    private readonly double _sBin;

    private readonly double _xmax;

    private readonly double _xmin;

    private readonly double _ymax;

    private readonly double _ymin;

    /// <summary>
    ///     Construct a collector based on the specified list of vertices and bounds.
    ///     It is assumed that the coordinates and values of all specifications are
    ///     valid floating-point values (no NaN's included).
    /// </summary>
    /// <param name="vertices">A list of valid vertices</param>
    /// <param name="mergeDuplicates">Indicates whether duplicates should be merged.</param>
    /// <exception cref="ArgumentException">If the vertex list is empty</exception>
    public NearestNeighborPointCollector(IList<IVertex> vertices, bool mergeDuplicates = false)
    {
        if (vertices == null || vertices.Count == 0)
            throw new ArgumentException("Vertex list cannot be null or empty", nameof(vertices));

        // Find the bounds of the data
        var firstVertex = vertices[0];
        var x0 = firstVertex.GetX();
        var x1 = firstVertex.GetX();
        var y0 = firstVertex.GetY();
        var y1 = firstVertex.GetY();

        foreach (var v in vertices)
        {
            var x = v.GetX();
            var y = v.GetY();
            if (x < x0) x0 = x;
            else if (x > x1) x1 = x;
            if (y < y0) y0 = y;
            else if (y > y1) y1 = y;
        }

        _xmin = x0;
        _xmax = x1;
        _ymin = y0;
        _ymax = y1;

        var xDelta = _xmax - _xmin;
        var yDelta = _ymax - _ymin;
        var nV = vertices.Count;

        // Calculate bin size and grid dimensions
        var nBinEst = (double)nV / TargetSamplesPerBin;
        if (nBinEst < 1)
        {
            // Just make one big bin
            _sBin = 1.01 * Math.Max(xDelta, yDelta);
            _nRow = 1;
            _nCol = 1;
            _nTier = 0;
        }
        else
        {
            if (nBinEst > MaxBinCount) nBinEst = MaxBinCount;
            var area = xDelta * yDelta;
            _sBin = Math.Sqrt(area / nBinEst);
            _nRow = (int)Math.Ceiling(yDelta / _sBin + 1.0e-4);
            _nCol = (int)Math.Ceiling(xDelta / _sBin + 1.0e-4);
            _nTier = Math.Max(_nRow, _nCol);
        }

        BinCount = _nRow * _nCol;

        // First pass: count vertices per bin
        var iCount = new int[BinCount];
        foreach (var v in vertices)
        {
            var x = v.GetX();
            var y = v.GetY();
            var iRow = (int)((y - _ymin) / _sBin);
            var iCol = (int)((x - _xmin) / _sBin);

            // Ensure indices are within bounds
            iRow = Math.Max(0, Math.Min(iRow, _nRow - 1));
            iCol = Math.Max(0, Math.Min(iCol, _nCol - 1));

            iCount[iRow * _nCol + iCol]++;
        }

        // Allocate storage for each bin
        _bins = new IVertex[BinCount][];
        for (var i = 0; i < BinCount; i++)
        {
            _bins[i] = new IVertex[iCount[i]];
            iCount[i] = 0; // Reset count for use as insertion index
        }

        if (mergeDuplicates)
        {
            // The merge threshold is 1/100000th of the average spacing
            var averageSpacing = Math.Sqrt(xDelta * yDelta / nV);
            var mergeThreshold = averageSpacing / 1.0e5;
            var m2 = mergeThreshold * mergeThreshold;
            var mergedVertices = new List<Vertex>();

            foreach (var v in vertices)
            {
                var x = v.GetX();
                var y = v.GetY();
                var iRow = Math.Max(0, Math.Min((int)((y - _ymin) / _sBin), _nRow - 1));
                var iCol = Math.Max(0, Math.Min((int)((x - _xmin) / _sBin), _nCol - 1));
                var index = iRow * _nCol + iCol;
                var n = iCount[index];
                var bin = _bins[index];

                var merged = false;
                for (var i = 0; i < n; i++)
                {
                    var dx = x - bin[i].GetX();
                    var dy = y - bin[i].GetY();
                    if (dx * dx + dy * dy < m2)
                    {
                        // For simplicity, we'll just keep the first vertex found at this location
                        // In a full implementation, you might want to merge the Z values
                        merged = true;
                        break;
                    }
                }

                if (!merged)
                {
                    bin[n] = v;
                    iCount[index]++;
                }
            }

            // Resize arrays if any merges occurred
            for (var i = 0; i < BinCount; i++)
                if (iCount[i] < _bins[i].Length)
                    Array.Resize(ref _bins[i], iCount[i]);
        }
        else
        {
            // No mergers required, just add to appropriate bin
            foreach (var v in vertices)
            {
                var x = v.GetX();
                var y = v.GetY();
                var iRow = Math.Max(0, Math.Min((int)((y - _ymin) / _sBin), _nRow - 1));
                var iCol = Math.Max(0, Math.Min((int)((x - _xmin) / _sBin), _nCol - 1));
                var index = iRow * _nCol + iCol;
                _bins[index][iCount[index]] = v;
                iCount[index]++;
            }
        }
    }

    /// <summary>
    ///     Gets the number of bins in the grid.
    /// </summary>
    public int BinCount { get; }

    /// <summary>
    ///     Gets the bounds of the data.
    /// </summary>
    public (double XMin, double XMax, double YMin, double YMax) Bounds =>
        (_xmin, _xmax, _ymin, _ymax);

    /// <summary>
    ///     Gets the grid dimensions.
    /// </summary>
    public (int Rows, int Columns) GridDimensions => (_nRow, _nCol);

    /// <summary>
    ///     Get the K nearest neighbors from the collection.
    /// </summary>
    /// <param name="x">The x coordinate of the search position</param>
    /// <param name="y">The y coordinate of the search position</param>
    /// <param name="k">The target number of vertices to be collected</param>
    /// <param name="distances">Storage for distances (must be at least size k)</param>
    /// <param name="vertices">Storage for vertices (must be at least size k)</param>
    /// <returns>The number of neighboring vertices identified</returns>
    public int GetNearestNeighbors(double x, double y, int k, double[] distances, IVertex[] vertices)
    {
        if (k <= 0) return 0;
        if (distances.Length < k) throw new ArgumentException("Distances array too small");
        if (vertices.Length < k) throw new ArgumentException("Vertices array too small");

        var binSearched = new bool[_bins.Length];
        var iRow = LimitIndex((int)((y - _ymin) / _sBin), _nRow);
        var iCol = LimitIndex((int)((x - _xmin) / _sBin), _nCol);
        var bIndex = iRow * _nCol + iCol;

        binSearched[bIndex] = true;
        var n = Gather(0, k, x, y, distances, vertices, _bins[bIndex]);

        // Search expanding tiers until we have enough points or no more improvement
        for (var iTier = 1; iTier < _nTier; iTier++)
        {
            var searched = n < k;
            var i0 = LimitIndex(iRow - iTier, _nRow);
            var i1 = LimitIndex(iRow + iTier, _nRow);
            var j0 = LimitIndex(iCol - iTier, _nCol);
            var j1 = LimitIndex(iCol + iTier, _nCol);

            for (var i = i0; i <= i1; i++)
            for (var j = j0; j <= j1; j++)
            {
                bIndex = i * _nCol + j;
                if (binSearched[bIndex]) continue;
                binSearched[bIndex] = true;

                if (IsBinWorthSearching(n, k, x, y, i, j, distances))
                {
                    searched = true;
                    n = Gather(n, k, x, y, distances, vertices, _bins[bIndex]);
                }
            }

            if (!searched)

                // No bins in this tier were worth searching
                break;
        }

        return n;
    }

    /// <summary>
    ///     Get the K nearest neighbors from the collection as lists.
    /// </summary>
    /// <param name="x">The x coordinate of the search position</param>
    /// <param name="y">The y coordinate of the search position</param>
    /// <param name="k">The target number of vertices to be collected</param>
    /// <returns>A list of the nearest vertices with their distances</returns>
    public List<(IVertex Vertex, double Distance)> GetNearestNeighbors(double x, double y, int k)
    {
        if (k <= 0) return new List<(IVertex, double)>();

        var distances = new double[k];
        var vertices = new IVertex[k];

        var found = GetNearestNeighbors(x, y, k, distances, vertices);

        var result = new List<(IVertex, double)>(found);
        for (var i = 0; i < found; i++) result.Add((vertices[i], Math.Sqrt(distances[i])));

        return result;
    }

    /// <summary>
    ///     Gets a list of all vertices currently stored in the collection.
    ///     The result may be slightly smaller than the original input if merge rules
    ///     were in effect and caused some co-located vertices to be merged.
    /// </summary>
    /// <returns>A list of all vertices in the collection</returns>
    public List<IVertex> GetVertices()
    {
        var totalCount = 0;
        for (var i = 0; i < BinCount; i++) totalCount += _bins[i].Length;

        var result = new List<IVertex>(totalCount);
        for (var i = 0; i < BinCount; i++) result.AddRange(_bins[i]);

        return result;
    }

    /// <summary>
    ///     Gathers the nearest k Vertices in a bin, replacing any previously
    ///     collected points if better candidates are found.
    /// </summary>
    /// <param name="previousCount">The previous count</param>
    /// <param name="k">The target count</param>
    /// <param name="x">The x coordinate of the query position</param>
    /// <param name="y">The y coordinate of the query position</param>
    /// <param name="distances">Storage for distances</param>
    /// <param name="vertices">Storage for vertices</param>
    /// <param name="bin">The array of vertices for the bin to be processed</param>
    /// <returns>The number of vertices stored</returns>
    private static int Gather(
        int previousCount,
        int k,
        double x,
        double y,
        double[] distances,
        IVertex[] vertices,
        IVertex[] bin)
    {
        if (bin.Length == 0) return previousCount;

        var n = previousCount;
        var i = 0;

        // Handle the first vertex if this is the first bin
        if (n < k)
        {
            if (n == 0)
            {
                distances[0] = bin[0].GetDistanceSq(x, y);
                vertices[0] = bin[0];
                n = 1;
                i = 1;
            }

            // Fill up to k vertices while maintaining sorted order
            while (i < bin.Length && n < k)
            {
                var vTest = bin[i++];
                var dTest = vTest.GetDistanceSq(x, y);

                var insertIndex = Array.BinarySearch(distances, 0, n, dTest);
                if (insertIndex < 0) insertIndex = ~insertIndex;

                // Shift elements to make room for insertion
                if (insertIndex < n)
                {
                    Array.Copy(distances, insertIndex, distances, insertIndex + 1, n - insertIndex);
                    Array.Copy(vertices, insertIndex, vertices, insertIndex + 1, n - insertIndex);
                }

                distances[insertIndex] = dTest;
                vertices[insertIndex] = vTest;
                n++;
            }
        }

        // Process remaining vertices, replacing worse ones
        while (i < bin.Length)
        {
            var vTest = bin[i++];
            var dTest = vTest.GetDistanceSq(x, y);

            if (dTest < distances[n - 1])
            {
                var insertIndex = Array.BinarySearch(distances, 0, n - 1, dTest);
                if (insertIndex < 0) insertIndex = ~insertIndex;

                // Shift elements to make room for insertion
                Array.Copy(distances, insertIndex, distances, insertIndex + 1, n - 1 - insertIndex);
                Array.Copy(vertices, insertIndex, vertices, insertIndex + 1, n - 1 - insertIndex);

                distances[insertIndex] = dTest;
                vertices[insertIndex] = vTest;
            }
        }

        return n;
    }

    /// <summary>
    ///     Given a row/column index, constrains it to the range of available bins.
    /// </summary>
    /// <param name="index">The computed coordinate index</param>
    /// <param name="n">The number of available bins</param>
    /// <returns>A value in the range 0 to n-1</returns>
    private static int LimitIndex(int index, int n)
    {
        if (index < 0) return 0;
        if (index >= n) return n - 1;
        return index;
    }

    /// <summary>
    ///     Indicates whether the specified bin could possibly contain a point that
    ///     is closer than the maximum-distance point yet located.
    /// </summary>
    /// <param name="n">The number of points collected so far</param>
    /// <param name="k">The number of points to be collected</param>
    /// <param name="x">The x coordinate of the query position</param>
    /// <param name="y">The y coordinate of the query position</param>
    /// <param name="iRow">The row of the bin</param>
    /// <param name="iCol">The column of the bin</param>
    /// <param name="distances">The array of distances for samples collected so far</param>
    /// <returns>True if the bin needs to be searched; otherwise, false</returns>
    private bool IsBinWorthSearching(int n, int k, double x, double y, int iRow, int iCol, double[] distances)
    {
        if (n < k) return true;

        // Find the x-coordinate offset to the nearest edge of the bin
        var cx = x - (_xmin + iCol * _sBin);
        if (cx > 0)
        {
            if (cx > _sBin) cx -= _sBin;
            else cx = 0; // x within range of the bin
        }

        // Find the y-coordinate offset to the nearest edge of the bin
        var cy = y - (_ymin + iRow * _sBin);
        if (cy > 0)
        {
            if (cy > _sBin) cy -= _sBin;
            else cy = 0; // y within range of the bin
        }

        var dc = cx * cx + cy * cy;
        var dMax = distances[n - 1];
        return dc <= 1.000001 * dMax; // A little extra for round-off
    }
}