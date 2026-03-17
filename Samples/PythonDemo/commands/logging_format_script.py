# /// script
# dependencies = []
# ///

"""
Test Log Formatting and Syntax Highlighting
Demonstrates structured output with colors and object formatting.
Similar to RevitDevTool.DotnetDemo/Log.cs SerilogFormattingTestCmd
"""

import json
from datetime import datetime, timedelta
from uuid import uuid4


def test_log_levels():
    """Test log level detection via keywords"""
    print("=== LOG LEVEL DETECTION TESTS ===")
    print()

    # Prefix detection (highest priority)
    print("[INFO] This should be Information level")
    print("[WARN] This should be Warning level")
    print("[ERROR] This should be Error level")
    print("[FATAL] This should be Critical level")
    print("[DEBUG] This should be Debug level")
    print()

    # Keyword detection (fallback)
    print("Operation completed successfully")  # contains "completed" -> Info
    print("Warning: Memory usage is high")  # contains "warning" -> Warning
    print("Error occurred during processing")  # contains "error" -> Error
    print("Fatal crash detected in system")  # contains "fatal" -> Critical
    print("Just a regular debug message")  # no keywords -> Debug


def test_scalar_values():
    """Test scalar values with formatting"""
    print()
    print("=== SCALAR VALUES TEST ===")
    print()

    print(f"Cache hit ratio: {0.856:.2%}")
    print(f"Response time: {datetime.now()}")
    print(f"Is valid: {True}")
    print("API version: 2.1.0")
    print("WARNING: Memory usage: 1024 MB (threshold: 2048 MB)")
    print("ERROR: Failed order ORD-12345 with code E500")


def test_simple_objects():
    """Test simple object printing"""
    print()
    print("=== SIMPLE OBJECTS TEST ===")
    print()

    metrics = {"CPU": 85.5, "Memory": 1024, "Connections": 42}
    print(metrics)
    print()

    # Pretty JSON
    print(json.dumps(metrics, indent=2))


def test_complex_objects():
    """Test complex nested objects"""
    print()
    print("=== COMPLEX OBJECTS TEST ===")
    print()

    # API Request
    api_request = {
        "Method": "POST",
        "Endpoint": "/api/users",
        "RequestId": str(uuid4()),
        "Headers": {"ContentType": "application/json", "Authorization": "Bearer ***"},
    }
    print(json.dumps(api_request, indent=2))
    print()

    # User Activity
    user_action = {
        "UserId": "user123",
        "Action": "Login",
        "IP": "192.168.1.1",
        "UserAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
        "Timestamp": datetime.now(datetime.timezone.utc).isoformat(),
    }
    print(json.dumps(user_action, indent=2))
    print()

    # Configuration
    config = {
        "ConnectionTimeout": 30,
        "MaxRetries": 3,
        "EnableCompression": True,
        "AllowedOrigins": ["https://api.example.com", "https://admin.example.com"],
    }
    print(json.dumps(config, indent=2))


def test_deep_nested():
    """Test deep nested structures"""
    print()
    print("=== DEEP NESTED STRUCTURES TEST ===")
    print()

    # Audit Log
    audit_log = {
        "EventId": str(uuid4()),
        "Timestamp": datetime.now(datetime.timezone.utc).isoformat(),
        "User": {"Id": "user456", "Role": "Administrator", "Department": "IT"},
        "Action": "ConfigurationUpdate",
        "Changes": [
            {"Property": "MaxConnections", "OldValue": 100, "NewValue": 200},
            {"Property": "Timeout", "OldValue": 30, "NewValue": 60},
        ],
    }
    print(json.dumps(audit_log, indent=2))
    print()

    # Deployment Info
    deployment_info = {
        "DeploymentId": str(uuid4()),
        "Environment": "Production",
        "Version": "2.1.0",
        "Timestamp": datetime.now(datetime.timezone.utc).isoformat(),
        "Services": [
            {
                "Name": "API",
                "Status": "Healthy",
                "Metrics": {"ResponseTime": 45, "ErrorRate": 0.01, "RequestsPerSecond": 150},
            },
            {
                "Name": "Database",
                "Status": "Degraded",
                "Metrics": {"ConnectionCount": 85, "QueryTime": 120, "ReplicationLag": 5},
            },
        ],
        "Infrastructure": {
            "Region": "us-east-1",
            "InstanceType": "t3.large",
            "Scaling": {"MinInstances": 2, "MaxInstances": 5, "CurrentInstances": 3},
        },
    }
    print(json.dumps(deployment_info, indent=2))


def test_exception():
    """Test exception handling"""
    print()
    print("=== EXCEPTION TEST ===")
    print()

    try:
        raise ValueError("Test exception message")
    except Exception as e:
        print(f"ERROR: Exception caught: {e}")
        import traceback

        print(traceback.format_exc())


if __name__ == "__main__":
    # Uncomment the tests you want to run:
    test_log_levels()
    test_scalar_values()
    test_simple_objects()
    # test_complex_objects()
    # test_deep_nested()
    # test_exception()
