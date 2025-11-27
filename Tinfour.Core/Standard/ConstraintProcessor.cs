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

namespace Tinfour.Core.Standard;

using System.Collections;
using System.Diagnostics;

using Tinfour.Core.Common;
using Tinfour.Core.Edge;

/// <summary>
///     Constraint processing ported from Java IncrementalTin.
///     Implements pinwheel search, edge removal, constraint insertion,
///     cavity fill, and local Delaunay restoration.
/// </summary>
public class ConstraintProcessor
{
    private readonly EdgePool _edgePool;

    private readonly GeometricOperations _geoOp;

    private readonly Thresholds _thresholds;

    private readonly StochasticLawsonsWalk _walker;

    internal ConstraintProcessor(
        EdgePool edgePool,
        GeometricOperations geoOp,
        Thresholds thresholds,
        StochasticLawsonsWalk walker)
    {
        _edgePool = edgePool;
        _geoOp = geoOp;
        _thresholds = thresholds;
        _walker = walker;
    }

    internal void FloodFillConstrainedRegion(
        IConstraint constraint,
        BitArray visited,
        List<IQuadEdge> edgesForConstraint)
    {
        if (edgesForConstraint.Count == 0) return;
        var constraintIndex = constraint.GetConstraintIndex();

        Debug.WriteLine(
            $"FloodFillConstrainedRegion: Starting for constraint index {constraintIndex} with {edgesForConstraint.Count} border edges");

        foreach (var e in edgesForConstraint)
            if (e.IsConstraintRegionBorder())
                FloodFillConstrainedRegionsQueue(constraintIndex, visited, e);
    }

