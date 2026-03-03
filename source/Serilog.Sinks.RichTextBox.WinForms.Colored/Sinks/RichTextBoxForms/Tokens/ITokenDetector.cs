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

public interface ITokenDetector
{
    bool TryCreateToken(object? rawValue, out DetectedToken token);

    bool TryCreateTokenFromString(string rawValue, out DetectedToken token);

    bool TryBuildUri(DetectedToken token, out string uri);

    bool TryParseUri(string uriText, out DetectedToken token);

    IReadOnlyList<DetectedToken> Extract(LogEvent logEvent);

    string BuildUniqueKey(DetectedToken token);
}
