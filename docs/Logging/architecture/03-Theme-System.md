# Color Keywords & Keyword Detection

## Overview

The logging system automatically detects keywords in log messages to determine severity levels and route messages to appropriate listeners. This enables intelligent log processing based on message content rather than just explicit log levels.

## Keyword Detection System

**Source:** `KeywordDetector.cs` in `RevitDevTool.Logging` namespace

Keywords are organized into three severity tiers and matched case-insensitively:

### Keyword Categories

| Category | Keywords | Log Level |
|----------|----------|-----------|
| **Critical** | CRITICAL, FATAL, PANIC, SECURITY BREACH, UNAUTHORIZED, PERMISSION DENIED | `LogLevel.Critical` |
| **Error** | ERROR, FAILED, EXCEPTION, TIMEOUT, INVALID, NOT FOUND, NULL REFERENCE, ACCESS VIOLATION | `LogLevel.Error` |
| **Warning** | WARNING, DEPRECATED, OBSOLETE, PERFORMANCE, MEMORY, LEAK, RETRY | `LogLevel.Warning` |

### Detection Flow

1. **Message arrives** at logging system
2. **KeywordDetector.DetectKeywordLevel()** scans message text
3. **Priority matching**: Critical → Error → Warning (first match wins)
4. **Result** returned to routing system
5. **Listeners filter** based on detected level

### Integration with TraceListeners

Custom `TraceListener` implementations can use keyword detection to filter and route messages:

- **AlertingListener**: Monitors for critical keywords, sends notifications
- **ColorizedFileWriter**: Applies ANSI color codes based on detected level
- **IssueTrackerListener**: Categorizes and tracks error patterns

## Color & Keyword Reference

| Keyword | Level | Icon | ANSI Color |
|---------|-------|------|-----------|
| CRITICAL, FATAL, PANIC | Critical | 🔴 | Red (#31) |
| ERROR, FAILED, EXCEPTION | Error | ❌ | Red (#31) |
| WARNING, DEPRECATED, LEAK | Warning | ⚠️ | Yellow (#33) |
| INFO, SUCCESS | Information | ℹ️ | Green (#32) |
| DEBUG, VERBOSE | Debug | 📝 | Blue (#34) |

---

