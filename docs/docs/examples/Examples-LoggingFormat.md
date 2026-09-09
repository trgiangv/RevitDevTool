# Example: Logging & Syntax Highlighting

**Demo:** Test log formatting, colors, and structured output
**Script:** [`logging_format_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/logging_format_script.py)
**Time:** 1-2 minutes

---

## What It Demonstrates

- Log level detection via keywords
- Syntax highlighting for different data types
- JSON pretty-printing
- Exception stack traces with formatting

---

## Key Pattern: Log Level Detection

### Prefix Detection (Priority 1)

```python
print("[INFO] This should be Information level")
print("[WARN] This should be Warning level")
print("[ERROR] This should be Error level")
print("[FATAL] This should be Critical level")
```

**Output:** Each line appears in Trace Panel with appropriate color.

### Keyword Detection (Priority 2)

```python
print("Operation completed successfully")  # → Info (contains "completed")
print("Warning: Memory usage is high")     # → Warning (contains "warning")
print("Error occurred during processing")  # → Error (contains "error")
```

**Output:** Automatic color coding based on detected keywords.

---

## Key Pattern: Pretty JSON

```python
import json

data = {
    "project": "Office Building",
    "walls": 127,
    "floors": 8,
    "families": ["Walls", "Doors", "Windows"]
}

print(json.dumps(data, indent=2))
```

**Output in Trace Panel:**
```json
{
  "project": "Office Building",
  "walls": 127,
  "floors": 8,
  "families": [
    "Walls",
    "Doors",
    "Windows"
  ]
}
```

- Keys in purple
- Strings in yellow
- Numbers in cyan
- Proper indentation preserved

---

## Key Pattern: Exception Formatting

```python
try:
    result = 10 / 0
except Exception as e:
    print(f"ERROR: {e}")
    import traceback
    traceback.print_exc()
```

**Output:**
```
ERROR: division by zero
Traceback (most recent call last):
  File "logging_format_script.py", line 85, in test_exceptions
    result = 10 / 0
ZeroDivisionError: division by zero
```

- Stack trace with syntax highlighting
- File paths clickable (if configured)
- Line numbers visible

---

## Try It Yourself

1. **Load** folder: `samples/PythonDemo/commands/`
2. **Execute** `logging_format_script.py`
3. **Watch** Trace Panel for colored output
4. **Observe** different log levels and formatting

**Full source:** [`logging_format_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/logging_format_script.py)

---

## Related Examples

- **Logging performance** → [`logging_batch_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/logging_batch_script.py)
- **Data analysis** → [`data_analysis_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/data_analysis_script.py)
