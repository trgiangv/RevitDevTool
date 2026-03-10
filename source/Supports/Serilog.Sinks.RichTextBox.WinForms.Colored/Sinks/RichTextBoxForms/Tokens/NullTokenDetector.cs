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

public sealed class NullTokenDetector : ITokenDetector
{
    public static NullTokenDetector Instance { get; } = new();

    private NullTokenDetector()
    {
    }

    public bool TryCreateToken(object? rawValue, out DetectedToken token)
    {
        token = null!;
        return false;
    }

    public bool TryCreateTokenFromString(string rawValue, out DetectedToken token)
    {
        token = null!;
        return false;
    }

    public bool TryBuildUri(DetectedToken token, out string uri)
    {
        uri = string.Empty;
        return false;
    }

    public bool TryParseUri(string uriText, out DetectedToken token)
    {
        token = null!;
        return false;
    }

    public IReadOnlyList<DetectedToken> Extract(LogEvent logEvent)
    {
        return [];
    }

    public string BuildUniqueKey(DetectedToken token)
    {
        return token.NormalizedValue;
    }
}
