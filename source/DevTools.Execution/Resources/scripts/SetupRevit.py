import sys
import builtins

def _is_ironpython():
    return '.net' in sys.version.lower()

_LOG_FUNC = '__log_func__'
_RDT_STATE = '__revitdevtool__'

if not _is_ironpython():
    import clr  # pyright: ignore[reportMissingImports] # noqa
    import os
    import site

    # Add Revit API references
    clr.AddReference("RevitAPI")
    clr.AddReference("RevitAPIUI")
    clr.AddReference("AdWindows")
    clr.AddReference("UIFramework")
    clr.AddReference("UIFrameworkServices")
    clr.AddReference("RevitDevTool")
    import System

    if int(__revit__.Application.VersionNumber) >= 2024:  # Revit 2024+ (WebView2 support)  # pyright: ignore[reportUndefinedVariable]
        clr.AddReference("Microsoft.Web.WebView2.Wpf")
        clr.AddReference("Microsoft.Web.WebView2.Core")

    if System.Environment.Version.Major >= 8:  # Revit 2025+ (.NET 8)
        clr.AddReference("System.Console")
        clr.AddReference("System.Diagnostics.TraceSource")

    # Ensure newly-installed packages are importable in the same runtime session.
    site_packages = os.path.join(sys.prefix, "Lib", "site-packages")
    if site_packages and os.path.isdir(site_packages):
        site.addsitedir(site_packages)

    # Initialize sys.__revitdevtool__ namespace for scope-local state (not global builtins pollution)
    if not hasattr(sys, _RDT_STATE):
        setattr(sys, _RDT_STATE, {})

def custom_print(*args, sep=' ', end='\n', file=None, flush=False):  # pyright: ignore[reportUnusedParameter]
    if not hasattr(builtins, _LOG_FUNC):
        return

    log_func = getattr(builtins, _LOG_FUNC)

    # Case 1: Single Argument -> Pass Raw Object (Enable Trace)
    if len(args) == 1:
        log_func(args[0])
        if end != '\n':
            log_func(end)
        return

    # Case 2: Mixed Content containing Complex Objects
    has_complex = any(not isinstance(a, (str, int, float, bool, type(None))) for a in args)

    if has_complex and sep == ' ':
        for arg in args:
            log_func(arg)
        if end != '\n':
            log_func(end)
        return

    # Case 3: Simple Text or Custom Separator -> Standard Join
    text = sep.join(str(arg) for arg in args) + end
    log_func(text)

builtins.print = custom_print

# Redirect stdout/stderr
class StdOutRedirector:
    def __init__(self, log_func_provider):
        self.log_func_provider = log_func_provider
        self._buffer = []

    def write(self, text):
        if not hasattr(self.log_func_provider, _LOG_FUNC):
            return
        if not text:
            return
        self._buffer.append(text)

    def flush(self):
        if not self._buffer:
            return
        log_func = getattr(self.log_func_provider, _LOG_FUNC, None)
        if log_func is None:
            self._buffer.clear()
            return
        merged = ''.join(self._buffer)
        self._buffer.clear()
        merged = merged.strip()
        if merged:
            log_func(merged)

sys.stdout = StdOutRedirector(builtins)
sys.stderr = StdOutRedirector(builtins)
