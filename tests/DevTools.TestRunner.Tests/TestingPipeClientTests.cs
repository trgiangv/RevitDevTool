using System.Text.Json;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using DevTools.TestRunner.Services;

namespace DevTools.TestRunner.Tests;

public sealed class TestingPipeClientTests
{
    [Fact]
    public async Task HelloAsync_returns_protocol_v2_response()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateTestingConnectedPairAsync();
        await using (server)
        await using (client)
        {
            server.OnRequest(async (request, ct) =>
            {
                if (request.Method != TestingProtocol.Hello)
                    return;

                var hello = request.Params!.Value.Deserialize(TestingJsonContext.Default.TestingHelloRequest);
                Assert.Equal(TestingProtocol.CurrentVersion, hello!.ProtocolVersion);
                Assert.Equal(TestingFrameworkIds.NUnit, hello.FrameworkId);

                await server.SendResponseAsync(
                    request.Id!,
                    JsonSerializer.SerializeToElement(
                        new TestingHelloResponse(
                            TestingProtocol.CurrentVersion,
                            TestingFrameworkIds.NUnit,
                            "Revit",
                            "2025",
                            1234,
                            false),
                        TestingJsonContext.Default.TestingHelloResponse),
                    ct);
            });

            var response = await client.HelloAsync(
                TestingFrameworkIds.NUnit,
                TestContext.Current.CancellationToken);

            Assert.Equal(TestingProtocol.CurrentVersion, response.ProtocolVersion);
            Assert.Equal(TestingFrameworkIds.NUnit, response.FrameworkId);
            Assert.Equal("Revit", response.Host);
            Assert.Equal("2025", response.HostVersion);
            Assert.Equal(1234, response.ProcessId);
            Assert.False(response.IsBusy);
        }
    }

    [Fact]
    public async Task RunAsync_maps_testing_run_response()
    {
        var runId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var (server, client) = await FakeNUnitHostPipeServer.CreateTestingConnectedPairAsync();
        await using (server)
        await using (client)
        {
            server.OnRequest(async (request, ct) =>
            {
                Assert.Equal(TestingProtocol.Run, request.Method);
                var run = request.Params!.Value.Deserialize(TestingJsonContext.Default.TestingRunRequest);
                Assert.Equal(TestingFrameworkIds.NUnit, run!.FrameworkId);
                Assert.Equal(@"C:\tests\Sample.dll", run.Assembly.Path);
                Assert.Equal("<filter><test>HostSmokeTests.Arithmetic</test></filter>", run.Selection.ProviderPayload);

                await server.SendResponseAsync(
                    request.Id!,
                    JsonSerializer.SerializeToElement(
                        new TestingRunResponse(
                            run.RunId,
                            TestingFrameworkIds.NUnit,
                            "generation-one",
                            [
                                new TestingCaseResult(
                                    "case-1",
                                    "Arithmetic",
                                    "Passed",
                                    12,
                                    null,
                                    null,
                                    "ok",
                                    null,
                                    [],
                                    []),
                            ],
                            TestingCancellationState.None,
                            null,
                            null),
                        TestingJsonContext.Default.TestingRunResponse),
                    ct);
            });

            var response = await client.RunAsync(
                new TestingRunRequest(
                    TestingProtocol.CurrentVersion,
                    runId,
                    TestingFrameworkIds.NUnit,
                    new TestingAssemblyReference(@"C:\tests\Sample.dll", null, null),
                    new TestingSelection([], "<filter><test>HostSmokeTests.Arithmetic</test></filter>"),
                    new Dictionary<string, string>()),
                progress: null,
                TestContext.Current.CancellationToken);

            Assert.Equal("generation-one", response.GenerationId);
            var passed = Assert.Single(response.Results);
            Assert.Equal("case-1", passed.TestId);
            Assert.Equal("Arithmetic", passed.DisplayName);
            Assert.Equal("Passed", passed.Outcome);
        }
    }

    [Fact]
    public async Task HelloAsync_rejects_incompatible_protocol_version()
    {
        var (server, client) = await FakeNUnitHostPipeServer.CreateTestingConnectedPairAsync();
        await using (server)
        await using (client)
        {
            server.OnRequest(async (request, ct) =>
            {
                await server.SendResponseAsync(
                    request.Id!,
                    JsonSerializer.SerializeToElement(
                        new TestingHelloResponse(
                            ProtocolVersion: 1,
                            FrameworkId: TestingFrameworkIds.NUnit,
                            Host: "Revit",
                            HostVersion: "2025",
                            ProcessId: 1234,
                            IsBusy: false),
                        TestingJsonContext.Default.TestingHelloResponse),
                    ct);
            });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.HelloAsync(TestingFrameworkIds.NUnit, TestContext.Current.CancellationToken));

            Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("1", exception.Message, StringComparison.Ordinal);
            Assert.Contains(TestingProtocol.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
        }
    }
}