    internal IQuadEdge? ProcessConstraint(
        IConstraint constraint,
        List<IQuadEdge> edgesForConstraint,
        IQuadEdge? searchEdge)
    {
        // Build working list of vertices and close polygon loop if needed
        var cvList = new List<IVertex>(constraint.GetVertices());
        if (constraint.IsPolygon()) cvList.Add(cvList[0]);
        var nSegments = cvList.Count - 1;

        // Establish starting edge e0 incident to v0
        var v0 = cvList[0];
        double x0 = v0.X, y0 = v0.Y;

        searchEdge ??= _edgePool.GetStartingEdge();
        if (searchEdge == null) return null;

        searchEdge = _walker.FindAnEdgeFromEnclosingTriangle(searchEdge, x0, y0);

        QuadEdge e0;
        if (IsMatchingVertex(v0, searchEdge.GetA())) e0 = (QuadEdge)searchEdge;
        else if (IsMatchingVertex(v0, searchEdge.GetB())) e0 = (QuadEdge)searchEdge.GetDual();
        else e0 = (QuadEdge)searchEdge.GetReverse();

        // Replace constraint vertices with TIN vertices using Java algorithm
        var a = e0.GetA();
        if (a != v0 && a is VertexMergerGroup group && group.Contains(v0.AsVertex())) cvList[0] = a;

        // searchEdge may be invalidated by subsequent edits
        searchEdge = null;

        var vTol = _thresholds.GetVertexTolerance();
        var successfulSegments = 0;

        // Segment loop - process each constraint segment
        for (var iSegment = 0; iSegment < nSegments; iSegment++)
        {
            v0 = cvList[iSegment];
            var v1 = cvList[iSegment + 1];

            Debug.WriteLine(
                $"ProcessConstraint: Processing segment {iSegment}/{nSegments - 1}: ({v0.X:F2},{v0.Y:F2}) -> ({v1.X:F2},{v1.Y:F2})");
            Debug.WriteLine($"ProcessConstraint: v0 index={v0.GetIndex()}, v1 index={v1.GetIndex()}");

            // Pinwheel at v0 to see if edge (v0,v1) already exists
            var e = e0;
            var priorNull = false;
            QuadEdge? reEntry = null;

            Debug.WriteLine(
                $"ProcessConstraint: Starting pinwheel search from edge {e0.GetIndex()} ({e0.GetA().GetIndex()}-{e0.GetB().GetIndex()})");
            var pinwheelStep = 0;

            do
            {
                var b = e.GetB();
                Debug.WriteLine(
                    $"ProcessConstraint: Pinwheel step {pinwheelStep}: edge {e.GetIndex()} -> vertex {(b.IsNullVertex() ? "NULL" : b.GetIndex().ToString())}({(b.IsNullVertex() ? "ghost" : $"{b.X:F1},{b.Y:F1}")})");

                if (b.IsNullVertex())
                {
                    priorNull = true;
                    Debug.WriteLine("ProcessConstraint: Found null vertex (ghost edge)");
                }
                else
                {
                    // Check if this edge connects v0 to v1 using Java algorithm
                    var directMatch = b.Equals(v1); // ReferenceEquals(b, v1);
                    Debug.WriteLine(
                        $"ProcessConstraint: Testing vertex {b.GetIndex()} against target {v1.GetIndex()}: Equals = {directMatch}");

                    if (directMatch)
                    {
                        Debug.WriteLine(
                            $"ProcessConstraint: Found existing edge for segment {iSegment} (direct match) - marking edge {e.GetIndex()} as constrained");
                        SetConstrained(e, constraint, edgesForConstraint);
                        e0 = (QuadEdge)e.GetDual();
                        successfulSegments++;
                        goto SegmentContinue; // next segment
                    }

                    if (b is VertexMergerGroup bGroup && bGroup.Contains(v1.AsVertex()))
                    {
                        Debug.WriteLine(
                            $"ProcessConstraint: Found existing edge for segment {iSegment} (merger group match)");
                        cvList[iSegment + 1] = b;
                        SetConstrained(e, constraint, edgesForConstraint);
                        e0 = (QuadEdge)e.GetDual();
                        successfulSegments++;
                        goto SegmentContinue; // next segment
                    }

                    if (priorNull) reEntry = e;
                    priorNull = false;
                }

                e = (QuadEdge)e.GetDualFromReverse()!;
                pinwheelStep++;

                if (pinwheelStep > 20)
                {
                    // Safety break
                    Debug.WriteLine($"ProcessConstraint: Breaking pinwheel after {pinwheelStep} steps (safety)");
                    break;
                }
            }
            while (!ReferenceEquals(e, e0));

            if (reEntry != null) e0 = reEntry;

            // Debug.WriteLine($"ProcessConstraint: No existing edge found, checking for intermediate vertex on constraint path");

            //// CRITICAL FIX: Check for intermediate vertices like Java does
            //// Java automatically finds vertex 4 (center) between 300 and 301
            // var intermediateVertex = FindIntermediateVertexOnPath(v0, v1, e0, vTol);
            // if (intermediateVertex != null && !IsMatchingVertex(intermediateVertex, v1))
            // {
            // Debug.WriteLine($"ProcessConstraint: Found intermediate vertex {intermediateVertex.GetIndex()} on constraint path");
            // Debug.WriteLine($"ProcessConstraint: Splitting constraint segment: {v0.GetIndex()} ? {intermediateVertex.GetIndex()} ? {v1.GetIndex()}");

            // // Insert the intermediate vertex and split the segment
            // cvList.Insert(iSegment + 1, intermediateVertex);
            // nSegments++;

            // // Find and mark the edge from v0 to intermediate vertex
            // var edgeToIntermediate = FindEdgeConnecting(v0, intermediateVertex);
            // if (edgeToIntermediate != null)
            // {
            // Debug.WriteLine($"ProcessConstraint: Marking edge {edgeToIntermediate.GetIndex()} ({v0.GetIndex()}-{intermediateVertex.GetIndex()}) as constrained");
            // SetConstrained((QuadEdge)edgeToIntermediate, constraint, edgesForConstraint);
            // e0 = (QuadEdge)edgeToIntermediate.GetDual();
            // successfulSegments++;
            // goto SegmentContinue;
            // }
            // }

            // Debug.WriteLine($"ProcessConstraint: No existing edge found, starting tunneling for segment {iSegment}");

            // Prepare ray(v0->v1)
            x0 = v0.X;
            y0 = v0.Y;

            // double y0_seg = v0.Y;
            double x1 = v1.X, y1 = v1.Y;
            double ux = x1 - x0, uy = y1 - y0;
            var u = Math.Sqrt(ux * ux + uy * uy);
            if (u == 0) goto SegmentContinue; // degenerate segment
            ux /= u;
            uy /= u; // unit direction
            double px = -uy, py = ux; // perpendicular

            // Check if e0's B vertex is collinear and ahead of ray
            var b0 = e0.GetB();
            double bx = b0.X - x0, by = b0.Y - y0;
            var bh = bx * px + by * py;
            if (Math.Abs(bh) <= vTol && bx * ux + by * uy > 0)
            {
                cvList.Insert(iSegment + 1, b0);
                nSegments++;
                SetConstrained(e0, constraint, edgesForConstraint);
                e0 = (QuadEdge)e0.GetDual();
                successfulSegments++;
                goto SegmentContinue;
            }

            // Find straddle edge across the constraint segment
            QuadEdge? h = null;
            QuadEdge? right0 = null, left0 = null;
            QuadEdge? right1 = null, left1 = null;
            double ax = 0, ay = 0, ah = 0;

            e = e0;
            do
            {
                ax = bx;
                ay = by;
                ah = bh;
                var n = (QuadEdge)e.GetForward();
                var b = n.GetB();
                bx = b.X - x0;
                by = b.Y - y0;
                bh = bx * px + by * py;

                if (Math.Abs(bh) <= vTol)
                {
                    // Check if vertex is collinear and ahead
                    double dx = bx - ax, dy = by - ay;
                    var denom = ux * dy - uy * dx;
                    {
                        // if (denom != 0)
                        var t = (ax * dy - ay * dx) / denom;
                        if (t > 0)
                        {
                            cvList.Insert(iSegment + 1, b);
                            nSegments++;
                            e0 = (QuadEdge)e.GetReverse(); // (b,v0)
                            SetConstrained((QuadEdge)e0.GetDual(), constraint, edgesForConstraint);
                            successfulSegments++;
                            goto SegmentContinue;
                        }
                    }
                }

                var hab = ah * bh;
                if (hab <= 0)
                {
                    double dx = bx - ax, dy = by - ay;
                    var denom = ux * dy - uy * dx;
                    if (denom != 0)
                    {
                        var t = (ax * dy - ay * dx) / denom;
                        if (t > 0)
                        {
                            right0 = e;
                            left0 = (QuadEdge)e.GetReverse();
                            h = (QuadEdge)n.GetDual();
                            break;
                        }
                    }
                }

                e = (QuadEdge)e.GetDualFromReverse()!;
            }
            while (!ReferenceEquals(e, e0));

            if (h == null) throw new InvalidOperationException("Internal failure 332, constraint not added");

            // Tunnel across: remove crossed edges until next vertex lies on segment
            var c = v1; // Initialize with target vertex as fallback

            while (true)
            {
                right1 = (QuadEdge)h.GetForward();
                left1 = (QuadEdge)h.GetReverse();
                c = right1.GetB();
                if (c.IsNullVertex()) throw new InvalidOperationException("Internal failure 345, constraint not added");

                RemoveEdge(h);

                double cx = c.X - x0, cy = c.Y - y0;
                var ch = cx * px + cy * py;
                if (Math.Abs(ch) < vTol && cx * ux + cy * uy > 0)
                {
                    if (!IsMatchingVertex(c, v1))
                    {
                        if (c is VertexMergerGroup cGroup && cGroup.Contains(v1.AsVertex()))
                        {
                            cvList[iSegment + 1] = c;
                        }
                        else if (Near(c, v1, vTol))
                        {
                            cvList[iSegment + 1] = c;
                        }
                        else
                        {
                            cvList.Insert(iSegment + 1, c);
                            nSegments++;
                        }
                    }

                    break;
                }

                var hac = ah * ch;
                var hbc = bh * ch;
                if (hac == 0 || hbc == 0)
                    throw new InvalidOperationException("Internal failure 377, constraint not added");

                if (hac < 0)
                {
                    // branch right
                    h = (QuadEdge)right1.GetDual();
                    bx = cx;
                    by = cy;
                    bh = bx * px + by * py;
                }
                else
                {
                    // branch left
                    h = (QuadEdge)left1.GetDual();
                    ax = cx;
                    ay = cy;
                    ah = ax * px + ay * py;
                }
            }

            // Insert the constraint edge (v0,c) and wire topology
            var nc = (QuadEdge)_edgePool.AllocateEdge(v0.AsVertex(), c.AsVertex());
            SetConstrained(nc, constraint, edgesForConstraint);
            var dc = (QuadEdge)nc.GetDual();

            Debug.WriteLine(
                $"ProcessConstraint: Created constraint edge {nc.GetIndex()} ({v0.X:F2},{v0.Y:F2})->({c.X:F2},{c.Y:F2})");

            if (left1 != null && left0 != null && right0 != null && right1 != null)
            {
                nc.SetForward(left1);
                nc.SetReverse(left0);
                dc.SetForward(right0);
                dc.SetReverse(right1);

                Debug.WriteLine(
                    $"ProcessConstraint: Wired topology - nc:{nc.GetIndex()} f:{left1.GetIndex()} r:{left0.GetIndex()}, dc:{dc.GetIndex()} f:{right0.GetIndex()} r:{right1.GetIndex()}");
            }
            else
            {
                Debug.WriteLine("ProcessConstraint: WARNING - null topology references during constraint edge wiring");
            }

            e0 = dc; // next segment will start here
            successfulSegments++;

            // Fill cavities on both sides
            FillCavity(nc);
            FillCavity(dc);

            SegmentContinue: ;
        }

        return e0;
    }

