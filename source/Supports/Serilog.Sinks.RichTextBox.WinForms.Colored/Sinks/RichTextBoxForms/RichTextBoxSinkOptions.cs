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

using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.RichTextBoxForms.Themes;
using Serilog.Sinks.RichTextBoxForms.Tokens;
using System.Globalization;

namespace Serilog.Sinks.RichTextBoxForms;

public class RichTextBoxSinkOptions
{
    private const string DefaultOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

    public int MaxLogLines
    {
        get;
        set => field = value switch
        {
            < 1 => 1,
            > 2048 => 2048,
            _ => value
        };
    }

    public int SpacesPerIndent
    {
        get;
        private set => field = value switch
        {
            < 0 => 0,
            > 16 => 16,
            _ => value
        };
    }

    public string OutputTemplate
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? DefaultOutputTemplate : value;
    }

    public RichTextBoxSinkOptions()
    {
        Theme = ThemePresets.EnhancedDark;
        AutoScroll = true;
        MaxLogLines = 256;
        OutputTemplate = DefaultOutputTemplate;
        FormatProvider = CultureInfo.InvariantCulture;
        SpacesPerIndent = 2;
        EnableTokenLinks = true;
        MinimumLogEventLevel = LogEventLevel.Verbose;
        TokenDetector = NullTokenDetector.Instance;
    }

    private RichTextBoxSinkOptions(RichTextBoxSinkOptions source)
    {
        Theme = source.Theme;
        AutoScroll = source.AutoScroll;
        MaxLogLines = source.MaxLogLines;
        OutputTemplate = source.OutputTemplate;
        FormatProvider = source.FormatProvider ?? CultureInfo.InvariantCulture;
        PrettyPrintJson = source.PrettyPrintJson;
        SpacesPerIndent = source.SpacesPerIndent;
        MinimumLogEventLevel = source.MinimumLogEventLevel;
        LevelSwitch = source.LevelSwitch;

        var detector = source.TokenDetector;
        var hasDetector = detector is not NullTokenDetector;
        EnableTokenLinks = source is { EnableTokenLinks: true, OnTokenClicked: not null } && hasDetector;
        OnTokensDetected = hasDetector ? source.OnTokensDetected : null;
        OnTokenClicked = EnableTokenLinks ? source.OnTokenClicked : null;
        TokenDetector = detector;
    }

    public bool AutoScroll { get; set; }

    public Theme Theme { get; set; }

    public IFormatProvider? FormatProvider { get; set; }

    public LogEventLevel MinimumLogEventLevel { get; set; }

    public LoggingLevelSwitch? LevelSwitch { get; }

    public bool PrettyPrintJson { get; set; }

    public bool EnableTokenLinks { get; set; }

    public Action<DetectedTokenBatch>? OnTokensDetected { get; set; }

    public Action<DetectedToken>? OnTokenClicked { get; set; }

    public ITokenDetector TokenDetector { get; set; }

    internal RichTextBoxSinkOptions ToRuntimeOptions()
    {
        return new RichTextBoxSinkOptions(this);
    }
}