"""Verify real-time streaming of test output from Revit back to pytest.

Each test produces identifiable print output so we can confirm
stdout capture flows through the notification pipeline.
"""

import time


def test_streaming_stdout_basic():
    """Simple print should appear in captured stdout."""
    print("STREAM_MARKER_BASIC: hello from Revit")
    assert True


def test_streaming_stdout_multiline():
    """Multiple print lines are captured together."""
    print("STREAM_MARKER_MULTI_1: line one")
    print("STREAM_MARKER_MULTI_2: line two")
    print("STREAM_MARKER_MULTI_3: line three")
    assert True


def test_streaming_with_revit_version():
    """Verify Revit context is available and output is streamed."""
    app = __revit__.Application  # noqa: F821
    version = app.VersionNumber
    print(f"STREAM_MARKER_REVIT: Revit {version}")
    assert version is not None


def test_streaming_slow_test():
    """A slightly slower test — output should arrive before batch completes."""
    print("STREAM_MARKER_SLOW: start")
    time.sleep(0.5)
    print("STREAM_MARKER_SLOW: end")
    assert True


def test_streaming_failure_captured():
    """Verify failed test output is properly captured and streamed."""
    import pytest

    print("STREAM_MARKER_FAIL: this test verifies failure capture")
    with pytest.raises(AssertionError, match="intentional"):
        assert 1 == 2, "intentional assertion failure"


def test_streaming_after_failure():
    """Confirms pipeline continues after previous tests."""
    print("STREAM_MARKER_AFTER: still running after previous tests")
    assert True
