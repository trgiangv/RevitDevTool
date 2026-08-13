using NUnit.Framework;

namespace DevTools.NUnit.Runtime.Fixtures;

[TestFixture]
public sealed class FullSemanticsFixture
{
    private const int FixtureInstanceMarker = 1701;
    private int _retryAttempts;
    private int _repeatInvocations;
    private readonly int _fixtureInstanceId = FixtureInstanceMarker;

    [OneTimeSetUp]
    public void FixtureOneTimeSetUp() =>
        AcceptanceRunContext.AppendToken("FullSemanticsFixture.OneTimeSetUp");

    [OneTimeTearDown]
    public void FixtureOneTimeTearDown() =>
        AcceptanceRunContext.AppendToken("FullSemanticsFixture.OneTimeTearDown");

    [SetUp]
    public void PerTestSetUp() =>
        AcceptanceRunContext.AppendToken(
            $"FullSemanticsFixture.SetUp:{TestContext.CurrentContext.Test.Name}");

    [TearDown]
    public void PerTestTearDown() =>
        AcceptanceRunContext.AppendToken(
            $"FullSemanticsFixture.TearDown:{TestContext.CurrentContext.Test.Name}");

    [Test]
    public void PlainTest_Passes()
    {
        Assert.That(GenerationMarker.Value, Is.EqualTo("generation-one"));
        Assert.Pass("plain-test");
    }

    [Test]
    public void GenerationMarker_IsReported() =>
        TestContext.WriteLine($"generation-marker={GenerationMarker.GetValue()}");

    [TestCase(1, 1, 2)]
    [TestCase(2, 3, 5)]
    [TestCase(10, -4, 6)]
    public void TestCase_Addition(int left, int right, int expected) =>
        Assert.That(left + right, Is.EqualTo(expected));

    [TestCaseSource(typeof(TestData), nameof(TestData.SimpleIntegers))]
    public void TestCaseSource_StaticProvider(int value) =>
        Assert.That(value % 2, Is.EqualTo(0));

    [TestCaseSource(typeof(TestData), nameof(TestData.ExecutableCases))]
    public void TestCaseSource_ExecutableProvider(string payload)
    {
        Assert.That(payload, Is.Not.Empty);
        Assert.That(TestData.ExecutableSourceInvocationCount, Is.GreaterThan(0));
    }

    [Test]
    public void MultipleAssertions_AllReported()
    {
        Assert.Multiple(() =>
        {
            Assert.That("generation-one", Is.EqualTo(GenerationMarker.Value));
            Assert.That(_fixtureInstanceId, Is.EqualTo(FixtureInstanceMarker));
            Assert.That(Environment.CurrentDirectory, Is.Not.Null);
        });
    }

    [Test]
    public void Warning_IsNonFatal()
    {
        Assert.Warn("acceptance-warning");
        Assert.Pass("warning-did-not-fail-test");
    }

    [Test]
    public void Inconclusive_TerminatesAsInconclusive() =>
        Assert.Inconclusive("acceptance-inconclusive");

    [Test]
    public void Output_IsWrittenToTestContext()
    {
        TestContext.WriteLine("acceptance-output-marker");
        System.Diagnostics.Trace.WriteLine("acceptance-trace-marker");
        System.Diagnostics.Debug.WriteLine("acceptance-debug-marker");
        Assert.Pass();
    }

    [Test]
    public void UnexpectedException_ThrowsOutsideAssertion()
    {
        throw new InvalidOperationException("acceptance-unexpected-exception");
    }

    [Test, Category("AcceptanceCategory"), Property("AcceptanceKey", "AcceptanceValue")]
    public void CategoryAndProperty_AreAttached() =>
        Assert.Pass();

    [Test, Ignore("acceptance-ignore")]
    public void Ignored_IsSkipped() =>
        Assert.Fail("ignored test must not execute");

