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
 * 03/2016  G. Lucas     Created
 * 08/2025 M.Fender     Ported to C#
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

/// <summary>
///     A utility for performing the bootstrap operation that is common
///     to both standard and virtual incremental TIN implementations.
/// </summary>
public class BootstrapUtility
{
    /// <summary>
    ///     An arbitrary maximum number of trials for selecting a bootstrap triangle.
    /// </summary>
    private const int NTrialMax = 16;

    /// <summary>
    ///     An arbitrary minimum number of trials for selecting a bootstrap triangle.
    /// </summary>
    private const int NTrialMin = 3;

    /// <summary>
    ///     An arbitrary factor for estimating the number of trials for selecting a bootstrap triangle.
    /// </summary>
    private const double TrialFactor = 1.0 / 3.0;

    /// <summary>
    ///     An arbitrary factor for computing the triangle min-area threshold.
    /// </summary>
    private static readonly double MinAreaFactor = Math.Sqrt(3.0) / 4.0 / 64.0;

    /// <summary>
    ///     A set of geometric calculations tuned to the threshold settings.
    /// </summary>
    private readonly GeometricOperations _geometricOps;

    /// <summary>
    ///     Random number generator for bootstrap operations.
    /// </summary>
    private readonly Random _random = new(0);

    /// <summary>
    ///     The threshold for determining whether the initial random-triangle selection
    ///     produced a sufficiently robust triangle to begin triangulation.
    /// </summary>
    private readonly double _triangleMinAreaThreshold;

    /// <summary>
    ///     Initializes a new instance of the BootstrapUtility class.
    /// </summary>
    /// <param name="thresholds">Thresholds for geometric calculations</param>
    public BootstrapUtility(Thresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);

