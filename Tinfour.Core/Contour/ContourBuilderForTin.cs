/*
 * Copyright 2019 Gary W. Lucas.
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
 * 07/2019  G. Lucas     Created
 * 08/2025  M.Fender     Ported to C# for Tinfour.Core
 * 11/2025  M. Fender    Added Span<T> optimizations for performance
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Contour;

using System.Collections;
using System.Diagnostics;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;

/// <summary>
///     Provides data elements and methods for constructing contours from a Delaunay
///     Triangulation. It is assumed that the data represented by the triangulation
///     can be treated as a continuous surface with no null values. Constrained
///     Delaunay Triangulations are allowed.
/// </summary>
public class ContourBuilderForTin
{
    /// <summary>
    ///     A list of "closed contours" which lie entirely in the interior of the TIN
    ///     and form closed loops.
    /// </summary>
    private readonly List<Contour> _closedContourList = new();

    private readonly double[] _envelope;

    /// <summary>
    ///     A list of "open contours" which cross the interior of the TIN and terminate
    ///     at points lying on its perimeter edges.
    /// </summary>
    private readonly List<Contour> _openContourList = new();

    /// <summary>
    ///     The list of regions that are not contained by other regions.
    /// </summary>
    private readonly List<ContourRegion> _outerRegions = new();

    /// <summary>
    ///     The perimeter edges for the TIN.
    /// </summary>
    private readonly List<IQuadEdge>? _perimeter;

    /// <summary>
    ///     A list of contours lying along the boundary. These contours consist
    ///     exclusively of points lying on the perimeter of the TIN. These contours are
    ///     predominantly open, but may be closed in the special case that there are no
    ///     interior contours that terminate at the perimeters (e.g. those in the
    ///     openContourList).
    /// </summary>
    private readonly List<Contour> _perimeterContourList = new();

    /// <summary>
    ///     A list of the perimeter links. Even though the perimeter links form a
    ///     self-closing linked list, we track them just an array list just to simplify
    ///     the debugging and diagnostics. This representation is slightly redundant,
    ///     but the added overhead is less important that creating manageable code.
    /// </summary>
    private readonly List<PerimeterLink>? _perimeterList = new();

    /// <summary>
    ///     A map relating edge index to a perimeter link
    /// </summary>
    private readonly Dictionary<int, PerimeterLink>? _perimeterMap = new();

    /// <summary>
    ///     A bitmap for tracking whether edges have been processed during contour
    ///     construction.
    /// </summary>
    private readonly BitArray? _perimeterTermination;

    /// <summary>
    ///     The list of all regions (may be empty).
    /// </summary>
    private readonly List<ContourRegion> _regionList = new();

    private readonly IIncrementalTin? _tin;

    /// <summary>
    ///     A class for assigning numeric values to contours.
    /// </summary>
    private readonly IVertexValuator? _valuator;

    /// <summary>
    ///     A bitmap for tracking whether edges have been processed during contour
    ///     construction.
    /// </summary>
    private readonly BitArray? _visited;

    /// <summary>
    ///     A safe copy of the contour value specifications.
    /// </summary>
    private readonly double[] _zContour;

    private int _nEdgeTransits;

    private int _nVertexTransits;

    private long _timeToBuildContours;

    private long _timeToBuildRegions;

    /// <summary>
    ///     Creates a set of contours at the specified vertical coordinates from the
    ///     Delaunay Triangulation. It is assumed that the data represented by the
    ///     triangulation can be treated as a continuous surface with no null values.
    ///     Constrained Delaunay Triangulations are allowed.
    /// </summary>
    /// <param name="tin">A valid TIN.</param>
    /// <param name="vertexValuator">
    ///     An optional valuator or a null reference if the
    ///     default is to be used.
    /// </param>
    /// <param name="zContour">A value array of contour values.</param>
    /// <param name="buildRegions">
    ///     Indicates whether the builder should produce region
    ///     (polygon) structures in addition to contours.
    /// </param>
    public ContourBuilderForTin(
        IIncrementalTin tin,
        IVertexValuator? vertexValuator,
        double[] zContour,
        bool buildRegions = false)
    {
        if (tin == null) throw new ArgumentNullException(nameof(tin), "Null reference for input TIN");

        if (!tin.IsBootstrapped()) throw new ArgumentException("Input TIN is not properly populated", nameof(tin));

        if (zContour == null)
            throw new ArgumentNullException(nameof(zContour), "Null reference for input contour list");

        for (var i = 1; i < zContour.Length; i++)
            if (!(zContour[i - 1] < zContour[i]))
                throw new ArgumentException(
                    $"Input contours must be unique and specified in ascending order, "
                    + $"zContours[{i}] does not meet this requirement");

        _tin = tin;
        _valuator = vertexValuator ?? new DefaultValuator();
        _zContour = new double[zContour.Length];
        Array.Copy(zContour, _zContour, zContour.Length);

        var n = tin.GetMaximumEdgeAllocationIndex();
        _visited = new BitArray(n);
        _perimeterTermination = new BitArray(n);

        // Create a closed loop of perimeter links in a counter-clockwise
        // direction. The edges in the perimeter are the interior side
        // of the TIN perimeter edges. Their duals will be the exterior sides
        // (and will connect to the ghost vertex).
        _perimeter = tin.GetPerimeter().ToList();
        PerimeterLink? prior = null;
        var k = 0;

        foreach (var p in _perimeter)
        {
            var pLink = new PerimeterLink(k, p);
            _perimeterMap![p.GetIndex()] = pLink;
            _perimeterList!.Add(pLink);

            if (prior != null)
            {
                prior.Next = pLink;
                pLink.Prior = prior;
            }

            prior = pLink;
            k++;
        }

        Debug.Assert(_perimeterList.Count > 0 && prior != null, "Missing perimeter data");
        var pFirst = _perimeterList[0];
        pFirst.Prior = prior;
        prior!.Next = pFirst;

        // Set flags for all edges that terminate on a perimeter vertex.
        // The pinwheel iterator gives edges leading away from the perimeter
        // vertex A of p. So we set the bit flag for its dual using an XOR
        foreach (var p in _perimeter)
        foreach (var w in p.GetPinwheel())
            _perimeterTermination[w.GetIndex() ^ 1] = true;

        _envelope = new double[2 * _perimeter.Count + 2];
        k = 0;
        foreach (var p in _perimeter)
        {
            var A = p.GetA();
            _envelope[k++] = A.X;
            _envelope[k++] = A.Y;
        }

        _envelope[k++] = _envelope[0];
        _envelope[k++] = _envelope[1];

        BuildAllContours();
        if (buildRegions) BuildRegions();

        // Clean up all construction elements including internal references.
        _tin = null;
        _valuator = null;
        _visited = null;
        _perimeterTermination = null;
        _perimeterMap = null;
        _perimeterList = null;
        _perimeter = null;

        foreach (var contour in _closedContourList) contour.CleanUp();

        foreach (var contour in _openContourList) contour.CleanUp();
    }

    /// <summary>
    ///     Gets a list of the contours that were constructed by this class.
    /// </summary>
    /// <returns>A valid, potentially empty list.</returns>
    public List<Contour> GetContours()
    {
        var n = _closedContourList.Count + _openContourList.Count + _perimeterContourList.Count;
        var cList = new List<Contour>(n);
        cList.AddRange(_openContourList);
        cList.AddRange(_closedContourList);
        cList.AddRange(_perimeterContourList);
        return cList;
    }

    /// <summary>
    ///     Gets the Cartesian coordinates of the convex hull of the triangulation
    ///     that was used to construct the contours for this instance.
    /// </summary>
    /// <returns>A valid array of coordinates.</returns>
    public double[] GetEnvelope()
    {
        var result = new double[_envelope.Length];
        _envelope.AsSpan().CopyTo(result);
        return result;
    }

    /// <summary>
    ///     Gets a read-only span view of the envelope coordinates.
    ///     This avoids allocation when only reading values.
    /// </summary>
    /// <returns>A read-only span of the envelope coordinate data.</returns>
    public ReadOnlySpan<double> GetEnvelopeSpan()
    {
        return _envelope.AsSpan();
    }

    /// <summary>
    ///     Gets a list of the contour regions (polygon features) that were built by
    ///     the constructor, if any.
    /// </summary>
    /// <returns>A valid, potentially empty list of regions.</returns>
    public List<ContourRegion> GetRegions()
    {
        return new List<ContourRegion>(_regionList);
    }

    /// <summary>
    ///     Provides a summary of statistics and measurements for the contour building
    ///     process and resulting data.
    /// </summary>
    /// <param name="writer">A valid TextWriter instance, such as Console.Out.</param>
    /// <param name="areaFactor">A unit-conversion factor for scaling area values</param>
    public void Summarize(TextWriter writer, double areaFactor = 1.0)
    {
        writer.WriteLine("Summary of statistics for contour building");
        writer.WriteLine($"Time to build contours {_timeToBuildContours / 1.0e+6, 7:F1} ms");
        writer.WriteLine($"Time to build regions  {_timeToBuildRegions / 1.0e+6, 7:F1} ms");
        writer.WriteLine(
            $"Open contours:      {_openContourList.Count, 8},  {CountPoints(_openContourList), 8} points");
        writer.WriteLine(
            $"Closed contours:    {_closedContourList.Count, 8},  {CountPoints(_closedContourList), 8} points");
        writer.WriteLine($"Regions:            {_regionList.Count, 8}");
        writer.WriteLine($"Outer Regions:      {_outerRegions.Count, 8}");
        writer.WriteLine($"Edge transits:      {_nEdgeTransits, 8}");
        writer.WriteLine($"Vertex transits:    {_nVertexTransits, 8}");
        writer.WriteLine();
    }

    private static int CountPoints(List<Contour> cList)
    {
        return cList.Sum((Contour c) => c.Size());
    }

    /// <summary>
    ///     Build the contours
    /// </summary>
    private void BuildAllContours()
    {
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < _zContour.Length; i++)
        {
            _visited!.SetAll(false); // Clear all bits
            BuildOpenContours(i);
            BuildClosedLoopContours(i);
        }

        stopwatch.Stop();
        _timeToBuildContours = stopwatch.ElapsedTicks * 100; // Convert to nanoseconds
    }

    /// <summary>
    ///     Build contours that lie entirely inside the TIN and do not intersect
    ///     the perimeter edges. These contours form closed loops.
    /// </summary>
    private void BuildClosedLoopContours(int iContour)
    {
        var z = _zContour[iContour];

        foreach (var p in _tin!.GetEdges())
        {
            var e = p;
            var eIndex = e.GetIndex();
            if (_visited![eIndex]) continue;

            MarkAsVisited(e);
            var A = e.GetA();
            var B = e.GetB();

            var zA = _valuator!.Value(A);
            var zB = _valuator!.Value(B);

            var test = (zA - z) * (zB - z);

            if (test < 0)
            {
                // The edge crosses the contour value.
                if (zA < zB)
                {
                    // e is an ascending edge, but the dual is a descending edge
                    e = e.GetDual();
                    (zA, zB) = (zB, zA);
                    A = e.GetA();
                    B = e.GetB();
                }

                // e is a descending edge and a valid start
                var contour = new Contour(iContour + 1, iContour, z, true);
                contour.Add(e, zA, zB);

                FollowContour(contour, z, e, Vertex.Null, 0, e, Vertex.Null);
            }
            else if (test == 0)
            {
                // At least one of the vertices is level with the contour value.
                if (zA == z && zB == z)
                {
                    // Both vertices are on the contour level - this edge lies on the contour
                    var f = e.GetForward();
                    var g = e.GetDual();
                    var h = g.GetForward();
                    MarkAsVisited(f);
                    MarkAsVisited(g);
                    MarkAsVisited(h);

                    var C = f.GetB();
                    var D = h.GetB();

                    if (!C.IsNullVertex() && !D.IsNullVertex())
                    {
                        var zC = _valuator.Value(C);
                        var zD = _valuator.Value(D);

                        if (zC >= z && z > zD)
                        {
                            var contour = new Contour(iContour + 1, iContour, z, true);
                            contour.Add(A);
                            contour.Add(B);

                            FollowContour(contour, z, e, A, 0, f, B);
                        }
                        else if (zD >= z && z > zC)
                        {
                            var contour = new Contour(iContour + 1, iContour, z, true);
                            contour.Add(B);
                            contour.Add(A);

                            FollowContour(contour, z, g, B, 0, h, A);
                        }
                    }
                }
            }
        }
    }

    private void BuildOpenContours(int iContour)
    {
        var z = _zContour[iContour];

        // Build contours that start from perimeter edges and go into the interior
        foreach (var edge in _perimeter!)
        {
            MarkAsVisited(edge);

            var e = edge;
            var f = e.GetForward();
            var r = e.GetReverse();
            var A = e.GetA();
            var B = f.GetA();
            var C = r.GetA();

            var zA = _valuator!.Value(A);
            var zB = _valuator.Value(B);
            var zC = _valuator.Value(C);

            if (zA > z && z > zB)
            {
                // e is an ascending edge and a valid start
                var contour = new Contour(iContour + 1, iContour, z, false);
                contour.Add(e, zA, zB);
                FollowContour(contour, z, e, Vertex.Null, 0, e, Vertex.Null);
            }
            else if (zA == z)
            {
                var startSweepIndex = 0;
                var g = r.GetDual();
                var h = g.GetForward();
                var G = h.GetB();
                MarkAsVisited(h);
                while (true)
                {
                    startSweepIndex++;
                    if (zB < z && z < zC)
                    {
                        // exit through an ascending edge
                        MarkAsVisited(f);
                        var contour = new Contour(iContour + 1, iContour, z, false);
                        contour.Add(A);
                        contour.Add(f.GetDual(), zC, zB);
                        FollowContour(contour, z, e, A, startSweepIndex, f.GetDual(), Vertex.Null);
                    }

                    if (G.IsNullVertex()) break;
                    var zG = _valuator.Value(G);
                    if (zB < z && zC == z && zG >= z)
                    {
                        // transfer through vertex C with supporting edge h
                        var contour = new Contour(iContour + 1, iContour, z, false);
                        contour.Add(A);
                        contour.Add(C);
                        MarkAsVisited(g);
                        MarkAsVisited(h);
                        var dualIndex = h.GetIndex() ^ 1;
                        if (_perimeterTermination!.Get(dualIndex))

                            // This is a short traversal. The contour terminates
                            // after a single segment.
                            FinishContour(contour, e, startSweepIndex, h, C);
                        else FollowContour(contour, z, e, A, startSweepIndex, h, C);
                    }

                    B = C;
                    C = G;
                    zB = zC;
                    zC = zG;
                    f = h;
                    r = h.GetForward();
                    g = r.GetDual();
                    h = g.GetForward();
                    G = h.GetB();
                    MarkAsVisited(h);
                }
            }
        }
    }

    private void BuildRegions()
    {
        var stopwatch = Stopwatch.StartNew();

        // Build regions using perimeter information first
        BuildRegionsUsingPerimeter();

        // Then handle interior regions from closed contours
        foreach (var contour in _closedContourList)
            if (contour.IsClosed() && contour.Size() >= 3)
                try
                {
                    var region = new ContourRegion(contour);
                    _regionList.Add(region);
                    _outerRegions.Add(region);
                }
                catch (Exception)
                {
                    // Skip invalid contours
                }

        // Organize nested regions if there are multiple regions
        if (_regionList.Count > 1) OrganizeNestedRegions();

        stopwatch.Stop();
        _timeToBuildRegions = stopwatch.ElapsedTicks * 100; // Convert to nanoseconds
    }

    /// <summary>
    ///     Build regions using contours that intersect the perimeter.
    /// </summary>
    private void BuildRegionsUsingPerimeter()
    {
        // Special case: If no open contours intersect the perimeter,
        // create a single region from the perimeter itself
        if (_openContourList.Count == 0)
        {
            BuildSimplePerimeterRegion();
            return;
        }

        // Prepare the perimeter tips
        foreach (var pLink in _perimeterList!) pLink.PrependThroughVertexTips();

        // The "stitching operation" - connect contours into regions
        foreach (var pLink in _perimeterList!)
        {
            if (pLink.Tip0 == null) continue; // No contours start/end on this edge

            var tip = pLink.Tip0;
            while (tip != null)
            {
                if (tip.Start)
                {
                    // Start of contour
                    if (!tip.Contour.TraversedForward)
                    {
                        var leftIndex = tip.Contour.GetLeftIndex();
                        var z = tip.Contour.GetZ();
                        var members = TraverseFromTipLink(tip, leftIndex, z, true);
                        if (members.Count > 0)
                        {
                            var region = new ContourRegion(members, leftIndex);
                            _regionList.Add(region);
                        }
                    }
                }
                else
                {
                    // End of contour (termination)
                    if (!tip.Contour.TraversedBackward)
                    {
                        var rightIndex = tip.Contour.GetRightIndex();
                        var z = tip.Contour.GetZ();
                        var members = TraverseFromTipLink(tip, rightIndex, z, false);
                        if (members.Count > 0)
                        {
                            var region = new ContourRegion(members, rightIndex);
                            _regionList.Add(region);
                        }
                    }
                }

                tip = tip.Next;
            }
        }
    }

    /// <summary>
    ///     Builds a simple perimeter region when no open contours exist.
    /// </summary>
    private void BuildSimplePerimeterRegion()
    {
        // Get the z value of a vertex on the perimeter to determine the region index
        var perimeterVertex = _perimeter![0].GetA();
        var z = _valuator!.Value(perimeterVertex);

        // Determine the appropriate left index
        var leftIndex = _zContour.Length;
        for (var i = 0; i < _zContour.Length; i++)
            if (_zContour[i] > z)
            {
                leftIndex = i;
                break;
            }

        // Create a contour that follows the perimeter
        var contour = new Contour(leftIndex, -1, z, true);

        // Add all perimeter vertices
        foreach (var p in _perimeter)
        {
            var A = p.GetA();
            contour.Add(A.X, A.Y);
        }

        // Add a closure point
        var firstVertex = _perimeter[0].GetA();
        contour.Add(firstVertex.X, firstVertex.Y);

        contour.Complete();
        _perimeterContourList.Add(contour);

        // Create a region from this contour
        var region = new ContourRegion(contour);
        _regionList.Add(region);
        _outerRegions.Add(region);
    }

    /// <summary>
    ///     Finishes the construction of an individual contour by setting up contour tips
    ///     for region building and adding it to the appropriate containers.
    /// </summary>
    /// <param name="contour">The contour to finish</param>
    /// <param name="startEdge">The edge that was used to specify the start of the contour</param>
    /// <param name="startSweepIndex">The sweep index for the start of the contour</param>
    /// <param name="terminalEdge">The edge that was used to create the end of the contour</param>
    /// <param name="terminalVertex">If the contour terminates on a vertex, a valid instance; otherwise null</param>
    private void FinishContour(
        Contour contour,
        IQuadEdge startEdge,
        int startSweepIndex,
        IQuadEdge terminalEdge,
        IVertex terminalVertex)
    {
        contour.Complete();

        if (contour.IsClosed())
        {
            _closedContourList.Add(contour);
            return;
        }

        _openContourList.Add(contour);

        // For open contours, set up perimeter links for region building
        var startIndex = startEdge.GetIndex();
        if (_perimeterMap!.TryGetValue(startIndex, out var pStart))
            pStart.AddContourTip(contour, true, startSweepIndex);

        if (terminalVertex.IsNullVertex())
        {
            var termIndex = terminalEdge.GetIndex() ^ 1;
            if (_perimeterMap.TryGetValue(termIndex, out var pTerm)) pTerm.AddContourTip(contour, false, 0);
        }
        else
        {
            // Handle terminal vertex case (more complex)
            // Find the proper perimeter edge by sweeping clockwise
            var terminalSweepIndex = 0;
            var s = terminalEdge;

            while (true)
            {
                terminalSweepIndex++;
                var n = s.GetForwardFromDual();
                var B = n.GetB();
                if (B.IsNullVertex()) break;
                s = n;
            }

            var termIndex = s.GetIndex();
            if (_perimeterMap.TryGetValue(termIndex, out var pTerm))
                pTerm.AddContourTip(contour, false, terminalSweepIndex);
        }
    }

    /**
   * Follow a contour to its completion. It is expected that when this
   * method is called, the startEdge will be either a descending edge
   * (in the through-edge case) or a support edge for a through-vertex case.
   * If the start passes through a vertex, the startVertex will be non-null.
   * The start sweep index will be zero in the through-edge case or
   * a value greater than zero in the through-vertex case.
   * The terminal edge and vertex follow the same rules as the start.
   * In fact, in some cases, the terminal edge may actually be the starting
   *
   * @param contour a valid instance
   * @param z the z value for the contour
   * @param startEdge the starting edge
   * @param startVertex the starting vertex, potentially null.
   * @param startSweepIndex a value zero (through-edge case) or larger
   * (through-vertex case).
   * @param terminalEdge the last edge added to the contour, so far
   * @param terminalVertex the last vertex added to the contour, so far,
   * potentially null.
   * @return indicates a successful completion; at this time, always true.
   */
    private bool FollowContour(
        Contour contour,
        double z,
        IQuadEdge startEdge,
        IVertex startVertex,
        int startSweepIndex,
        IQuadEdge terminalEdge,
        IVertex terminalVertex)
    {
        var V = terminalVertex;
        var e = terminalEdge;
        MarkAsVisited(e);

        while (true)
        {
            var f = e.GetForward();
            var r = e.GetReverse();
            MarkAsVisited(e);
            var A = e.GetA();
            var B = f.GetA();
            var C = r.GetA();
            var zA = _valuator!.Value(A);
            var zB = _valuator.Value(B);
            double zC;
            if (C.IsNullVertex()) zC = double.NaN;
            else zC = _valuator.Value(C);

            if (V.IsNullVertex())
            {
                // transition through edge
                // e should be a descending edge with z values
                // bracketing the contour z.
                _nEdgeTransits++;
                Debug.Assert(zA > z && z > zB, "Entry not on a bracketed descending edge");
                if (zC < z)
                {
                    // exit via edge C-to-A
                    e = r.GetDual();
                    contour.Add(e, zA, zC);
                    MarkAsVisited(e);
                }
                else if (zC > z)
                {
                    // exit through edge B-to-C
                    e = f.GetDual();
                    contour.Add(e, zC, zB);
                    MarkAsVisited(e);
                }
                else if (zC == z)
                {
                    // transition-vertex side
                    e = r;
                    V = C;
                    contour.Add(C);
                }
                else
                {
                    // this could happen if zC is a null
                    // meaning we have a broken contour
                    // but because we tested on N==null above, so we should never reach
                    // here unless there's an incorrect implementation.
                    return false;
                }
            }
            else
            {
                // transition through vertex
                // sweep search clockwise starting from support edge
                _nVertexTransits++;

                // since we couldn't find a transition within the
                // current triangle, we need to search in a clockwise
                // direction for the transition.
                var e0 = e;
                var g = e;
                while (true)
                {
                    g = g.GetForwardFromDual();
                    var h = g.GetForward();
                    var k = h.GetForward();
                    var K = h.GetA();
                    var G = h.GetB();
                    var zK = _valuator.Value(K);
                    var zG = _valuator.Value(G);
                    MarkAsVisited(g);
                    MarkAsVisited(h);
                    MarkAsVisited(k);
                    if (zG > z && z > zK)
                    {
                        e = h.GetDual();
                        V = null;
                        contour.Add(e, zG, zK);
                        break;
                    }

                    if (zG == z && z > zK)
                    {
                        contour.Add(G);
                        e = f;
                        V = G;
                        break;
                    }

                    f = h;
                }

                Debug.Assert(!e0.Equals(e), "trans-vertex search loop failed");
            }

            // check for termination conditions
            if (V?.IsNullVertex() == true)
            {
                if (contour.IsClosed())
                {
                    if (startEdge.Equals(e) && startVertex.IsNullVertex())
                    {
                        // closed loop
                        FinishContour(contour, startEdge, startSweepIndex, e, V);
                        return true;
                    }
                }
                else
                {
                    C = e.GetForward().GetB();
                    if (C.IsNullVertex())
                    {
                        FinishContour(contour, startEdge, startSweepIndex, e, V);
                        return true;
                    }
                }
            }
            else
            {
                if (contour.IsClosed())
                    if (V.Equals(startVertex))
                    {
                        FinishContour(contour, startEdge, 0, e, V);
                        return true;
                    }

                var dualIndex = e.GetIndex() ^ 1;
                if (_perimeterTermination!.Get(dualIndex))
                {
                    FinishContour(contour, startEdge, startSweepIndex, e, V);
                    return true;
                }
            }
        }
    }

    /// <summary>
    ///     Sets the visited flag for an edge and its dual.
    /// </summary>
    /// <param name="e">A valid edge</param>
    private void MarkAsVisited(IQuadEdge e)
    {
        var index = e.GetIndex();
        _visited![index] = true;
        _visited[index ^ 1] = true;
    }

    /// <summary>
    ///     Organizes regions into a hierarchy based on containment.
    /// </summary>
    private void OrganizeNestedRegions()
    {
        var nRegion = _regionList.Count;
        if (nRegion < 2) return;

        // Sort regions by area (largest to smallest)
        _regionList.Sort((ContourRegion r1, ContourRegion r2) => r2.GetAbsoluteArea().CompareTo(r1.GetAbsoluteArea()));

        // Find parent-child relationships
        for (var i = 0; i < nRegion - 1; i++)
        {
            var outerRegion = _regionList[i];
            var outerXY = outerRegion.GetXY();

            for (var j = i + 1; j < nRegion; j++)
            {
                var innerRegion = _regionList[j];

                // Perimeter regions are never enclosed by other regions
                if (innerRegion.GetRegionType() == ContourRegion.ContourRegionType.Perimeter) continue;

                var testPoint = innerRegion.GetTestPoint();
                if (ContourRegion.IsPointInsideRegion(outerXY, testPoint.X, testPoint.Y))
                {
                    innerRegion.SetParent(outerRegion);
                    outerRegion.AddChild(innerRegion);
                }
            }
        }

        // Clear and rebuild outer regions list
        _outerRegions.Clear();
        foreach (var region in _regionList)
            if (region.GetParent() == null)
                _outerRegions.Add(region);
    }

    /// <summary>
    ///     Traverses from a tip link to create a set of contour region members.
    /// </summary>
    private List<ContourRegionMember> TraverseFromTipLink(TipLink tipLink0, int leftIndex, double z, bool forward0)
    {
        var mList = new List<ContourRegionMember>();
        var node = tipLink0;
        var forward = forward0;

        do
        {
            var contour = node.Contour;
            var member = new ContourRegionMember(contour, forward);
            mList.Add(member);

            double x, y;
            var boundaryContour = new Contour(leftIndex, -1, z, false);
            _perimeterContourList.Add(boundaryContour);

            member = new ContourRegionMember(boundaryContour, true);
            mList.Add(member);

            if (forward)
            {
                contour.TraversedForward = true;
                node = contour.TerminalTip!;
                x = contour.GetXY()[contour.Size() * 2 - 2];
                y = contour.GetXY()[contour.Size() * 2 - 1];
            }
            else
            {
                contour.TraversedBackward = true;
                node = contour.StartTip!;
                x = contour.GetXY()[0];
                y = contour.GetXY()[1];
            }

            boundaryContour.Add(x, y);

            if (node.Next != null)
            {
                node = node.Next;
            }
            else
            {
                var pLink = node.PLink.Next!;
                var pEdge = pLink.Edge;
                var A = pEdge.GetA();
                boundaryContour.Add(A.X, A.Y);

                while (pLink.Tip0 == null)
                {
                    pLink = pLink.Next!;
                    pEdge = pLink.Edge;
                    A = pEdge.GetA();
                    boundaryContour.Add(A.X, A.Y);
                }

                node = pLink.Tip0!;
            }

            contour = node.Contour;
            if (node.Start)
            {
                // Start of contour
                forward = true; // for next contour traversal
                x = contour.GetXY()[0];
                y = contour.GetXY()[1];
            }
            else
            {
                // End of contour
                forward = false; // for next contour traversal
                x = contour.GetXY()[contour.Size() * 2 - 2];
                y = contour.GetXY()[contour.Size() * 2 - 1];
            }

            boundaryContour.Add(x, y);
            boundaryContour.Complete(); // we're done building the boundary contour
        }
        while (node != tipLink0);

        return mList;
    }

    private class DefaultValuator : IVertexValuator
    {
        public double Value(IVertex v)
        {
            // Skip null/ghost vertices
            if (v.IsNullVertex()) return double.NaN; // Return NaN for ghost vertices - they should be filtered out

            var z = v.GetZ();
            if (double.IsNaN(z))
                throw new ArgumentException(
                    $"Input includes vertex with NaN z value (index: {v.GetIndex()}, coords: {v.X:F1},{v.Y:F1})");

            return z;
        }
    }
}