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
using System.Globalization;

using Avalonia.Data.Converters;

using Tinfour.Core.Interpolation;

/// <summary>
///     Converter for displaying user-friendly names for interpolation types in the UI.
/// </summary>
public class InterpolationTypeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is InterpolationType type)
            return type.ToDisplayName();
        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // This converter is only used for display, not for editing
        throw new NotImplementedException();
    }
}