    [Explicit("acceptance-explicit")]
    [Test]
    public void Explicit_RequiresExplicitSelection() =>
        Assert.Pass();

    [Test, Retry(3)]
    public void Retry_EventuallyPasses()
    {
        _retryAttempts++;
        AcceptanceRunContext.AppendToken($"FullSemanticsFixture.Retry_EventuallyPasses:attempt-{_retryAttempts}");

        if (_retryAttempts < 3)
        {
            Assert.Fail($"retry attempt {_retryAttempts}");
        }

        Assert.That(_retryAttempts, Is.EqualTo(3));
    }

    [Test, Repeat(2)]
    public void Repeat_ExecutesMultipleTimes()
    {
        _repeatInvocations++;
        AcceptanceRunContext.AppendToken($"FullSemanticsFixture.Repeat_ExecutesMultipleTimes:invocation-{_repeatInvocations}");
        Assert.Pass();
    }

    [Test]
    public void Lifecycle_SetUpPrecedesTearDown_ForThisTest()
    {
        var tokens = AcceptanceRunContext.ReadTokens();
        var testName = TestContext.CurrentContext.Test.Name;
        var setUpToken = $"FullSemanticsFixture.SetUp:{testName}";
        var tearDownSeen = tokens.Any(token => token.StartsWith($"FullSemanticsFixture.TearDown:{testName}", StringComparison.Ordinal));

        Assert.That(tokens, Does.Contain(setUpToken));
        Assert.That(tearDownSeen, Is.False, "tear-down for the active test must not run before the test body");
    }

    [Test]
    public async Task AsyncTest_Completes()
    {
        await Task.Delay(1).ConfigureAwait(false);
        Assert.Pass();
    }
}

[TestFixture]
public sealed class OrderedSemanticsFixture
{
    [Test, Order(1)]
    public void Ordered_First()
    {
        AcceptanceRunContext.AppendToken("OrderedSemanticsFixture.Ordered_First");
        Assert.Pass();
    }

    [Test, Order(2)]
    public void Ordered_Second()
    {
        AcceptanceRunContext.AppendToken("OrderedSemanticsFixture.Ordered_Second");

        var firstIndex = AcceptanceRunContext.IndexOfToken("OrderedSemanticsFixture.Ordered_First");
        var secondBodyIndex = AcceptanceRunContext.IndexOfToken("OrderedSemanticsFixture.Ordered_Second");

        Assert.That(firstIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(secondBodyIndex, Is.GreaterThan(firstIndex));
        Assert.Pass();
    }
}

[TestFixture]
public sealed class AsyncLifecycleFixture
{
    [OneTimeSetUp]
    public async Task AsyncOneTimeSetUp()
    {
        await Task.Yield();
        AcceptanceRunContext.AppendToken("AsyncLifecycleFixture.OneTimeSetUp");
    }

    [OneTimeTearDown]
    public async Task AsyncOneTimeTearDown()
    {
        await Task.Yield();
        AcceptanceRunContext.AppendToken("AsyncLifecycleFixture.OneTimeTearDown");
    }

    [SetUp]
    public async Task AsyncSetUp()
    {
        await Task.Yield();
        AcceptanceRunContext.AppendToken(
            $"AsyncLifecycleFixture.SetUp:{TestContext.CurrentContext.Test.Name}");
    }

    [TearDown]
    public async Task AsyncTearDown()
    {
        await Task.Yield();
        AcceptanceRunContext.AppendToken(
            $"AsyncLifecycleFixture.TearDown:{TestContext.CurrentContext.Test.Name}");
    }

    [Test]
    public async Task AsyncLifecycle_TestCompletes()
    {
        await Task.Delay(1).ConfigureAwait(false);
        var tokens = AcceptanceRunContext.ReadTokens();
        var testName = TestContext.CurrentContext.Test.Name;
        Assert.That(tokens, Does.Contain($"AsyncLifecycleFixture.SetUp:{testName}"));
        Assert.Pass();
    }
}
