import clr
import sys
import os
import site
import builtins

# Add Revit API references
clr.AddReference("RevitAPI")
clr.AddReference("RevitAPIUI")
clr.AddReference("AdWindows")
clr.AddReference("UIFramework")
clr.AddReference("UIFrameworkServices")
clr.AddReference("Revit.Async")

# Version specific references
if int(__revit__.Application.VersionNumber) >= 2024:  # pyright: ignore[reportUndefinedVariable] # noqa: F821
    clr.AddReference("Microsoft.Web.WebView2.Wpf")
    clr.AddReference("Microsoft.Web.WebView2.Core")

if int(__revit__.Application.VersionNumber) >= 2025:  # pyright: ignore[reportUndefinedVariable] # noqa: F821
    clr.AddReference("System.Console")
    clr.AddReference("System.Diagnostics.TraceSource")

# Ensure newly-installed packages are importable in the same runtime session.
site_packages = os.path.join(sys.prefix, "Lib", "site-packages")
if site_packages and os.path.isdir(site_packages):
    site.addsitedir(site_packages)

# Initialize sys.__revitdevtool__ namespace for scope-local state (not global builtins pollution)
if not hasattr(sys, '__revitdevtool__'):
    sys.__revitdevtool__ = {}
 
def custom_print(*args, sep=' ', end='\n'):
    # __log_func__ will be injected globally
    if not hasattr(builtins, '__log_func__'):
        # Fallback to original print if __log_func__ is not available
        # This shouldn't happen during normal execution
        return
     
    log_func = builtins.__log_func__
    
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

# Override built-in print
builtins.print = custom_print

# Redirect stdout/stderr
class StdOutRedirector:
    def __init__(self, log_func_provider):
        self.log_func_provider = log_func_provider
    def write(self, text):
        # __log_func__ will be injected per-execution in PythonExecutor
        if not hasattr(self.log_func_provider, '__log_func__'):
            return
        log_func = self.log_func_provider.__log_func__
        # Avoid empty newlines from being logged separately if possible
        if text != '\n':
            log_func(text)
    def flush(self):
        pass

sys.stdout = StdOutRedirector(builtins)
sys.stderr = StdOutRedirector(builtins)