        _triangleMinAreaThreshold = thresholds.GetNominalPointSpacing() * MinAreaFactor;
        _geometricOps = new GeometricOperations(thresholds);
    }

    /// <summary>
    ///     Obtains the initial three vertices for building the mesh by selecting from
    ///     the input list with logic to identify an initial triangle with non-trivial area.
    /// </summary>
    /// <param name="list">A valid list of input vertices</param>
    /// <returns>If successful, a valid array of the initial three vertices; otherwise null</returns>
    public IVertex[]? Bootstrap(IList<IVertex> list)
    {
        if (list == null || list.Count < 3) return null;

        var vertices = new IVertex[3];
        var testVertices = new IVertex[3];
        var n = list.Count;
        var nTrial = ComputeNumberOfTrials(n);

        var bestScore = double.NegativeInfinity;

        // Attempt random selection first
        for (var trial = 0; trial < nTrial; trial++)
        {
            if (n == 3)
            {
                testVertices[0] = list[0];
                testVertices[1] = list[1];
                testVertices[2] = list[2];
            }
            else
            {
                // Pick three unique vertices at random
                for (var i = 0; i < 3; i++)
                    while (true)
                    {
                        var index = _random.Next(n);
                        testVertices[i] = list[index];
                        var unique = true;
                        for (var j = 0; j < i; j++)
                            if (testVertices[j].Equals(testVertices[i]))
                            {
                                unique = false;
                                break;
                            }

                        if (unique) break;
                    }
            }

            var area = _geometricOps.Area(testVertices[0], testVertices[1], testVertices[2]);
            if (area == 0) continue;

            if (area < 0)
            {
                // Swap to ensure positive orientation
                (testVertices[0], testVertices[2]) = (testVertices[2], testVertices[0]);
                area = -area;
            }

            if (area > bestScore)
            {
                bestScore = area;
                vertices[0] = testVertices[0];
                vertices[1] = testVertices[1];
                vertices[2] = testVertices[2];
            }
        }

        if (bestScore >= _triangleMinAreaThreshold) return vertices;

        if (n == 3)

            // Already tested this case above
            return null;

        // Try more sophisticated selection
        var testList = new List<IVertex>(3);
        var testResult = TestInput(list, testList);
        if (testResult == BootstrapTestResult.Valid) return testList.ToArray();

        if (testResult != BootstrapTestResult.Unknown)

            // Pathological case detected
            return null;

        // Last resort: exhaustive search
        return ExhaustiveSearch(list, bestScore);
    }

    /// <summary>
    ///     Tests input vertices to detect pathological cases and attempt to find a valid triangle.
    /// </summary>
    /// <param name="input">The input vertex list</param>
    /// <param name="output">The output list to populate with selected vertices</param>
    /// <returns>The result of the bootstrap test</returns>
    public BootstrapTestResult TestInput(IList<IVertex> input, IList<IVertex> output)
    {
        if (input == null || input.Count < 3) return BootstrapTestResult.InsufficientPointSet;

        output.Clear();
        var n = input.Count;

        // Compute mean coordinates
        double xBar = 0, yBar = 0;
        foreach (var vertex in input)
        {
            xBar += vertex.GetX();
            yBar += vertex.GetY();
        }

        xBar /= n;
        yBar /= n;

        // Compute variance-covariance terms
        double xy = 0, x2 = 0, y2 = 0;
        foreach (var vertex in input)
        {
            var x = vertex.GetX() - xBar;
            var y = vertex.GetY() - yBar;
            xy += x * y;
            x2 += x * x;
            y2 += y * y;
        }

        var thresholds = _geometricOps.GetThresholds();
        var samePoint2 = thresholds.GetVertexTolerance2();

        if (x2 <= samePoint2 && y2 <= samePoint2) return BootstrapTestResult.TrivialPointSet;

        // Find principal direction using linear regression
        var twoTheta = Math.Atan2(2 * xy, x2 - y2);
        var sin2T = Math.Sin(twoTheta);
        var cos2T = Math.Cos(twoTheta);

        var secondDerivative = 2 * (x2 - y2) * cos2T + 4 * xy * sin2T;
        var theta = twoTheta / 2;
        if (secondDerivative < -thresholds.GetHalfPlaneThreshold()) theta += Math.PI / 2;

        var uX = Math.Cos(theta);
        var uY = Math.Sin(theta);
        var pX = -uY; // perpendicular direction
        var pY = uX;

        // Find vertex farthest from the regression line
        var sMax = double.NegativeInfinity;
        var maxIndex = -1;
        var vertexA = Vertex.Null;

        for (var i = 0; i < n; i++)
        {
            var vertex = input[i];
            var x = vertex.GetX() - xBar;
            var y = vertex.GetY() - yBar;
            var s = Math.Abs(x * pX + y * pY);
            if (s > sMax)
            {
                sMax = s;
                maxIndex = i;
                vertexA = vertex;
            }
        }

        if (sMax < thresholds.GetHalfPlaneThreshold()) return BootstrapTestResult.CollinearPointSet;

        // Find vertices at extremes along the regression line
        var tMin = double.PositiveInfinity;
        var tMax = double.NegativeInfinity;
        var vertexB = Vertex.Null;
        var vertexC = Vertex.Null;

        for (var i = 0; i < n; i++)
            if (i != maxIndex)
            {
                var vertex = input[i];
                var x = vertex.GetX() - xBar;
                var y = vertex.GetY() - yBar;
                var t = x * uX + y * uY;
                if (t > tMax)
                {
                    tMax = t;
                    vertexB = vertex;
                }

                if (t < tMin)
                {
                    tMin = t;
                    vertexC = vertex;
                }
            }

        if (vertexA.IsNullVertex() || vertexB.IsNullVertex() || vertexC.IsNullVertex())
            return BootstrapTestResult.InsufficientPointSet;

        // Try to improve the triangle by random sampling
        var areaMax = Math.Abs(_geometricOps.Area(vertexA, vertexB, vertexC));
        if (vertexA.GetDistance(vertexC) > vertexA.GetDistance(vertexB)) (vertexB, vertexC) = (vertexC, vertexB);

        var bestC = vertexC;
        var nTrial = ComputeNumberOfTrials(n);
        for (var trial = 0; trial < nTrial; trial++)
        {
            var index = _random.Next(n);
            var candidate = input[index];
            var area = Math.Abs(_geometricOps.Area(vertexA, vertexB, candidate));
            if (area > areaMax)
            {
                areaMax = area;
                bestC = candidate;
            }
        }

        vertexC = bestC;

        // Try semi-exhaustive search if still below threshold
        if (areaMax < _triangleMinAreaThreshold)
            foreach (var vertex in input)
            {
                var area = Math.Abs(_geometricOps.Area(vertexA, vertexB, vertex));
                if (area > _triangleMinAreaThreshold)
                {
                    vertexC = vertex;
                    areaMax = area;
                    break;
                }
            }

        // Ensure positive orientation
        var finalArea = _geometricOps.Area(vertexA, vertexB, vertexC);
        if (finalArea < 0)
        {
            (vertexB, vertexC) = (vertexC, vertexB);
            finalArea = -finalArea;
        }

        if (finalArea > _triangleMinAreaThreshold)
        {
            output.Add(vertexA);
            output.Add(vertexB);
            output.Add(vertexC);
            return BootstrapTestResult.Valid;
        }

        return BootstrapTestResult.Unknown;
    }

    /// <summary>
    ///     Computes the number of trials to attempt based on vertex count.
    /// </summary>
    /// <param name="nVertices">The number of vertices</param>
    /// <returns>The number of trials to attempt</returns>
    private static int ComputeNumberOfTrials(int nVertices)
    {
        var nTrial = (int)Math.Pow(nVertices, TrialFactor);
        if (nTrial < NTrialMin) nTrial = NTrialMin;
        else if (nTrial > NTrialMax) nTrial = NTrialMax;
        return nTrial;
    }

    /// <summary>
    ///     Performs an exhaustive search for a valid bootstrap triangle.
    /// </summary>
    /// <param name="list">The list of vertices</param>
    /// <param name="currentBestScore">The current best area score</param>
    /// <returns>A valid triangle or null if none found</returns>
    private IVertex[]? ExhaustiveSearch(IList<IVertex> list, double currentBestScore)
    {
        var vertices = new IVertex[3];
        var testVertices = new IVertex[3];
        var bestScore = currentBestScore;
        var n = list.Count;

        for (var i = 0; i < n - 2; i++)
        {
            testVertices[0] = list[i];
            for (var j = i + 1; j < n - 1; j++)
            {
                testVertices[1] = list[j];
                for (var k = j + 1; k < n; k++)
                {
                    testVertices[2] = list[k];
                    var area = _geometricOps.Area(testVertices[0], testVertices[1], testVertices[2]);
                    var absArea = Math.Abs(area);

                    if (absArea > bestScore)
                    {
                        bestScore = absArea;
                        if (area < 0)
                        {
                            vertices[0] = testVertices[2];
                            vertices[1] = testVertices[1];
                            vertices[2] = testVertices[0];
                        }
                        else
                        {
                            vertices[0] = testVertices[0];
                            vertices[1] = testVertices[1];
                            vertices[2] = testVertices[2];
                        }

                        if (absArea >= _triangleMinAreaThreshold) return vertices;
                    }
                }
            }
        }

        return null; // No suitable triangle found
    }
}