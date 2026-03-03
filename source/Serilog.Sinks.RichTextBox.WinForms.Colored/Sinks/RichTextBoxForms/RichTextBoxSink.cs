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
using Serilog.Sinks.RichTextBoxForms.Collections;
using Serilog.Sinks.RichTextBoxForms.Extensions;
using Serilog.Sinks.RichTextBoxForms.Rendering;
using Serilog.Sinks.RichTextBoxForms.Rtf;
using Serilog.Sinks.RichTextBoxForms.Themes;
using Serilog.Sinks.RichTextBoxForms.Tokens;

namespace Serilog.Sinks.RichTextBoxForms;

public class RichTextBoxSink : ILogEventSink, IDisposable
{
    private const double FlushIntervalMs = 1000.0 / 16.0;
    private readonly ConcurrentCircularBuffer<LogEvent> _buffer;
    private readonly AutoResetEvent _signal;
    private readonly RichTextBoxSinkOptions _options;
    private readonly RichTextBox _richTextBox;
    private readonly CancellationTokenSource _tokenSource;
    private readonly Task _processingTask;
    private readonly Action? _unsubscribeThemeChanged;
    private ITokenRenderer _renderer;
    private volatile int _themeVersion;
    private bool _disposed;

    /// <param name="richTextBox">Target control for log output.</param>
    /// <param name="options">Sink configuration including theme, auto-scroll, and formatting.</param>
    /// <param name="renderer">Custom renderer, or <c>null</c> to use the default <see cref="TemplateRenderer"/>.</param>
    /// <param name="onThemeChanged">
    /// Optional subscription hook for automatic theme switching. The sink passes its own
    /// <see cref="SetTheme"/> callback; the consumer wires it to an external theme-change
    /// signal and returns an unsubscribe <see cref="Action"/> that the sink calls on dispose.
    /// </param>
    public RichTextBoxSink(
        RichTextBox richTextBox,
        RichTextBoxSinkOptions options,
        ITokenRenderer? renderer = null,
        Func<Action<Theme>, Action>? onThemeChanged = null)
    {
        _options = options;
        _richTextBox = richTextBox;
        _renderer = renderer ?? new TemplateRenderer(options);
        _tokenSource = new CancellationTokenSource();

        _buffer = new ConcurrentCircularBuffer<LogEvent>(options.MaxLogLines);
        _signal = new AutoResetEvent(false);

        richTextBox.Clear();
        richTextBox.ReadOnly = true;
        richTextBox.DetectUrls = options.EnableTokenLinks;
        richTextBox.ForeColor = options.Theme.DefaultStyle.Foreground;
        richTextBox.BackColor = options.Theme.DefaultStyle.Background;
        ApplyNativeThemeToControl();
        if (options is { EnableTokenLinks: true, OnTokenClicked: not null })
        {
            richTextBox.LinkClicked += OnLinkClicked;
        }

        _unsubscribeThemeChanged = onThemeChanged?.Invoke(SetTheme);

        _processingTask = Task.Run(() => ProcessMessages(_tokenSource.Token));
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        if (_options is { EnableTokenLinks: true, OnTokenClicked: not null })
        {
            _richTextBox.LinkClicked -= OnLinkClicked;
        }
        _unsubscribeThemeChanged?.Invoke();
        _tokenSource.Cancel();
        _signal.Set();
        _processingTask.GetAwaiter().GetResult();
        _signal.Dispose();
        _tokenSource.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Emit(LogEvent logEvent)
    {
        _buffer.Add(logEvent);
        _signal.Set();
    }

    public void Clear()
    {
        if (_disposed)
        {
            return;
        }

        _buffer.Clear();
        _signal.Set();
    }

    public void Restore()
    {
        if (_disposed)
        {
            return;
        }

        _buffer.Restore();
        _signal.Set();
    }

    /// <summary>
    /// Switches the theme at runtime. Re-renders all buffered log events with the new
    /// color scheme and applies native dark/light mode to the control (scrollbar, border).
    /// </summary>
    public void SetTheme(Theme newTheme)
    {
        if (_disposed) return;

        _options.Theme = newTheme;
        _renderer = new TemplateRenderer(_options);
        Interlocked.Increment(ref _themeVersion);
        _signal.Set();

        if (_richTextBox.InvokeRequired)
            _richTextBox.BeginInvoke(UpdateControlTheme);
        else
            UpdateControlTheme();
    }

    private void UpdateControlTheme()
    {
        if (_richTextBox.IsDisposed || _richTextBox.Disposing) return;
        _richTextBox.ForeColor = _options.Theme.DefaultStyle.Foreground;
        _richTextBox.BackColor = _options.Theme.DefaultStyle.Background;
        _richTextBox.SetTheme(_options.Theme.IsDarkTheme);
    }

    private void ApplyNativeThemeToControl()
    {
        if (_richTextBox.IsHandleCreated)
            _richTextBox.SetTheme(_options.Theme.IsDarkTheme);
        else
            _richTextBox.HandleCreated += OnHandleCreatedApplyTheme;
    }

    private void OnHandleCreatedApplyTheme(object? sender, EventArgs e)
    {
        _richTextBox.HandleCreated -= OnHandleCreatedApplyTheme;
        _richTextBox.SetTheme(_options.Theme.IsDarkTheme);
    }

    private void ProcessMessages(CancellationToken token)
    {
        var builder = new RtfBuilder(_options.Theme);
        var snapshot = new List<LogEvent>(_options.MaxLogLines);
        var flushInterval = TimeSpan.FromMilliseconds(FlushIntervalMs);
        var lastFlush = DateTime.MinValue;
        var currentThemeVersion = _themeVersion;

        while (!token.IsCancellationRequested)
        {
            if (!WaitForSignalOrThrottle(token, flushInterval, ref lastFlush))
                break;

            builder = RefreshBuilderIfThemeChanged(builder, ref currentThemeVersion);

            _signal.Reset();
            _buffer.TakeSnapshot(snapshot);
            EmitDetectedTokens(snapshot);
            RenderSnapshot(snapshot, builder);

            if (_richTextBox.IsDisposed || _richTextBox.Disposing)
                continue;

            _richTextBox.SetRtf(builder.Rtf, _options.AutoScroll, token);
            lastFlush = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Waits for the next signal and enforces the minimum flush interval throttle.
    /// Returns <c>false</c> if the <paramref name="token"/> was cancelled and processing should stop.
    /// </summary>
    private bool WaitForSignalOrThrottle(CancellationToken token, TimeSpan flushInterval, ref DateTime lastFlush)
    {
        _signal.WaitOne();
        if (token.IsCancellationRequested)
            return false;

        var elapsed = DateTime.UtcNow - lastFlush;
        if (elapsed >= flushInterval) return true;
        var remaining = flushInterval - elapsed;
        return !token.WaitHandle.WaitOne(remaining);
    }

    /// <summary>
    /// Returns a new <see cref="RtfBuilder"/> when the theme has changed since the last render cycle,
    /// otherwise returns the existing builder unchanged.
    /// </summary>
    private RtfBuilder RefreshBuilderIfThemeChanged(RtfBuilder builder, ref int currentThemeVersion)
    {
        var latestVersion = _themeVersion;
        if (currentThemeVersion == latestVersion)
            return builder;

        currentThemeVersion = latestVersion;
        return new RtfBuilder(_options.Theme);
    }

    private void RenderSnapshot(List<LogEvent> snapshot, RtfBuilder builder)
    {
        builder.Clear();
        foreach (var evt in snapshot)
        {
            _renderer.Render(evt, builder);
        }
    }

    private void EmitDetectedTokens(List<LogEvent> snapshot)
    {
        if (_options.OnTokensDetected == null)
        {
            return;
        }

        var uniqueTokens = new Dictionary<string, DetectedToken>(StringComparer.Ordinal);
        foreach (var logEvent in snapshot)
        {
            var tokens = _options.TokenDetector.Extract(logEvent);
            foreach (var token in tokens)
            {
                var key = _options.TokenDetector.BuildUniqueKey(token);
                if (!uniqueTokens.ContainsKey(key))
                {
                    uniqueTokens.Add(key, token);
                }
            }
        }

        if (uniqueTokens.Count == 0)
        {
            return;
        }

        _options.OnTokensDetected(new DetectedTokenBatch(new List<DetectedToken>(uniqueTokens.Values)));
    }

    private void OnLinkClicked(object? sender, LinkClickedEventArgs e)
    {
        if (_options.OnTokenClicked == null)
        {
            return;
        }

        var linkText = e.LinkText;
        if (string.IsNullOrWhiteSpace(linkText))
        {
            return;
        }

        if (!_options.TokenDetector.TryParseUri(linkText, out var token))
        {
            return;
        }
        _options.OnTokenClicked(token);
    }
}