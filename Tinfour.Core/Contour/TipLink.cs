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

/// <summary>
///     Provides node definitions for a linked list of the tips for a single
///     perimeter edge.
/// </summary>
internal class TipLink
{
    /// <summary>
    ///     The contour
    /// </summary>
    public readonly Contour Contour;

    public readonly PerimeterLink PLink;

    /// <summary>
    ///     True if the contour starts on the tip; otherwise false.
    /// </summary>
    public readonly bool Start;

    public readonly int SweepIndex;

    /// <summary>
    ///     True if the contour terminates on the tip; otherwise, false.
    /// </summary>
    public readonly bool Termination;

    public TipLink(PerimeterLink pLink, Contour contour, bool start, int sweepIndex)
    {
        Contour = contour;
        PLink = pLink;
        Start = start;
        Termination = !start;
        SweepIndex = sweepIndex;
    }

    public TipLink? Next { get; set; }

    public TipLink? Prior { get; set; }
}