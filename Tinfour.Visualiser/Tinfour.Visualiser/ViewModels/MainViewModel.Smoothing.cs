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

namespace Tinfour.Visualiser.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

using Tinfour.Core.Utils;

/// <summary>
///     Partial class containing smoothing-related properties for MainViewModel.
/// </summary>
public partial class MainViewModel
{
    /// <summary>
    ///     Whether to apply smoothing filter to contours.
    /// </summary>
    [ObservableProperty]
    private bool _applySmoothingToContours;

    /// <summary>
    ///     Number of smoothing passes (5-40 range recommended).
    /// </summary>
    [ObservableProperty]
    private int _smoothingPasses = SmoothingFilter.DefaultPasses;
}
