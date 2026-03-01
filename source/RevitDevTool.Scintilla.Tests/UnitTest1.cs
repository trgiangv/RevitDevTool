using System.Text;
using System.Threading;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Logger;
using RevitDevTool.Scintilla.Render;
using RevitDevTool.Scintilla.Search;
using RevitDevTool.Scintilla.Services;

namespace RevitDevTool.Scintilla.Tests;

public sealed class ScintillaLogViewerControllerTests
{
    [Fact]
    public void TryPost_DropsWhenQueueCapacityExceeded()
    {
        using var backend = new RecordingBackend();
        var controller = new ScintillaLogViewerController(
            backend,
            new InlineDispatcher(),
            options: new ScintillaLogViewerOptions
            {
                ChannelCapacity = 1,
                DropPolicy = BoundedChannelFullMode.Wait,
                FlushIntervalMs = 1000,
                DisableHistory = true,
                MaxBatchSize = 1
            });
        var sink = (ILogEntrySink)controller;

        var first = sink.TryPost(CreateEntry("first"));
        var second = sink.TryPost(CreateEntry("second"));

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, controller.DroppedMessages);
        Assert.Equal(1, controller.LocalWriteFails);
    }

    [Fact]
    public async Task ApplyFilter_RebuildsVisibleEntriesFromHistory()
    {
        using var backend = new RecordingBackend();
        var controller = new ScintillaLogViewerController(
            backend,
            new InlineDispatcher(),
            options: new ScintillaLogViewerOptions
            {
                FlushIntervalMs = 5,
                MaxBatchSize = 32,
                DisableHistory = false,
                MaxHistoryEntries = 100
            });
        var sink = (ILogEntrySink)controller;

        controller.Start();
        Assert.True(sink.TryPost(CreateEntry("Alpha token")));
        Assert.True(sink.TryPost(CreateEntry("beta token")));
        Assert.True(sink.TryPost(CreateEntry("gamma")));

        await Task.Delay(50);
        controller.ApplyFilter(new LogFilterOptions { TextContains = "TOKEN", MatchCase = false });
        await Task.Delay(50);
        controller.Stop();

        Assert.Contains(backend.AppendCalls, call => call.Count == 2);
    }

    [Fact]
    public async Task ApplyFilter_LevelOnly_UsesLevelSelection()
    {
        using var backend = new RecordingBackend();
        var controller = new ScintillaLogViewerController(
            backend,
            new InlineDispatcher(),
            options: new ScintillaLogViewerOptions
            {
                FlushIntervalMs = 5,
                MaxBatchSize = 32,
                DisableHistory = false,
                MaxHistoryEntries = 100
            });
        var sink = (ILogEntrySink)controller;

        controller.Start();
        Assert.True(sink.TryPost(CreateEntry("alpha", LogLevel.Information)));
        Assert.True(sink.TryPost(CreateEntry("beta", LogLevel.Warning)));
        Assert.True(sink.TryPost(CreateEntry("gamma", LogLevel.Error)));

        await Task.Delay(50);
        controller.ApplyFilter(new LogFilterOptions
        {
            AllowedLevels = new HashSet<LogLevel> { LogLevel.Warning, LogLevel.Error }
        });
        await Task.Delay(50);
        controller.Stop();

        Assert.Contains(backend.AppendCalls, call => call.Count == 2);
    }

    [Fact]
    public async Task ApplyFilter_NonAsciiText_IgnoreCase_MatchesUtf8Messages()
    {
        using var backend = new RecordingBackend();
        var controller = new ScintillaLogViewerController(
            backend,
            new InlineDispatcher(),
            options: new ScintillaLogViewerOptions
            {
                FlushIntervalMs = 5,
                MaxBatchSize = 32,
                DisableHistory = false,
                MaxHistoryEntries = 100
            });
        var sink = (ILogEntrySink)controller;

        controller.Start();
        Assert.True(sink.TryPost(CreateEntry("Xin chao the gioi")));
        Assert.True(sink.TryPost(CreateEntry("XIN CHÀO THẾ GIỚI")));
        Assert.True(sink.TryPost(CreateEntry("khong lien quan")));

        await Task.Delay(50);
        controller.ApplyFilter(new LogFilterOptions { TextContains = "chào thế", MatchCase = false });
        await Task.Delay(50);
        controller.Stop();

        Assert.Contains(backend.AppendCalls, call => call.Count == 1);
    }

    private static LogEntry CreateEntry(string text)
        => CreateEntry(text, LogLevel.Information);

    private static LogEntry CreateEntry(string text, LogLevel level)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return new LogEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Level = level,
            Source = "tests",
            Message = new ArraySegment<byte>(bytes, 0, bytes.Length),
            Properties = LogEntry.EmptyProperties
        };
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;
        public void Invoke(Action action) => action();
        public void BeginInvoke(Action action) => action();
    }

    private sealed class RecordingBackend : ILogDocumentBackend
    {
        public List<(int Count, bool AutoScroll)> AppendCalls { get; } = new();
        private int _lineCount;

        public void ConfigureStyles(ILogRenderStrategy renderStrategy)
        {
        }

        public void AppendBatch(IReadOnlyList<LogEntry> entries, ILogRenderStrategy renderStrategy, bool autoScroll)
        {
            AppendCalls.Add((entries.Count, autoScroll));
            _lineCount += entries.Count;
        }

        public void TrimHeadLines(int linesToRemove)
        {
            _lineCount = Math.Max(0, _lineCount - linesToRemove);
        }

        public int GetLineCount() => _lineCount;

        public void Clear()
        {
            _lineCount = 0;
        }

        public void Clear(ClearMode mode)
        {
            _lineCount = 0;
        }

        public LogSearchResult FindNext(string pattern, bool matchCase, bool useRegex)
            => LogSearchResult.NotFound;

        public LogSearchResult FindPrevious(string pattern, bool matchCase, bool useRegex)
            => LogSearchResult.NotFound;

        public LogSearchResult HighlightSearch(string pattern, bool matchCase, bool useRegex)
        {
            return LogSearchResult.NotFound;
        }

        public Task<int> CountMatchesAsync(string pattern, bool matchCase, bool useRegex, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public void Dispose()
        {
        }
    }
}
