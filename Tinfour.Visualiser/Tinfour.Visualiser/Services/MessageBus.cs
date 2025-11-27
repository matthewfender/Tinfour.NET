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

namespace Tinfour.Visualiser.Services;

using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Media.Imaging;

using Tinfour.Core.Contour;

/// <summary>
///     A simple message bus to enable communication between view models and views/controls
///     without direct references. This is particularly useful for browser WASM environments.
/// </summary>
public static class MessageBus
{
    // Reset view event
    public static event EventHandler<EventArgs>? ResetViewRequested;

    /// <summary>
    ///     Request a reset of the triangulation view
    /// </summary>
    public static void RequestResetView()
    {
        ResetViewRequested?.Invoke(null, EventArgs.Empty);
    }
}