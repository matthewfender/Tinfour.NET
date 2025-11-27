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

using System;
using System.Threading.Tasks;

using Avalonia;

using CommunityToolkit.Mvvm.Input;

using Tinfour.Core.Interpolation;
using Tinfour.Visualiser.Services;

/// <summary>
///     Partial class containing interpolation-related methods for MainViewModel.
/// </summary>
public partial class MainViewModel
{
[RelayCommand]
    private async Task GenerateInterpolationAsync()
    {
        if (this.IsGenerating) return;

        if (this.Triangulation == null || !this.Triangulation.IsBootstrapped())
        {
            this.StatusText = "? Error: You must first create or load a triangulation before generating interpolation.";
            return;
        }

        try
        {
            this.IsGenerating = true;
            this.StatusText =
                $"Generating interpolation raster using {this.SelectedInterpolationType.ToDisplayName()}...";

            var result = await Task.Run(() =>
                {
                    // Get TIN bounds
                    var bounds = this.Triangulation.GetBounds();
                    if (!bounds.HasValue) throw new InvalidOperationException("Unable to determine TIN bounds");

                    var tinBounds = new Rect(
                        bounds.Value.Left,
                        bounds.Value.Top,
                        bounds.Value.Width,
                        bounds.Value.Height);

                    // Determine optimal raster size for good quality without being too slow
                    var displaySize = new Size(800, 600); // Reasonable default
                    var (width, height) =
                        InterpolationRasterService.GetOptimalRasterSize(tinBounds, displaySize, 25_000_000);

                    // Generate the raster
                    return InterpolationRasterService.CreateInterpolatedRaster(
                        this.Triangulation,
                        tinBounds,
                        width,
                        height,
                        this.SelectedInterpolationType,
                        this.ConstrainedInterpolationOnly);
                });

            this.InterpolationResult = result;

            this.UpdateStatistics();
            this.StatusText = $"? Interpolation Generated\n{result}";
        }
        catch (Exception ex)
        {
            this.StatusText = $"? Error generating interpolation: {ex.Message}";
        }
        finally
        {
            this.IsGenerating = false;
        }
    }
}