    private static bool Near(IVertex a, IVertex b, double tol)
    {
        return Math.Abs(a.X - b.X) <= tol && Math.Abs(a.Y - b.Y) <= tol;
    }

    private void FillCavity(QuadEdge cavityEdge)
    {
        Debug.WriteLine($"FillCavity: Starting cavity fill for edge {cavityEdge.GetIndex()}");

        // Build ear list around the polygonal cavity
        var n0 = cavityEdge;
        var n1 = (QuadEdge)n0.GetForward();
        var pStart = n0;
        var nEar = 0;

        var firstEar = new DevillersEar(nEar, null, n1, n0);
        var priorEar = firstEar;
        nEar = 1;
        do
        {
            n0 = n1;
            n1 = (QuadEdge)n1.GetForward();
            var ear = new DevillersEar(nEar, priorEar, n1, n0);
            priorEar = ear;
            nEar++;
        }
        while (!ReferenceEquals(n1, pStart));

        priorEar.Next = firstEar;
        firstEar.Prior = priorEar;

        Debug.WriteLine($"FillCavity: Built cavity with {nEar} ears");

        if (nEar == 3) return; // already a triangle

        // Score ears
        var eC = firstEar.Next!;
        FillScore(firstEar);
        while (!ReferenceEquals(eC, firstEar))
        {
            FillScore(eC);
            eC = eC.Next!;
        }

        var newEdges = new List<QuadEdge>();
        while (true)
        {
            DevillersEar? earMin = null;
            var minScore = double.PositiveInfinity;
            var ear = firstEar;
            do
            {
                if (ear.Score < minScore && ear.Score > 0)
                {
                    minScore = ear.Score;
                    earMin = ear;
                }

                ear = ear.Next!;
            }
            while (!ReferenceEquals(ear, firstEar));

            if (earMin == null)
                throw new InvalidOperationException("Unable to identify correct geometry for cavity fill");

            // Close ear with new edge v2->v0
            var prior = earMin.Prior!;
            var next = earMin.Next!;
            var e = (QuadEdge)_edgePool.AllocateEdge(earMin.V2, earMin.V0);
            var d = (QuadEdge)e.GetDual();
            e.SetForward(earMin.C);
            e.SetReverse(earMin.N);
            d.SetForward(next.N);
            d.SetReverse(prior.C);
            newEdges.Add(e);

            if (nEar == 4) break; // cavity filled

            // Link and rescore neighbors
            prior.Next = next;
            next.Prior = prior;
            prior.V2 = earMin.V2;
            prior.N = d;
            next.C = d;
            next.P = prior.C;
            next.V0 = earMin.V0;
            FillScore(prior);
            FillScore(next);
            firstEar = prior;
            nEar--;
        }

        // Restore Delaunay locally by flipping any non-Delaunay edges
        foreach (var n in newEdges) RecursiveRestoreDelaunay(n);
    }

