using Revit.Async;

namespace RevitDevTool.ExternalEvent.App.Commands;

public static class ContextLostRepro
{
    public static async Task Unsafe_AsyncDelegateOverload_ContextLoss()
    {
        // Reproduce the context loss issue in this overload:
        // RevitTask.RunAsync<TResult>(Func<UIApplication, Task<TResult>> function)
        await RevitTask.RunAsync<string>(async app =>
        {
            var doc = app.ActiveUIDocument.Document;

            if (new FilteredElementCollector(doc)
                    .OfClass(typeof(Wall))
                    .WhereElementIsNotElementType()
                    .FirstElement() is not Wall wall)
                throw new InvalidOperationException("No wall found for repro.");

            var viewName = doc.ActiveView?.Name ?? "<null view>";
            Console.WriteLine($"[Advanced][Thread {Environment.CurrentManagedThreadId}] Selected wallId={wall.Id}, view={viewName}");
            var wallId = wall.Id;

            // Simulate async IO inside the async delegate of this overload.
            await Task.Delay(800).ConfigureAwait(false);
            var generatedComment = $"unsafe-tag-{DateTime.UtcNow:HHmmss}";

            Console.WriteLine($"[Advanced][Thread {Environment.CurrentManagedThreadId}] After await in async delegate. Start transaction flow.");

            // If the continuation has lost API context, the transaction flow below will fail.
            using var txGroup = new TransactionGroup(doc, "Advanced Unsafe Update Group");
            txGroup.Start();

            using (var tx1 = new Transaction(doc, "Update Comments"))
            {
                tx1.Start();
                var targetWall = doc.GetElement(wallId) as Wall
                                 ?? throw new InvalidOperationException("Target wall not found.");

                var comments = targetWall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)
                               ?? throw new InvalidOperationException("Comments parameter not found.");

                comments.Set(generatedComment);
                tx1.Commit();
            }

            using (var tx2 = new Transaction(doc, "Update Mark"))
            {
                tx2.Start();
                var targetWall = doc.GetElement(wallId) as Wall
                                 ?? throw new InvalidOperationException("Target wall not found.");

                var mark = targetWall.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)
                           ?? throw new InvalidOperationException("Mark parameter not found.");

                mark.Set($"M-{DateTime.UtcNow:HHmmss}");
                tx2.Commit();
            }

            txGroup.Assimilate();
            return "done";
        });
    }

    public static async Task Safe_AsyncDelegateOverload_ReenterBeforeWrite()
    {
        // Step 1: Capture context and target in Revit API context.
        var payload = await RevitTask.RunAsync(app =>
        {
            var doc = app.ActiveUIDocument.Document;
            var wall = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .FirstElement() as Wall;

            if (wall is null)
                throw new InvalidOperationException("No wall found for repro.");

            var viewName = doc.ActiveView?.Name ?? "<null view>";
            Console.WriteLine($"[Advanced][Thread {Environment.CurrentManagedThreadId}] [SAFE] Selected wallId={wall.Id}, view={viewName}");
            return new Payload(doc, wall.Id);
        });

        // Step 2: Simulate async work outside Revit context.
        await Task.Delay(800).ConfigureAwait(false);
        var generatedComment = $"safe-tag-{DateTime.UtcNow:HHmmss}";

        Console.WriteLine($"[Advanced][Thread {Environment.CurrentManagedThreadId}] [SAFE] Outside API context, preparing data={generatedComment}");

        // Step 3: Re-enter Revit API context before starting transaction.
        await RevitTask.RunAsync<string>(_ =>
        {
            var doc = payload.Document;
            var wallId = payload.WallId;

            using var txGroup = new TransactionGroup(doc, "Advanced Safe Update Group");
            txGroup.Start();

            using (var tx1 = new Transaction(doc, "Update Comments"))
            {
                tx1.Start();
                var targetWall = doc.GetElement(wallId) as Wall
                                 ?? throw new InvalidOperationException("Target wall not found.");

                var comments = targetWall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)
                               ?? throw new InvalidOperationException("Comments parameter not found.");

                comments.Set(generatedComment);
                tx1.Commit();
            }

            using (var tx2 = new Transaction(doc, "Update Mark"))
            {
                tx2.Start();
                var targetWall = doc.GetElement(wallId) as Wall
                                 ?? throw new InvalidOperationException("Target wall not found.");

                var mark = targetWall.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)
                           ?? throw new InvalidOperationException("Mark parameter not found.");

                mark.Set($"M-{DateTime.UtcNow:HHmmss}");
                tx2.Commit();
            }

            txGroup.Assimilate();
            Console.WriteLine($"[Advanced][Thread {Environment.CurrentManagedThreadId}] [SAFE] Transaction flow committed.");
            return "done";
        });
    }

    private readonly record struct Payload(Document Document, ElementId WallId);
}