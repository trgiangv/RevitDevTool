from datetime import datetime, timezone

from System import Console
from System.Diagnostics import Debug, Trace


def emit_marker(kind):
    stamp = datetime.now(timezone.utc).isoformat()
    token = f"PIPE_BRIDGE_MARKER::{kind}::{stamp}"
    return token


if __name__ == "__main__":
    print(emit_marker("print"))
    Trace.WriteLine(emit_marker("trace"))
    Debug.WriteLine(emit_marker("debug"))
    Console.WriteLine(emit_marker("console"))
