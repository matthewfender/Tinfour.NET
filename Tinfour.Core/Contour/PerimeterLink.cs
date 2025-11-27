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
 * 08/2019  G. Lucas     Created
 * 08/2025 M.Fender     Ported to C# for Tinfour.Core
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Contour;

using Tinfour.Core.Common;

/// <summary>
///     Provides elements for tracking when contours intersect perimeter edges
/// </summary>
internal class PerimeterLink
{
    /// <summary>
    ///     The interior edge of the perimeter;
    /// </summary>
    public readonly IQuadEdge Edge;

    /// <summary>
    ///     A diagnostic value used to record the position of the link within the
    ///     perimeter chain.
    /// </summary>
    public readonly int Index;

    private readonly List<TipLink> _tempList = new();

    public PerimeterLink(int index, IQuadEdge edge)
    {
        Index = index;
        Edge = edge;
    }

    /// <summary>
    ///     A reference to the next perimeter edge in a counter-clockwise (left)
    ///     direction.
    /// </summary>
    public PerimeterLink? Next { get; set; }

    /// <summary>
    ///     A reference to the prior perimeter edge in a counter-clockwise (left)
    ///     direction.
    /// </summary>
    public PerimeterLink? Prior { get; set; }

    /// <summary>
    ///     The tip closest to the start of the edge
    /// </summary>
    public TipLink? Tip0 { get; private set; }

    /// <summary>
    ///     The tip closest to the termination of the edge
    /// </summary>
    public TipLink? Tip1 { get; private set; }

    public void AddContourTip(Contour contour, bool contourStart, int sweepIndex)
    {
        var tip = new TipLink(this, contour, contourStart, sweepIndex);

        if (contourStart) contour.StartTip = tip;
        else contour.TerminalTip = tip;

        if (sweepIndex != 0)
        {
            _tempList.Add(tip);
            return;
        }

        if (Tip0 == null)
        {
            Tip0 = tip;
            Tip1 = tip;
        }
        else
        {
            if (contourStart)
            {
                // The tip is the start of a contour,
                // it should be on a descending edge (A.z > B.Z)
                // prepend the new tip to the linked-list of tips
                tip.Next = Tip0;
                Tip0.Prior = tip;
                Tip0 = tip;
            }
            else
            {
                // The tip is the termination of a contour
                // it should be on an ascending edge (A.Z < B.Z)
                // append the new tip to the linked-list of tips
                tip.Prior = Tip1;
                Tip1!.Next = tip;
                Tip1 = tip;
            }
        }
    }

    public void PrependThroughVertexTips()
    {
        if (_tempList.Count == 0) return;

        _tempList.Sort((TipLink o1, TipLink o2) => o1.SweepIndex.CompareTo(o2.SweepIndex));

        foreach (var tip in _tempList)
            if (Tip0 == null)
            {
                Tip0 = tip;
                Tip1 = tip;
            }
            else
            {
                tip.Next = Tip0;
                Tip0.Prior = tip;
                Tip0 = tip;
            }

        _tempList.Clear();
    }

    public override string ToString()
    {
        if (Prior == null || Next == null)
            return $"Perimeter link {Index}: {Edge.GetIndex()} (no links)";
        return
            $"Perimeter link {Index}: {Prior.Edge.GetIndex()} <- {Edge.GetIndex()} -> {Next.Edge.GetIndex()}";
    }
}