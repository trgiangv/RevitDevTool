using Python.Runtime;
using RevitDevTool.Execution.Models;
namespace RevitDevTool.Utils;

[PublicAPI]
public static class ExecutionExtensions
{
    public static ExecutionResult ToExecutionResult(this Result result, string message, long durationMs)
    {
        return result switch
        {
            Result.Succeeded => ExecutionResult.Succeeded("Command completed successfully.", durationMs),
            Result.Cancelled => ExecutionResult.Cancelled(
                string.IsNullOrWhiteSpace(message) ? "Command cancelled." : message,
                durationMs),
            _ => ExecutionResult.Failed(
                string.IsNullOrWhiteSpace(message) ? "Command failed." : message,
                durationMs: durationMs)
        };
    }
    
    public static List<object> AsObjectCollection(this PyObject pyObject)
    {
        if (!pyObject.IsIterable()) return [];
        var netList = new List<object>();
        using (Py.GIL())
        {
            dynamic pyList = pyObject;
            foreach (dynamic item in pyList)
            {
                var managedItem = item.AsManagedObject(typeof(object));
                if (managedItem is null) continue;
                switch (managedItem)
                {
                    case PyObject nestedPyObj when nestedPyObj.IsIterable():
                        netList.AddRange(nestedPyObj.AsObjectCollection());
                        break;
                    case IEnumerable<object> enumerable and not IEnumerable<char>:
                        netList.AddRange(enumerable);
                        break;
                    default:
                        netList.Add(managedItem);
                        break;
                }
            }
        }
        return netList;
    }
}
