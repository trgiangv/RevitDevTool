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

using Serilog.Events;

namespace Serilog.Sinks.RichTextBoxForms.Tokens;

public sealed class DetectedToken
{
    public DetectedToken(
        RevitTokenKind kind,
        string rawValue,
        string normalizedValue,
        string displayText,
        string? propertyPath = null,
        DateTimeOffset? timestamp = null,
        LogEventLevel? level = null)
    {
        Kind = kind;
        RawValue = rawValue;
        NormalizedValue = normalizedValue;
        DisplayText = displayText;
        PropertyPath = propertyPath;
        Timestamp = timestamp;
        Level = level;
    }

    public RevitTokenKind Kind { get; }
    public string RawValue { get; }
    public string NormalizedValue { get; }
    public string DisplayText { get; }
    public string? PropertyPath { get; }
    public DateTimeOffset? Timestamp { get; }
    public LogEventLevel? Level { get; }
}