    private void FillScore(DevillersEar ear)
    {
        ear.Score = _geoOp.Area(ear.V0, ear.V1, ear.V2);
        if (ear.Score > 0)
        {
            double x0 = ear.V0.X, y0 = ear.V0.Y;
            double x1 = ear.V1.X, y1 = ear.V1.Y;
            double x2 = ear.V2.X, y2 = ear.V2.Y;
            var e = ear.Next!;
            while (!ReferenceEquals(e, ear.Prior))
            {
                if (!(e.V2.Equals(ear.V0) || e.V2.Equals(ear.V1) || e.V2.Equals(ear.V2)))
                {
                    double x = e.V2.X, y = e.V2.Y;
                    if (_geoOp.HalfPlane(x0, y0, x1, y1, x, y) >= 0
                        && _geoOp.HalfPlane(x1, y1, x2, y2, x, y) >= 0
                        && _geoOp.HalfPlane(x2, y2, x0, y0, x, y) >= 0)
                    {
                        ear.Score = double.PositiveInfinity;
                        break;
                    }
                }

                e = e.Next!;
            }
        }
    }

    private void FloodFillConstrainedRegionsQueue(int constraintIndex, BitArray visited, IQuadEdge firstEdge)
    {
        // Using an ArrayDeque (Java) equivalent - Queue in C#
        var deque = new Queue<IQuadEdge>();
        var maxQueueSize = 0;

        deque.Enqueue(firstEdge);

        while (deque.Count > 0)
        {
            if (deque.Count > maxQueueSize) maxQueueSize = deque.Count;

            var e = deque.Peek();
            var f = e.GetForward();
            var fIndex = f.GetIndex();

            if (!f.IsConstraintRegionBorder() && !visited[fIndex])
            {
                visited[fIndex] = true;
                f.SetConstraintRegionInteriorIndex(constraintIndex);
                deque.Enqueue(f.GetDual());
                continue;
            }

            var r = e.GetReverse();
            var rIndex = r.GetIndex();

            if (!r.IsConstraintRegionBorder() && !visited[rIndex])
            {
                visited[rIndex] = true;
                r.SetConstraintRegionInteriorIndex(constraintIndex);
                deque.Enqueue(r.GetDual());
                continue;
            }

            deque.Dequeue();
        }

        Debug.WriteLine($"FloodFillConstrainedRegionsQueue: Maximum queue size was {maxQueueSize}");
    }

