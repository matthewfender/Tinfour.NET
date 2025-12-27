/*
 * Copyright 2025 G.W. Lucas
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

namespace Tinfour.Core.Interpolation;

using Tinfour.Core.Common;
using Tinfour.Core.Utils;

/// <summary>
///     Provides interpolation based on the classic method of inverse distance
///     weighting (IDW).
///     <para>
///         This class is intended primarily for diagnostic purposes and does not
///         implement a comprehensive set of options in support of the
///         inverse distance-weighting concept.
///     </para>
/// </summary>
public class InverseDistanceWeightingInterpolator : IInterpolatorOverTin
{
    private readonly VertexValuatorDefault _defaultValuator = new();

    private readonly IdwVariation _idwVariation;

    private readonly double _lambda;

    private readonly int _maxDepth = 3;

    private readonly int _minPoints = 6;

    private readonly IIncrementalTinNavigator _navigator;

    private readonly double _power;

    private readonly double _precisionThreshold;

    private readonly KahanSummation _sumDist = new();

    private readonly double _vertexTolerance2; // square of vertexTolerance

    private long _nInterpolation;

    private long _sumVertexCount;

    // Backing field for MaxInterpolationDistance
    private double? _maxInterpolationDistance;

    // Cached squared value of MaxInterpolationDistance for performance
    private double _maxInterpolationDistance2;

    /// <inheritdoc />
    public double? MaxInterpolationDistance
    {
        get => _maxInterpolationDistance;
        set
        {
            _maxInterpolationDistance = value;
            _maxInterpolationDistance2 = value.HasValue ? value.Value * value.Value : double.MaxValue;
        }
    }

    /// <summary>
    ///     Construct an interpolator that operates on the specified TIN.
    ///     Because the interpolator will access the TIN on a read-only basis,
    ///     it is possible to construct multiple instances of this class and
    ///     allow them to operate in parallel threads.
    ///     <para>
    ///         <strong>Important Synchronization Issue</strong>
    ///         To improve performance, the classes in this package
    ///         frequently maintain state-data about the TIN that can be reused
    ///         for query to query. They also avoid run-time overhead by not
    ///         implementing any kind of synchronization. If an application modifies the TIN,
    ///         instances of this class will not be aware of the change. In such cases,
    ///         interpolation methods may fail by either throwing an exception or,
    ///         worse, returning an incorrect value. The onus is on the calling
    ///         application to manage the use of this class and to ensure that
    ///         no modifications are made to the TIN between interpolation operations.
    ///         If the TIN is modified, the internal state data for this class must
    ///         be reset using a call to resetForChangeToTIN().
    ///     </para>
    ///     <para>
    ///         This constructor creates an interpolator based on Shepard's classic
    ///         weight = 1/(d^2) formula.
    ///     </para>
    /// </summary>
    /// <param name="tin">A valid instance of an incremental TIN.</param>
    /// <param name="constrainedRegionsOnly">
    ///     Flag indicating whether to restrict
    ///     interpolation to constrained regions only.
    /// </param>
    public InverseDistanceWeightingInterpolator(IIncrementalTin tin, bool constrainedRegionsOnly = false)
    {
        _idwVariation = IdwVariation.Shepard;
        var thresholds = tin.GetThresholds();

        _vertexTolerance2 = thresholds.GetVertexTolerance2();
        _precisionThreshold = thresholds.GetPrecisionThreshold();

        _navigator = tin.GetNavigator();
        _lambda = 3.5 / 2;
        _power = 2.0;
        ConstrainedRegionsOnly = constrainedRegionsOnly;
    }

    /// <summary>
    ///     Constructs an interpolator using the specified method.
    ///     <para>
    ///         <strong>Gaussian Kernel:</strong> If the Gaussian Kernel option is
    ///         specified, the parameter will be interpreted as the bandwidth (lambda) for
    ///         the formula weight = exp(-(1/2)(d/lambda)).
    ///     </para>
    ///     <para>
    ///         <strong>Power formula:</strong> If the Gaussian Kernel options is not
    ///         specified, the parameter will be interpreted as a power for the formula
    ///         weight = 1/pow(d, power);
    ///     </para>
    /// </summary>
    /// <param name="tin">A valid TIN</param>
    /// <param name="parameter">
    ///     A parameter specifying either bandwidth (for Gaussian)
    ///     or power. In both cases the parameter must be greater than zero.
    /// </param>
    /// <param name="gaussian">
    ///     True if the Gaussian kernel is to be used; otherwise
    ///     the non-Gaussian method (power) will be used
    /// </param>
    /// <param name="constrainedRegionsOnly">
    ///     Flag indicating whether to restrict
    ///     interpolation to constrained regions only.
    /// </param>
    public InverseDistanceWeightingInterpolator(
        IIncrementalTin tin,
        double parameter,
        bool gaussian,
        bool constrainedRegionsOnly = false)
    {
        var thresholds = tin.GetThresholds();

        _vertexTolerance2 = thresholds.GetVertexTolerance2();
        _precisionThreshold = thresholds.GetPrecisionThreshold();
        _navigator = tin.GetNavigator();
        if (gaussian)
        {
            _lambda = parameter;
            _power = 0;
            _idwVariation = IdwVariation.GaussianKernel;
        }
        else
        {
            _idwVariation = IdwVariation.Power;
            _power = parameter;
            _lambda = 0;
        }

        ConstrainedRegionsOnly = constrainedRegionsOnly;
    }

    private enum IdwVariation
    {
        Shepard,

        Power,

        GaussianKernel
    }

    public bool ConstrainedRegionsOnly { get; }

    /// <summary>
    ///     Computes the average sample spacing.
    ///     <para>
    ///         In many cases, the sample spacing of the perimeter edges of a TIN
    ///         is much larger than the mean sample spacing for the interior.
    ///         So if the TIN contains more than 3 points, the perimeter edges are
    ///         not included in the tabulation.
    ///     </para>
    /// </summary>
    /// <param name="tin">A valid instance</param>
    /// <returns>If the TIN is valid, a positive value; otherwise Double.NaN</returns>
    public static double ComputeAverageSampleSpacing(IIncrementalTin tin)
    {
        var kahanSum = new KahanSummation();
        var nSum = 0;

        foreach (var e in tin.GetEdges())
        {
            kahanSum.Add(e.GetLength());
            nSum++;
        }

        if (nSum == 0)

            // only occurs if the tin has not been properly bootstrapped
            return double.NaN;

        if (nSum == 3)

            // there are only 3 edges, so we don't need to remove the perimeter
            return kahanSum.GetMean();

        // remove the perimeter edges
        foreach (var e in tin.GetPerimeter())
        {
            kahanSum.Add(-e.GetLength());
            nSum--;
        }

        return kahanSum.GetSum() / nSum;
    }

    /// <summary>
    ///     Estimates a nominal bandwidth for the Gaussian kernel method of
    ///     interpolation using the mean length of the distances between samples.
    /// </summary>
    /// <param name="pointSpacing">A positive value</param>
    /// <returns>If successful, a positive value; otherwise, Double.NaN</returns>
    public static double EstimateNominalBandwidth(double pointSpacing)
    {
        if (pointSpacing <= 0) return double.NaN;
        return -Math.Log(1 / 12.0) * pointSpacing / 2;
    }

    /// <summary>
    ///     Gets a string indicating the interpolation method.
    /// </summary>
    /// <returns>A descriptive string.</returns>
    public string GetMethod()
    {
        if (_idwVariation == IdwVariation.GaussianKernel) return $"IDW (Gaussian: {_lambda:F2})";
        return $"IDW (Power: {_power:F2})";
    }

    /// <summary>
    ///     Gets the surface normal for the most recent interpolation.
    /// </summary>
    /// <returns>A zero-sized array (not supported by this interpolator).</returns>
    public double[] GetSurfaceNormal()
    {
        return Array.Empty<double>();
    }

    /// <summary>
    ///     Perform inverse distance weighting interpolation.
    ///     <para>
    ///         This interpolation is not defined beyond the convex hull of the TIN
    ///         and this method will produce a Double.NaN if the specified coordinates
    ///         are exterior to the TIN.
    ///     </para>
    /// </summary>
    /// <param name="x">The x coordinate for the interpolation point</param>
    /// <param name="y">The y coordinate for the interpolation point</param>
    /// <param name="valuator">
    ///     A valid valuator for interpreting the z value of each
    ///     vertex or a null value to use the default.
    /// </param>
    /// <returns>
    ///     If the interpolation is successful, a valid floating point
    ///     value; otherwise, a NaN.
    /// </returns>
    public double Interpolate(double x, double y, IVertexValuator? valuator)
    {
        var vq = valuator ?? _defaultValuator;
        var edge = _navigator.GetNeighborEdge(x, y);

        if (edge == null)

            // this should happen only when TIN is not bootstrapped
            return double.NaN;

        // confirm that the query coordinates are inside the TIN
        var v0 = edge.GetA();
        var v1 = edge.GetB();
        var v2 = edge.GetForward().GetB();
        if (v2.IsNullVertex())

            // (x,y) is either on perimeter or outside the TIN.
            return double.NaN;

        // Check MaxInterpolationDistance if set
        if (_maxInterpolationDistance.HasValue)
        {
            var d0 = v0.GetDistanceSq(x, y);
            var d1 = v1.GetDistanceSq(x, y);
            var d2 = v2.GetDistanceSq(x, y);
            var minDistSq = Math.Min(d0, Math.Min(d1, d2));
            if (minDistSq > _maxInterpolationDistance2)
                return double.NaN;
        }

        if (ConstrainedRegionsOnly)
            if (!edge.IsConstraintRegionMember() || (!edge.IsConstraintRegionInterior()
                                                     && !edge.GetForward().IsConstraintRegionInterior()
                                                     && !edge.GetReverse().IsConstraintRegionInterior()))
                return double.NaN;

        // Collect neighboring vertices by traversing the TIN
        var neighbors = new List<Vertex>();

        // Get the vertices of the triangle containing the point
        var triangleVerts = new HashSet<Vertex>();

        if (!v0.IsNullVertex())
            triangleVerts.Add((Vertex)v0);

        if (!v1.IsNullVertex())
            triangleVerts.Add((Vertex)v1);

        if (!v2.IsNullVertex())
            triangleVerts.Add((Vertex)v2);

        // Add initial triangle vertices
        neighbors.AddRange(triangleVerts);

        // Expand to nearby triangles if needed (basic implementation)
        var depth = 0;
        var frontier = new List<IQuadEdge> { edge };
        var processed = new HashSet<IQuadEdge>();

        while (depth < _maxDepth && neighbors.Count < _minPoints && frontier.Count > 0)
        {
            var nextFrontier = new List<IQuadEdge>();

            foreach (var e in frontier)
            {
                if (processed.Contains(e))
                    continue;

                processed.Add(e);

                // Add the vertices from this edge
                var vertA = e.GetA();
                var vertB = e.GetB();

                if (!vertA.IsNullVertex() && !triangleVerts.Contains((Vertex)vertA))
                {
                    triangleVerts.Add((Vertex)vertA);
                    neighbors.Add((Vertex)vertA);
                }

                if (!vertB.IsNullVertex() && !triangleVerts.Contains((Vertex)vertB))
                {
                    triangleVerts.Add((Vertex)vertB);
                    neighbors.Add((Vertex)vertB);
                }

                // Add connected edges to frontier
                var dual = e.GetDual();
                if (dual != null && !processed.Contains(dual))
                    nextFrontier.Add(dual);

                var forward = e.GetForward();
                if (forward != null && !processed.Contains(forward))
                    nextFrontier.Add(forward);

                var reverse = e.GetReverse();
                if (reverse != null && !processed.Contains(reverse))
                    nextFrontier.Add(reverse);
            }

            frontier = nextFrontier;
            depth++;
        }

        var val = valuator ?? _defaultValuator;

        double wSum = 0;
        double wzSum = 0;
        double sSum = 0;

        foreach (var v in neighbors)
        {
            var z = val.Value(v);
            
            // Skip vertices with NaN Z values (e.g. constraints without elevation)
            if (double.IsNaN(z)) continue;

            var dx = v.X - x;
            var dy = v.Y - y;
            var s2 = dx * dx + dy * dy;
            var s = Math.Sqrt(s2);
            sSum += s;
            double w;

            switch (_idwVariation)
            {
                case IdwVariation.Shepard:
                    if (s2 < _vertexTolerance2)

                        // the distance is so small, we call it a match
                        return z;
                    w = 1 / s2;
                    break;
                case IdwVariation.Power:
                    if (s2 < _vertexTolerance2)

                        // the distance is so small, we call it a match
                        return z;
                    w = 1.0 / Math.Pow(s2, _power / 2);
                    break;
                case IdwVariation.GaussianKernel:
                default:
                    w = Math.Exp(-0.5 * s / _lambda);
                    break;
            }

            wSum += w;
            wzSum += z * w;
        }

        if (wSum < _precisionThreshold)

            // this should only happen if the neighbor point collector failed
            return double.NaN;

        _nInterpolation++;
        _sumVertexCount += neighbors.Count;
        _sumDist.Add(sSum);

        return wzSum / wSum;
    }

    /// <summary>
    ///     Tests whether the interpolator supports calculation of surface normals.
    /// </summary>
    /// <returns>Always returns false for this implementation.</returns>
    public bool IsSurfaceNormalSupported()
    {
        return false;
    }

    /// <summary>
    ///     Prints diagnostic information about sample sizes and spacing
    ///     used for interpolation.
    /// </summary>
    /// <param name="writer">A TextWriter instance to write to.</param>
    public void PrintDiagnostics(TextWriter writer)
    {
        long n = 1;
        if (_nInterpolation > 0) n = _nInterpolation;

        long nV = 1;
        if (_sumVertexCount > 0) nV = _sumVertexCount;

        writer.WriteLine($"Interpolations       {_nInterpolation, 10}");
        writer.WriteLine($"Avg. sample size:    {(double)_sumVertexCount / n, 12:F1}");
        writer.WriteLine($"Avg. sample spacing: {_sumDist.GetSum() / nV, 15:F4}");
    }

    /// <summary>
    ///     Used by an application to reset the state data within the interpolator
    ///     when the content of the TIN may have changed. Resetting the state data
    ///     unnecessarily may result in a performance reduction when processing
    ///     a large number of interpolations, but is otherwise harmless.
    /// </summary>
    public void ResetForChangeToTin()
    {
        _navigator.ResetForChangeToTin();
    }

    /// <summary>
    ///     Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        if (_idwVariation == IdwVariation.GaussianKernel) return $"IDW (Gaussian: {_lambda:F2})";

        return $"IDW (Power: {_power:F1})";
    }
}