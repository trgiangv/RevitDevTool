#region Copyright 2025 Simon Vonhoff & Contributors

//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

#endregion

using Serilog.Configuration;
using Serilog.Sinks.RichTextBoxForms;
using Serilog.Sinks.RichTextBoxForms.Rendering;
using Serilog.Sinks.RichTextBoxForms.Themes;
// ReSharper disable ConvertToExtensionBlock

namespace Serilog;

public static class RichTextBoxSinkLoggerConfigurationExtensions
{
    /// <summary>
    /// Adds a sink that writes log events to a Windows Forms <see cref="System.Windows.Forms.RichTextBox"/> with color-coded formatting.
    /// </summary>
    /// <param name="sinkConfiguration">The logger sink configuration.</param>
    /// <param name="richTextBoxControl">The <see cref="System.Windows.Forms.RichTextBox"/> to display log output.</param>
    /// <param name="richTextBoxSink">The created <see cref="RichTextBoxSink"/> instance.</param>
    /// <param name="options">Options controlling rendering, token detection and level filtering behavior.</param>
    /// <param name="onThemeChanged">
    /// Optional subscription hook for automatic theme switching. Receives the sink's
    /// <see cref="RichTextBoxSink.SetTheme"/> callback; wire it to your theme-change signal
    /// and return an unsubscribe <see cref="Action"/> that the sink calls on dispose.
    /// </param>
    /// <returns>The logger configuration, for chaining.</returns>
    public static LoggerConfiguration RichTextBox(
        this LoggerSinkConfiguration sinkConfiguration,
        RichTextBox richTextBoxControl,
        out RichTextBoxSink richTextBoxSink,
        RichTextBoxSinkOptions? options = null,
        Func<Action<Theme>, Action>? onThemeChanged = null)
    {
        var appliedOptions = (options ?? new RichTextBoxSinkOptions()).ToRuntimeOptions();
        var renderer = new TemplateRenderer(appliedOptions);
        richTextBoxSink = new RichTextBoxSink(richTextBoxControl, appliedOptions, renderer, onThemeChanged);
        return sinkConfiguration.Sink(richTextBoxSink, appliedOptions.MinimumLogEventLevel, appliedOptions.LevelSwitch);
    }

    /// <summary>
    /// Adds a sink that writes log events to a Windows Forms <see cref="System.Windows.Forms.RichTextBox"/> with color-coded formatting.
    /// </summary>
    /// <param name="sinkConfiguration">The logger sink configuration.</param>
    /// <param name="richTextBoxControl">The <see cref="System.Windows.Forms.RichTextBox"/> to display log output.</param>
    /// <param name="options">Options controlling rendering, token detection and level filtering behavior.</param>
    /// <param name="onThemeChanged">
    /// Optional subscription hook for automatic theme switching. Receives the sink's
    /// <see cref="RichTextBoxSink.SetTheme"/> callback; wire it to your theme-change signal
    /// and return an unsubscribe <see cref="Action"/> that the sink calls on dispose.
    /// </param>
    /// <returns>The logger configuration, for chaining.</returns>
    public static LoggerConfiguration RichTextBox(
        this LoggerSinkConfiguration sinkConfiguration,
        RichTextBox richTextBoxControl,
        RichTextBoxSinkOptions? options = null,
        Func<Action<Theme>, Action>? onThemeChanged = null)
    {
        return sinkConfiguration.RichTextBox(richTextBoxControl,
            out _,
            options,
            onThemeChanged);
    }
}