    /// <summary>
    ///     Creates or gets a VertexMergerGroup for coincident vertices.
    ///     This follows the Java algorithm pattern for handling multiple vertices at the same location.
    /// </summary>
    private IVertex GetOrCreateMergerGroup(IVertex constraintVertex, IVertex tinVertex)
    {
        var tol = _thresholds.GetVertexTolerance();

        // If vertices are not close enough, no merger needed
        if (!Near(constraintVertex, tinVertex, tol)) return constraintVertex;

        // If TIN vertex is already a merger group, add to it
        if (tinVertex is VertexMergerGroup existingGroup)
        {
            existingGroup.AddVertex(constraintVertex);
            return existingGroup;
        }

        // If constraint vertex is already a merger group, add TIN vertex to it
        if (constraintVertex is VertexMergerGroup constraintGroup)
        {
            constraintGroup.AddVertex(tinVertex);
            return constraintGroup;
        }

        // Neither is a merger group - create new one if they're coincident
        if (Near(constraintVertex, tinVertex, tol))
        {
            var newGroup = new VertexMergerGroup(tinVertex.AsVertex());
            newGroup.AddVertex(constraintVertex);
            return newGroup;
        }

        return constraintVertex;
    }

    private bool IsMatchingVertex(IVertex v, IVertex fromTin)
    {
        if (fromTin?.IsNullVertex() == true) return false;

        // Direct equality check (most common case)
        if (ReferenceEquals(v, fromTin)) return true;

        // Check for vertex merger group containment
        if (fromTin is VertexMergerGroup group && group.Contains(v.AsVertex())) return true;

        // Use simple equality comparison like Java
        // note we are only comparing X/Y coords, not index or Z value
        return v.Equals(fromTin);
    }

