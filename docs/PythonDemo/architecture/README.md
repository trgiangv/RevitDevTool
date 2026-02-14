# PythonDemo Architecture Documentation

**Internal design documentation for PythonDemo module developers.**

---

## 📚 Documentation Files

### Core Architecture
- **[00-overview.md](00-overview.md)** - System architecture and component overview
- **[01-developer-guide.md](01-developer-guide.md)** - Development workflow and extension guide

---

## 🎯 Quick Navigation

### Understanding the System
Start with [00-overview.md](00-overview.md) to understand:
- Demo scripts collection structure
- Dashboard application architecture (React + Python + WebView2)
- Data analysis workflow examples
- Module integration patterns (CodeExecute, Logging, Visualization)

### Contributing Code
See [01-developer-guide.md](01-developer-guide.md) for:
- Setting up the development environment
- Running quality gates (Ruff, Mypy, ESLint)
- Creating new demo scripts
- Working with PEP 723 dependencies
- Testing patterns

---

## 🔗 Related Resources

- **Source Code:** [source/RevitDevTool.PythonDemo/](../../../source/RevitDevTool.PythonDemo/)
- **Demo Scripts:** [source/RevitDevTool.PythonDemo/commands/](../../../source/RevitDevTool.PythonDemo/commands/)
- **User Documentation:** [Examples-Overview.md (Wiki)](https://github.com/trgiangv/RevitDevTool/wiki/Examples-Overview)
- **Dashboard Guide:** [Examples-Dashboard.md (Wiki)](https://github.com/trgiangv/RevitDevTool/wiki/Examples-Dashboard)
- **Data Analysis Guide:** [Examples-DataAnalysis.md (Wiki)](https://github.com/trgiangv/RevitDevTool/wiki/Examples-DataAnalysis)

---

## 🏗️ Module Purpose

PythonDemo is a **collection of demonstration scripts** showcasing RevitDevTool's three core modules:

### Demo Scripts (`commands/`)
12+ example scripts demonstrating:
- **Data Analysis** - Polars-based Revit element analysis
- **Visualization** - Curve, solid, XYZ geometry rendering
- **Logging** - Format testing, batch performance, keyword detection
- **Scientific Computing** - sklearn, shapely, trimesh integration
- **UI Patterns** - Modeless windows, WebView2 dashboards

### Dashboard Application (`revit_dashboard/`)
Production-grade analytics dashboard:
- **Python backend** for data extraction and analytics
- **React + TypeScript frontend** with modern UI
- **WebView2** integration for in-Revit display
- **Excel export** for downstream workflows

This module serves as:
1. **Reference implementations** for common Revit Python patterns
2. **Testing suite** for RevitDevTool features
3. **Learning examples** for new users

---

_Last updated: 2026-02-14_
