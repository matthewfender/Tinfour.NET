/*
 * Copyright 2015-2025 Gary W. Lucas.
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
 * 04/2026  M.Fender     Initial implementation for constraint leak detection
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Utils;

using Tinfour.Core.Common;

/// <summary>
///     A diagnostic utility that identifies Steiner points that have escaped
///     outside constraint polygon boundaries during Ruppert refinement.
///     Detection uses both geometric point-in-polygon testing and constraint
///     region flag state to find divergences.
/// </summary>
public class ConstraintLeakDetector
{
    /// <summary>
    ///     A report summarizing the results of a constraint leak detection pass.
    /// </summary>
    /// <param name="LeakedCount">The number of Steiner points geometrically outside the constraint polygon.</param>
    /// <param name="TotalSteinerPoints">The total number of Steiner points examined.</param>
    /// <param name="LeakedPoints">The list of Steiner points that are geometrically outside the constraint polygon.</param>
    /// <param name="Divergences">The list of points where geometric PIP disagrees with constraint region flag state.</param>
    public record LeakReport(
        int LeakedCount,
        int TotalSteinerPoints,
        List<LeakedPoint> LeakedPoints,
        List<DivergencePoint> Divergences
    );

    /// <summary>
    ///     Describes a Steiner point that is geometrically outside the constraint polygon.
    /// </summary>
    /// <param name="X">The x coordinate of the leaked point.</param>
    /// <param name="Y">The y coordinate of the leaked point.</param>
    /// <param name="VertexIndex">The index of the vertex in the TIN.</param>
    public record LeakedPoint(double X, double Y, int VertexIndex);

    /// <summary>
    ///     Describes a Steiner point where the geometric PIP result disagrees
    ///     with the constraint region flag state on the containing triangle's edges.
    /// </summary>
    /// <param name="X">The x coordinate of the divergence point.</param>
    /// <param name="Y">The y coordinate of the divergence point.</param>
    /// <param name="VertexIndex">The index of the vertex in the TIN.</param>
    /// <param name="GeometryInside">True if the geometric PIP test says the point is inside the polygon.</param>
    /// <param name="FlagStateInside">True if the constraint region flag state says the point is inside the region.</param>
    public record DivergencePoint(
        double X, double Y, int VertexIndex,
        bool GeometryInside, bool FlagStateInside
    );

    /// <summary>
    ///     Detects Steiner points that have leaked outside a constraint polygon boundary
    ///     and identifies divergences between geometric PIP and flag state.
    /// </summary>
    /// <param name="tin">The incremental TIN to examine.</param>
    /// <param name="constraint">The constraint polygon to check against.</param>
    /// <param name="boundaryTolerance">
    ///     Points classified as Outside by PIP but within this distance of a constraint
    ///     edge are treated as on-boundary (not leaked). This handles Steiner points placed
    ///     exactly on constraint edges by segment splitting, where PIP ray-crossing can
    ///     return Outside due to floating-point precision. Default: 1e-6.
    /// </param>
    /// <returns>A <see cref="LeakReport"/> describing all leaked and divergent points.</returns>
    public static LeakReport Detect(IIncrementalTin tin, IConstraint constraint, double boundaryTolerance = 1e-6)
    {
        var constraintVertices = constraint.GetVertices();
        var navigator = tin.GetNavigator();

        var steinerPoints = tin.GetVertices().Where(v => v.IsSynthetic()).ToList();
        var leakedPoints = new List<LeakedPoint>();
        var divergences = new List<DivergencePoint>();

        foreach (var vertex in steinerPoints)
        {
            var x = vertex.X;
            var y = vertex.Y;

            // Geometric point-in-polygon check
            var pipResult = Polyside.IsPointInPolygon(constraintVertices, x, y);

            // For points classified as Outside, check if they are within tolerance
            // of a constraint edge. SplitSegmentSmart places Steiner points exactly on
            // constraint edges, and PIP ray-crossing can misclassify these as Outside.
            if (pipResult == Polyside.Result.Outside && boundaryTolerance > 0)
            {
                var dist = MinDistanceToPolygonEdge(x, y, constraintVertices);
                if (dist <= boundaryTolerance)
                    pipResult = Polyside.Result.Edge;
            }

            var geometryInside = pipResult != Polyside.Result.Outside;

            // Flag state check: navigate to the containing triangle and check
            // whether any of its three edges is a constraint region member
            var flagStateInside = false;
            var edge = navigator.GetNeighborEdge(x, y);
            var e0 = edge;
            var e1 = edge.GetForward();
            var e2 = e1.GetForward();

            if (e0.IsConstraintRegionMember() ||
                e1.IsConstraintRegionMember() ||
                e2.IsConstraintRegionMember())
            {
                flagStateInside = true;
            }

            // A point is "leaked" if it is geometrically OUTSIDE the polygon
            if (pipResult == Polyside.Result.Outside)
            {
                leakedPoints.Add(new LeakedPoint(x, y, vertex.GetIndex()));
            }

            // A "divergence" occurs when geometry and flag state disagree
            // (skip Edge results since they are ambiguous boundary points)
            if (pipResult != Polyside.Result.Edge && geometryInside != flagStateInside)
            {
                divergences.Add(new DivergencePoint(
                    x, y, vertex.GetIndex(),
                    geometryInside, flagStateInside));
            }
        }

        return new LeakReport(
            leakedPoints.Count,
            steinerPoints.Count,
            leakedPoints,
            divergences);
    }

    private static double MinDistanceToPolygonEdge(double px, double py, IList<IVertex> vertices)
    {
        var minDist = double.MaxValue;
        var n = vertices.Count;
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            var dist = PointToSegmentDistance(px, py,
                vertices[i].X, vertices[i].Y,
                vertices[j].X, vertices[j].Y);
            if (dist < minDist) minDist = dist;
        }
        return minDist;
    }

    private static double PointToSegmentDistance(double px, double py, double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-30)
            return Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));

        var t = Math.Max(0, Math.Min(1, ((px - ax) * dx + (py - ay) * dy) / lenSq));
        var projX = ax + t * dx;
        var projY = ay + t * dy;
        return Math.Sqrt((px - projX) * (px - projX) + (py - projY) * (py - projY));
    }
}