    private bool RecursiveRestoreDelaunay(QuadEdge n)
    {
        if (n.IsConstrained()) return false;
        var nf = (QuadEdge)n.GetForward();
        var a = n.GetA();
        var b = n.GetB();
        var c = nf.GetB();
        if (c.IsNullVertex()) return false;
        var d = (QuadEdge)n.GetDual();
        var df = (QuadEdge)d.GetForward();
        var t = df.GetB();
        if (t.IsNullVertex()) return false;

        var h = _geoOp.InCircle(a, b, c, t);
        if (h > 0)
        {
            var nr = (QuadEdge)n.GetReverse();
            var dr = (QuadEdge)d.GetReverse();
            n.SetVertices(t, c);
            n.SetForward(nr);
            n.SetReverse(df);
            d.SetForward(dr);
            d.SetReverse(nf);
            dr.SetForward(nf);
            nr.SetForward(df);

            RecursiveRestoreDelaunay(nf);
            RecursiveRestoreDelaunay(nr);
            RecursiveRestoreDelaunay(df);
            RecursiveRestoreDelaunay(dr);
            return true;
        }

        return false;
    }

    private void RemoveEdge(QuadEdge e)
    {
        var d = (QuadEdge)e.GetDual();
        var dr = (QuadEdge)d.GetReverse();
        var df = (QuadEdge)d.GetForward();
        var ef = (QuadEdge)e.GetForward();
        var er = (QuadEdge)e.GetReverse();

        //// Debug logging with extensive index validation
        // int eIndex = e.GetIndex();
        // int dIndex = d.GetIndex();
        // int eBaseIndex = e.GetBaseIndex();
        // var eBase = e.GetBaseReference();

        // Debug.WriteLine($"RemoveEdge: Removing edge {eIndex} (base: {eBaseIndex}) ({e.GetA().X:F2},{e.GetA().Y:F2})->({e.GetB().X:F2},{e.GetB().Y:F2})");
        // Debug.WriteLine($"RemoveEdge: Dual edge index: {dIndex}, base reference index: {eBase.GetIndex()}");

        //// Validate ALL edge indices in the topology
        // try
        // {
        // Debug.WriteLine($"RemoveEdge: Topology indices - dr:{dr?.GetIndex()}, df:{df?.GetIndex()}, ef:{ef?.GetIndex()}, er:{er?.GetIndex()}");

        // // Check for any negative indices in the topology
        // var invalidIndices = new List<string>();
        // if (dr?.GetIndex() < 0) invalidIndices.Add($"dr:{dr.GetIndex()}");
        // if (df?.GetIndex() < 0) invalidIndices.Add($"df:{df.GetIndex()}");
        // if (ef?.GetIndex() < 0) invalidIndices.Add($"ef:{ef.GetIndex()}");
        // if (er?.GetIndex() < 0) invalidIndices.Add($"er:{er.GetIndex()}");

        // if (invalidIndices.Count > 0)
        // {
        // Debug.WriteLine($"RemoveEdge: WARNING - Invalid indices in topology: {string.Join(", ", invalidIndices)}");
        // }
        // }
        // catch (Exception ex)
        // {
        // Debug.WriteLine($"RemoveEdge: Error validating topology indices: {ex.Message}");
        // }

        //// Validate edge indices before removal
        // if (eIndex < 0 || dIndex < 0)
        // {
        // Debug.WriteLine($"RemoveEdge: ERROR - Invalid edge indices detected: e={eIndex}, d={dIndex}");
        // Debug.WriteLine($"RemoveEdge: Skipping deallocation of corrupted edge");
        // return;
        // }

        //// Store topology before removal for validation
        // if (dr == null || df == null || ef == null || er == null)
        // {
        // Debug.WriteLine($"RemoveEdge: WARNING - null reference detected before removal");
        // return;
        // }

        // Perform the topology rewiring
        dr.SetForward(ef);
        df.SetReverse(er);

        Debug.WriteLine(
            $"RemoveEdge: Rewired topology - dr:{dr.GetIndex()} -> ef:{ef.GetIndex()}, df:{df.GetIndex()} <- er:{er.GetIndex()}");

        //// Validate edge index again just before deallocation
        // int finalIndex = e.GetIndex();
        // int finalBaseIndex = e.GetBaseIndex();
        // if (finalIndex != eIndex || finalBaseIndex != eBaseIndex)
        // {
        // Debug.WriteLine($"RemoveEdge: ERROR - Edge index changed during topology rewiring: {eIndex}->{finalIndex}, base: {eBaseIndex}->{finalBaseIndex}");
        // return;
        // }

        // Debug.WriteLine($"RemoveEdge: Deallocating edge with index {finalIndex}");
        _edgePool.DeallocateEdge(e);
    }

