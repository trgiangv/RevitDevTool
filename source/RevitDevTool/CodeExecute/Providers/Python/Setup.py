import sys
import builtins
import clr

clr.AddReference("RevitAPI")
clr.AddReference('RevitAPIUI')
clr.AddReference("AdWindows")
clr.AddReference("UIFramework")
clr.AddReference("UIFrameworkServices")

if int(__revit__.Application.VersionNumber) >= 2024:
    clr.AddReference("Microsoft.Web.WebView2.Wpf")
    clr.AddReference("Microsoft.Web.WebView2.Core")
    
if int(__revit__.Application.VersionNumber) >= 2025:
    clr.AddReference("System.Console")
    clr.AddReference("System.Diagnostics.TraceSource")

if __root__ not in sys.path:
    sys.path.append(__root__)

def custom_print(*args, sep=' ', end='\n'):
    # To use Trace Visualization, pass objects as separate arguments: print("Label", obj)

    # Case 1: Single Argument -> Pass Raw Object (Enable Trace)
    if len(args) == 1:
        __log_func__(args[0])
        if end != '\n': 
            __log_func__(end)
        return

    # Case 2: Mixed Content containing Complex Objects
    # If we just str(obj), we lose Trace ability. 
    # If using default separator, we split them into separate logs to preserve objects.
    has_complex = any(not isinstance(a, (str, int, float, bool, type(None))) for a in args)
    
    if has_complex and sep == ' ':
        for arg in args:
            __log_func__(arg)
        if end != '\n': 
            __log_func__(end)
        return

    # Case 3: Simple Text or Custom Separator -> Standard Join
    text = sep.join(str(arg) for arg in args) + end
    __log_func__(text)

# Override built-in print
builtins.print = custom_print

# Redirect stdout/stderr
class StdOutRedirector:
    def __init__(self, log_func):
        self.log_func = log_func
    def write(self, text):
        # Avoid empty newlines from being logged separately if possible
        if text != '\n':
            self.log_func(text)
    def flush(self):
        pass

sys.stdout = StdOutRedirector(__log_func__)
sys.stderr = StdOutRedirector(__log_func__)