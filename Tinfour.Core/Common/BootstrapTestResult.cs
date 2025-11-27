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

namespace Tinfour.Core.Common;

/// <summary>
///     Indicates the results of the evaluation for a set of input points.
/// </summary>
public enum BootstrapTestResult
{
    /// <summary>
    ///     The point set is insufficiently large for analysis.
    /// </summary>
    InsufficientPointSet,

    /// <summary>
    ///     All input vertices are coincident or nearly coincident.
    /// </summary>
    TrivialPointSet,

    /// <summary>
    ///     All input vertices are collinear or nearly collinear.
    /// </summary>
    CollinearPointSet,

    /// <summary>
    ///     A valid bootstrap triangle was found.
    /// </summary>
    Valid,

    /// <summary>
    ///     Unable to find a valid bootstrap triangle through limited search.
    /// </summary>
    Unknown
}