    private void SetConstrained(QuadEdge edge, IConstraint constraint, List<IQuadEdge> edgesForConstraint)
    {
        var dual = (QuadEdge)edge.GetDual();
        var idx = constraint.GetConstraintIndex();

        Debug.WriteLine($"SetConstrained: Marking edge {edge.GetIndex()} as constrained with index {idx}");

        if (constraint.DefinesConstrainedRegion())
        {
            // Store edge reference for flood fill and mark both sides
            edgesForConstraint.Add(edge);

            // For region constraints, call the interface methods which will delegate to the duals
            edge.SetConstraintBorderIndex(idx);
            Debug.WriteLine($"SetConstrained: Marked region border on edges {edge.GetIndex()} and {dual.GetIndex()}");
        }
        else
        {
            // Line feature. Call interface methods which will delegate to the duals
            // This preserves the geometric indices in the base edges
            if (edge.IsConstraintRegionMember()) edge.SetConstraintLineMemberFlag();
            else edge.SetConstraintLineIndex(idx);

            _edgePool.AddLinearConstraintToMap(edge, constraint);

            // _edgePool.AddLinearConstraintToMap(dual, constraint); java doesn't do this
            Debug.WriteLine($"SetConstrained: Marked line constraint on edges {edge.GetIndex()} and {dual.GetIndex()}");
        }
    }

    // Minimal DevillersEar to support cavity fill
    private class DevillersEar
    {
        public readonly IVertex V1;

        public QuadEdge C; // current

        public int Index;

        public QuadEdge N; // next edge

        public DevillersEar? Next;

        public QuadEdge P; // prior edge

        public DevillersEar? Prior;

        public double Score;

        public IVertex V0;

        public IVertex V2;

        public DevillersEar(int index, DevillersEar? priorEar, QuadEdge current, QuadEdge prior)
        {
            Index = index;
            Prior = priorEar;
            if (priorEar != null) priorEar.Next = this;
            C = current;
            N = (QuadEdge)C.GetForward();
            P = prior;
            V0 = C.GetA();
            V1 = C.GetB();
            V2 = N.GetB();
        }
    }
}