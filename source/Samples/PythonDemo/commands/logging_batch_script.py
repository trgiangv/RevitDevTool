# /// script
# dependencies = []
# ///

"""
Test Batch Logging Performance
Demonstrates high-volume logging scenarios.
Similar to RevitDevTool.DotnetDemo/Log.cs batch tests
"""

import time


def test_batch_small():
    """Test logging 100 messages"""
    print("=== Batch Logging Test (100 messages) ===")
    start = time.time()

    for i in range(1, 101):
        print(f"Processing batch item Step {i}")
        print(f"Batch processing metrics for Step {i}")
        print(f"Completed processing Step {i:03d}")

    elapsed = (time.time() - start) * 1000
    print(f"WARNING: Total processing time: {elapsed:.2f} ms")


def test_batch_medium():
    """Test logging 1,000 messages"""
    print("=== Batch Logging Test (1,000 messages) ===")
    start = time.time()

    for i in range(1, 1001):
        print(f"Processing batch item Step {i}")
        if i % 100 == 0:
            print(f"Milestone: Completed {i} steps")

    elapsed = (time.time() - start) * 1000
    print(f"WARNING: Total processing time: {elapsed:.2f} ms")


def test_batch_large():
    """Test logging 10,000 messages (stress test)"""
    print("=== Batch Logging Test (10,000 messages) ===")
    print("WARNING: This may take a while...")
    start = time.time()

    for i in range(1, 10001):
        print(f"Processing batch item Step {i}")
        if i % 1000 == 0:
            print(f"Milestone: Completed {i} steps")

    elapsed = (time.time() - start) * 1000
    print(f"WARNING: Total processing time: {elapsed:.2f} ms")


def test_log_levels():
    """Test different log level keywords"""
    print("=== Log Level Keywords Test ===")

    # These should be colored based on keywords
    print("Operation completed successfully")  # Info (contains "completed")
    print("WARNING: Memory usage is high")  # Warning
    print("ERROR: Failed to process element")  # Error
    print("FATAL: Critical system failure")  # Critical
    print("Just a regular debug message")  # Debug

    print()
    print("[INFO] Explicit info level")
    print("[WARN] Explicit warning level")
    print("[ERROR] Explicit error level")
    print("[DEBUG] Explicit debug level")


def test_large_strings():
    """Test logging large strings"""
    print("=== Large String Logging Test ===")
    start = time.time()

    large_string = "X" * 1000
    for i in range(100):
        print(large_string)

    elapsed = (time.time() - start) * 1000
    print(f"WARNING: Total time for large string logging: {elapsed:.2f} ms")


def test_urls():
    """Test URL detection and formatting"""
    print("=== URL Logging Test ===")

    urls = [
        "http://example.com/resource1",
        "https://example.com/resource2",
        "http://example.com/api/v1/users",
        "https://api.github.com/repos/owner/repo",
    ]

    for url in urls:
        print(f"Accessing URL: {url}")


if __name__ == "__main__":
    # Uncomment the test you want to run:
    # test_batch_small()
    test_batch_medium()
    # test_batch_large()
    # test_log_levels()
    # test_large_strings()
    # test_urls()
