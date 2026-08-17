using System.Text.Json;
using DevTools.Ipc;
using DevTools.NUnit.Transport.Compatibility;
using DevTools.NUnit.Transport.Contracts;
using DevTools.NUnit.Transport.Results;
using DevTools.NUnit.Runner.Services;

namespace DevTools.TestRunner.Tests;

public sealed class NUnitPipeClientTests
{
    [Fact]
    public async Task HelloAsync_returns_protocol_v2_response()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (server)
        await using (client)
        {
            server.OnRequest(async (request, ct) =>
            {
                if (request.Method != NUnitProtocol.Hello)
                    return;

                var hello = request.Params!.Value.Deserialize(NUnitJsonContext.Default.NUnitHelloRequest);
                Assert.Equal(NUnitProtocol.CurrentVersion, hello!.ProtocolVersion);

                await server.SendResponseAsync(
                    request.Id!,
                    JsonSerializer.SerializeToElement(
                        new NUnitHelloResponse(
                            NUnitProtocol.CurrentVersion,
                            "Revit",
                            "2025",
                            1234,
                            false),
                        NUnitJsonContext.Default.NUnitHelloResponse),
                    ct);
            });

            var response = await client.HelloAsync(TestContext.Current.CancellationToken);

            Assert.Equal(NUnitProtocol.CurrentVersion, response.ProtocolVersion);
            Assert.Equal("Revit", response.Host);
            Assert.Equal("2025", response.HostVersion);
            Assert.Equal(1234, response.ProcessId);
            Assert.False(response.IsBusy);
        }
    }

    [Fact]
    public async Task HelloAsync_rejects_successful_v1_hello_response()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (server)
        await using (client)
        {
            server.OnRequest(async (request, ct) =>
            {
                await server.SendResponseAsync(
                    request.Id!,
                    JsonSerializer.SerializeToElement(
                        new NUnitHelloResponse(
                            ProtocolVersion: 1,
                            Host: "Revit",
                            HostVersion: "2025",
                            ProcessId: 1234,
                            IsBusy: false),
                        NUnitJsonContext.Default.NUnitHelloResponse),
                    ct);
            });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.HelloAsync(TestContext.Current.CancellationToken));

            Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("1", exception.Message, StringComparison.Ordinal);
            Assert.Contains(NUnitProtocol.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task HelloAsync_rejects_incompatible_protocol_version()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (server)
        await using (client)
        {
            server.OnRequest(async (request, ct) =>
            {
                var hello = request.Params!.Value.Deserialize(NUnitJsonContext.Default.NUnitHelloRequest);
                await server.SendErrorAsync(
                    request.Id!,
                    ProtocolCompatibility.IncompatibleCode,
                    $"NUnit protocol version {hello!.ProtocolVersion} is not supported.",
                    JsonSerializer.SerializeToElement(new { requested = hello.ProtocolVersion, expected = NUnitProtocol.CurrentVersion }),
                    ct);
            });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.HelloAsync(TestContext.Current.CancellationToken));

            Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task DiscoverAsync_maps_v2_dto_fields()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (server)
        await using (client)
        {
            server.OnRequest(async (request, ct) =>
            {
                Assert.Equal(NUnitProtocol.Discover, request.Method);
                var discover = request.Params!.Value.Deserialize(NUnitJsonContext.Default.NUnitDiscoverRequest);
                Assert.Equal(@"C:\tests\Sample.dll", discover!.AssemblyPath);
                Assert.Null(discover.Filter);

                await server.SendResponseAsync(
                    request.Id!,
                    JsonSerializer.SerializeToElement(
                        FakeNUnitHostResponses.SampleDiscoverResponse(),
                        NUnitJsonContext.Default.NUnitDiscoverResponse),
                    ct);
            });

            var response = await client.DiscoverAsync(
                @"C:\tests\Sample.dll",
                filter: null,
                TestContext.Current.CancellationToken);

            Assert.Equal("generation-one", response.GenerationId);
            Assert.Equal("generation.loaded", response.RuntimeDiagnostic!.Code);
            Assert.Equal(2, response.Cases.Count);

            var passed = response.Cases[0];
            Assert.Equal("case-1", passed.Id);
            Assert.Equal("fixture-1", passed.ParentTestId);
            Assert.Equal("Smoke", passed.Traits![0].Value);
            Assert.Equal("Fixture.cs", passed.Source!.File);
            Assert.Equal(42, passed.Source.Line);

            var skipped = response.Cases[1];
            Assert.Equal("not ready", skipped.SkipReason);
        }
    }

    [Fact]
    public async Task DiscoverAsync_sends_normalized_framework_filter_xml()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (server)
        await using (client)
        {
            server.OnRequest(async (request, ct) =>
            {
                var discover = request.Params!.Value.Deserialize(NUnitJsonContext.Default.NUnitDiscoverRequest);
                Assert.Equal("<filter><cat>Smoke</cat></filter>", discover!.Filter);

                await server.SendResponseAsync(
                    request.Id!,
                    JsonSerializer.SerializeToElement(
                        new NUnitDiscoverResponse([]),
                        NUnitJsonContext.Default.NUnitDiscoverResponse),
                    ct);
            });

            _ = await client.DiscoverAsync(
                @"C:\tests\Sample.dll",
                "  <filter><cat>Smoke</cat></filter>  ",
                TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task DiscoverAsync_rejects_plain_tsl_filter()
    {
        var (_, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (client)
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                client.DiscoverAsync(@"C:\tests\Sample.dll", "cat == 'Smoke'", TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task RunAsync_returns_v2_response_with_attachments_and_diagnostics()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (server)
        await using (client)
        {
            server.OnRequest(async (request, ct) =>
            {
                Assert.Equal(NUnitProtocol.Run, request.Method);
                var run = request.Params!.Value.Deserialize(NUnitJsonContext.Default.NUnitRunRequest);
                Assert.Equal(@"C:\tests\Sample.dll", run!.AssemblyPath);
                Assert.Null(run.Filter);

                await server.SendResponseAsync(
                    request.Id!,
                    JsonSerializer.SerializeToElement(
                        FakeNUnitHostResponses.SampleRunResponse(run.RunId),
                        NUnitJsonContext.Default.NUnitRunResponse),
                    ct);
            });

            var response = await client.RunAsync(
                @"C:\tests\Sample.dll",
                filter: null,
                progress: null,
                TestContext.Current.CancellationToken);

            Assert.Equal("generation-one", response.GenerationId);
            Assert.Equal("run.complete", response.RuntimeDiagnostic!.Code);
            var result = Assert.Single(response.Cases);
            Assert.Equal("trace", result.Attachments![0].Name);
            Assert.Equal(@"C:\temp\trace.txt", result.Attachments[0].Path);
        }
    }

    [Fact]
    public async Task RunAsync_reports_progress_before_final_response()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (server)
        await using (client)
        {
            var progressEvents = new List<NUnitProgressEvent>();
            var progress = new Progress<NUnitProgressEvent>(evt => progressEvents.Add(evt));

            server.OnRequest(async (request, ct) =>
            {
                var run = request.Params!.Value.Deserialize(NUnitJsonContext.Default.NUnitRunRequest);
                var progressCase = new NUnitCaseResult(
                    "case-1",
                    "PlainTest",
                    NUnitOutcomes.Passed,
                    5,
                    null,
                    null,
                    null);

                await server.SendNotificationAsync(
                    NUnitProtocol.Progress,
                    JsonSerializer.SerializeToElement(
                        new NUnitProgressEvent(run!.RunId, progressCase),
                        NUnitJsonContext.Default.NUnitProgressEvent),
                    ct);

                await server.SendResponseAsync(
                    request.Id!,
                    JsonSerializer.SerializeToElement(
                        FakeNUnitHostResponses.SampleRunResponse(run.RunId),
                        NUnitJsonContext.Default.NUnitRunResponse),
                    ct);
            });

            _ = await client.RunAsync(
                @"C:\tests\Sample.dll",
                filter: null,
                progress,
                TestContext.Current.CancellationToken);

            Assert.Single(progressEvents);
            Assert.Equal("PlainTest", progressEvents[0].Case.Name);
            Assert.Equal(NUnitOutcomes.Passed, progressEvents[0].Case.Outcome);
        }
    }

    [Fact]
    public async Task RunAsync_does_not_duplicate_terminal_progress_events()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (server)
        await using (client)
        {
            var progressEvents = new List<NUnitProgressEvent>();
            var progress = new Progress<NUnitProgressEvent>(evt => progressEvents.Add(evt));

            server.OnRequest(async (request, ct) =>
            {
                var run = request.Params!.Value.Deserialize(NUnitJsonContext.Default.NUnitRunRequest);
                var progressCase = new NUnitCaseResult(
                    "case-1",
                    "PlainTest",
                    NUnitOutcomes.Passed,
                    5,
                    null,
                    null,
                    null);

                var progressPayload = JsonSerializer.SerializeToElement(
                    new NUnitProgressEvent(run!.RunId, progressCase),
                    NUnitJsonContext.Default.NUnitProgressEvent);

                await server.SendNotificationAsync(NUnitProtocol.Progress, progressPayload, ct);
                await server.SendNotificationAsync(NUnitProtocol.Progress, progressPayload, ct);

                await server.SendResponseAsync(
                    request.Id!,
                    JsonSerializer.SerializeToElement(
                        FakeNUnitHostResponses.SampleRunResponse(run.RunId),
                        NUnitJsonContext.Default.NUnitRunResponse),
                    ct);
            });

            _ = await client.RunAsync(
                @"C:\tests\Sample.dll",
                filter: null,
                progress,
                TestContext.Current.CancellationToken);

            Assert.Single(progressEvents);
        }
    }

    [Fact]
    public async Task RunAsync_ignores_foreign_run_progress()
    {
        var foreignRunId = Guid.NewGuid();
        var (server, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (server)
        await using (client)
        {
            var progressEvents = new List<NUnitProgressEvent>();
            var progress = new Progress<NUnitProgressEvent>(evt => progressEvents.Add(evt));
            Guid activeRunId = Guid.Empty;

            server.OnRequest(async (request, ct) =>
            {
                var run = request.Params!.Value.Deserialize(NUnitJsonContext.Default.NUnitRunRequest);
                activeRunId = run!.RunId;

                var progressCase = new NUnitCaseResult(
                    "case-1",
                    "PlainTest",
                    NUnitOutcomes.Passed,
                    5,
                    null,
                    null,
                    null);

                await server.SendNotificationAsync(
                    NUnitProtocol.Progress,
                    JsonSerializer.SerializeToElement(
                        new NUnitProgressEvent(foreignRunId, progressCase),
                        NUnitJsonContext.Default.NUnitProgressEvent),
                    ct);

                await server.SendNotificationAsync(
                    NUnitProtocol.Progress,
                    JsonSerializer.SerializeToElement(
                        new NUnitProgressEvent(activeRunId, progressCase),
                        NUnitJsonContext.Default.NUnitProgressEvent),
                    ct);

                await server.SendResponseAsync(
                    request.Id!,
                    JsonSerializer.SerializeToElement(
                        FakeNUnitHostResponses.SampleRunResponse(activeRunId),
                        NUnitJsonContext.Default.NUnitRunResponse),
                    ct);
            });

            _ = await client.RunAsync(
                @"C:\tests\Sample.dll",
                filter: null,
                progress,
                TestContext.Current.CancellationToken);

            var observed = Assert.Single(progressEvents);
            Assert.Equal(activeRunId, observed.RunId);
            Assert.Equal("case-1", observed.Case.Id);
        }
    }

    [Fact]
    public async Task RunAsync_caller_cancellation_sends_cancel_for_active_run_id()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (server)
        await using (client)
        {
            using var runCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            Guid activeRunId = Guid.Empty;
            var runStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            server.OnRequest(async (request, ct) =>
            {
                if (request.Method == NUnitProtocol.Cancel)
                {
                    cancelObserved.TrySetResult();
                    await server.SendResponseAsync(request.Id!, null, ct);
                    return;
                }

                if (request.Method != NUnitProtocol.Run)
                    return;

                var run = request.Params!.Value.Deserialize(NUnitJsonContext.Default.NUnitRunRequest);
                activeRunId = run!.RunId;
                runStarted.TrySetResult();

                await cancelObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            });

            var runTask = client.RunAsync(
                @"C:\tests\Sample.dll",
                filter: null,
                progress: null,
                runCts.Token);

            await runStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            runCts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
            await cancelObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(activeRunId, Assert.Single(server.ObservedCancelRunIds));
        }
    }

    [Fact]
    public async Task RunAsync_timeout_cancellation_sends_cancel_for_active_run_id()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (server)
        await using (client)
        {
            using var runCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

            Guid activeRunId = Guid.Empty;
            var runStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            server.OnRequest(async (request, ct) =>
            {
                if (request.Method == NUnitProtocol.Cancel)
                {
                    cancelObserved.TrySetResult();
                    await server.SendResponseAsync(request.Id!, null, ct);
                    return;
                }

                if (request.Method != NUnitProtocol.Run)
                    return;

                var run = request.Params!.Value.Deserialize(NUnitJsonContext.Default.NUnitRunRequest);
                activeRunId = run!.RunId;
                runStarted.TrySetResult();

                await cancelObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            });

            var runTask = client.RunAsync(
                @"C:\tests\Sample.dll",
                filter: null,
                progress: null,
                runCts.Token);

            await runStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            runCts.CancelAfter(TimeSpan.FromMilliseconds(250));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
            await cancelObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(activeRunId, Assert.Single(server.ObservedCancelRunIds));
        }
    }

    [Fact]
    public async Task RunAsync_cancellation_before_run_sent_does_not_send_cancel()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (server)
        await using (client)
        {
            using var runCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            runCts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.RunAsync(@"C:\tests\Sample.dll", null, null, runCts.Token));

            Assert.Empty(server.ObservedCancelRunIds);
        }
    }

    [Fact]
    public async Task RunAsync_abandoned_run_response_is_discarded_before_subsequent_requests()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (server)
        await using (client)
        {
            for (var cycle = 0; cycle < 3; cycle++)
            {
                server.ResetRequestHandlers();
                using var runCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
                var runStarted = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                var cancelObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                server.OnRequest(async (request, ct) =>
                {
                    if (request.Method == NUnitProtocol.Cancel)
                    {
                        cancelObserved.TrySetResult();
                        await server.SendResponseAsync(request.Id!, null, ct);
                        return;
                    }

                    if (request.Method == NUnitProtocol.Hello)
                    {
                        await server.SendResponseAsync(
                            request.Id!,
                            JsonSerializer.SerializeToElement(
                                new NUnitHelloResponse(
                                    NUnitProtocol.CurrentVersion,
                                    "Revit",
                                    "2025",
                                    1234,
                                    false),
                                NUnitJsonContext.Default.NUnitHelloResponse),
                            ct);
                        return;
                    }

                    if (request.Method == NUnitProtocol.Discover)
                    {
                        await server.SendResponseAsync(
                            request.Id!,
                            JsonSerializer.SerializeToElement(
                                new NUnitDiscoverResponse([]),
                                NUnitJsonContext.Default.NUnitDiscoverResponse),
                            ct);
                        return;
                    }

                    if (request.Method != NUnitProtocol.Run)
                        return;

                    var run = request.Params!.Value.Deserialize(NUnitJsonContext.Default.NUnitRunRequest);
                    runStarted.TrySetResult(request.Id!);

                    await cancelObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

                    await server.SendResponseAsync(
                        request.Id!,
                        JsonSerializer.SerializeToElement(
                            new NUnitRunResponse(
                                run!.RunId,
                                new NUnitRunSummary(0, 0, 0, 0, 0, 1),
                                []),
                            NUnitJsonContext.Default.NUnitRunResponse),
                        ct);
                });

                var runTask = client.RunAsync(@"C:\tests\Sample.dll", null, null, runCts.Token);
                _ = await runStarted.Task.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);
                runCts.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
                await cancelObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

                var hello = await client.HelloAsync(TestContext.Current.CancellationToken);
                Assert.Equal(NUnitProtocol.CurrentVersion, hello.ProtocolVersion);

                var discover = await client.DiscoverAsync(
                    @"C:\tests\Sample.dll",
                    null,
                    TestContext.Current.CancellationToken);
                Assert.Empty(discover.Cases);

                Assert.Equal(0, client.InboxDepthForTesting);
                Assert.Equal(0, client.PendingDiscardedResponseCountForTesting);
            }
        }
    }

    [Fact]
    public async Task RunAsync_discarded_cancel_response_does_not_block_subsequent_hello()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (server)
        await using (client)
        {
            using var runCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var runStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            server.OnRequest(async (request, ct) =>
            {
                if (request.Method == NUnitProtocol.Cancel)
                {
                    await server.SendResponseAsync(request.Id!, null, ct);
                    return;
                }

                if (request.Method == NUnitProtocol.Hello)
                {
                    await server.SendResponseAsync(
                        request.Id!,
                        JsonSerializer.SerializeToElement(
                            new NUnitHelloResponse(
                                NUnitProtocol.CurrentVersion,
                                "Revit",
                                "2025",
                                1234,
                                false),
                            NUnitJsonContext.Default.NUnitHelloResponse),
                        ct);
                    return;
                }

                if (request.Method != NUnitProtocol.Run)
                    return;

                runStarted.TrySetResult();
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            });

            var runTask = client.RunAsync(@"C:\tests\Sample.dll", null, null, runCts.Token);
            await runStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            runCts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

            var hello = await client.HelloAsync(TestContext.Current.CancellationToken);
            Assert.Equal(NUnitProtocol.CurrentVersion, hello.ProtocolVersion);
        }
    }

    [Fact]
    public async Task CancelAsync_sends_cancel_request_without_blocking_run_response()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (server)
        await using (client)
        {
            var cancelObserved = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);

            server.OnRequest(async (request, ct) =>
            {
                if (request.Method == NUnitProtocol.Cancel)
                {
                    var cancel = request.Params!.Value.Deserialize(NUnitJsonContext.Default.NUnitCancelRequest);
                    cancelObserved.TrySetResult(cancel!.RunId);
                    await server.SendResponseAsync(request.Id!, null, ct);
                    return;
                }

                if (request.Method != NUnitProtocol.Run)
                    return;

                var run = request.Params!.Value.Deserialize(NUnitJsonContext.Default.NUnitRunRequest);
                await client.CancelAsync(run!.RunId, ct);

                var cancelledRunId = await cancelObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
                Assert.Equal(run.RunId, cancelledRunId);

                await server.SendResponseAsync(
                    request.Id!,
                    JsonSerializer.SerializeToElement(
                        new NUnitRunResponse(
                            run.RunId,
                            new NUnitRunSummary(0, 0, 0, 0, 0, 1),
                            []),
                        NUnitJsonContext.Default.NUnitRunResponse),
                    ct);
            });

            var response = await client.RunAsync(
                @"C:\tests\Sample.dll",
                filter: null,
                progress: null,
                TestContext.Current.CancellationToken);

            Assert.Equal(1, response.Summary.Cancelled);
        }
    }

    [Fact]
    public async Task DiscoverAsync_propagates_server_error_details()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (server)
        await using (client)
        {
            server.OnRequest(async (request, ct) =>
            {
                await server.SendErrorAsync(
                    request.Id!,
                    "nunit/assembly_load_failed",
                    "Failed to load test assembly.",
                    JsonSerializer.SerializeToElement(new { details = "missing dependency" }),
                    ct);
            });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.DiscoverAsync(@"C:\missing.dll", null, TestContext.Current.CancellationToken));

            Assert.Contains("Failed to load test assembly.", exception.Message, StringComparison.Ordinal);
            Assert.Contains("missing dependency", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task DiscoverAsync_rejects_malformed_request_response()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateConnectedPairAsync();
        await using (server)
        await using (client)
        {
            server.OnRequest(async (request, ct) =>
            {
                await server.SendResponseAsync(request.Id!, null, ct);
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.DiscoverAsync(@"C:\tests\Sample.dll", null, TestContext.Current.CancellationToken));
        }
    }
}
