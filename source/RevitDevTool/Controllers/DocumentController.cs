using System.Diagnostics;
using Autodesk.Revit.UI;
using RevitDevTool.Bridge;
using RevitDevTool.Bridge.Enums.Revit;
using RevitDevTool.Bridge.Revit;
using RevitDevTool.Utils;

namespace RevitDevTool.Controllers;

/// <summary>
/// Opens and closes Revit documents based on <see cref="RevitOpenOptions"/>.
/// Headless mode uses Application.OpenDocumentFile (background, no UI).
/// UI mode uses UIApplication.OpenAndActivateDocument (activates in Revit).
/// </summary>
public static class DocumentController
{
    public static Document Open(UIApplication uiApp, ResolvedJob job)
    {
        return job.Open.Headless
            ? OpenHeadless(uiApp.Application, job)
            : OpenUi(uiApp, job);
    }

    public static void Close(UIApplication uiApp, ResolvedJob job, Document document)
    {
        if (!document.IsValidObject) return;

        if (job.Open.Headless)
        {
            document.Close(false);
        }
        else
        {
            uiApp.CloseActiveUiDocument();
        }

        Trace.TraceInformation($"[DocumentController] Closed document: {job.FilePath}");
    }

    private static Document OpenHeadless(Autodesk.Revit.ApplicationServices.Application app, ResolvedJob job)
    {
        var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(job.FilePath);
        var options = BuildOpenOptions(job);

        app.FailuresProcessing += OnFailuresProcessing;
        try
        {
            return app.OpenDocumentFile(modelPath, options);
        }
        finally
        {
            app.FailuresProcessing -= OnFailuresProcessing;
        }
    }

    private static Document OpenUi(UIApplication uiApp, ResolvedJob job)
    {
        var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(job.FilePath);
        var options = BuildOpenOptions(job);

        uiApp.Application.FailuresProcessing += OnFailuresProcessing;
        uiApp.DialogBoxShowing += OnDialogBoxShowing;
        try
        {
            var uiDocument = uiApp.OpenAndActivateDocument(modelPath, options, false);
            return uiDocument.Document;
        }
        finally
        {
            uiApp.Application.FailuresProcessing -= OnFailuresProcessing;
            uiApp.DialogBoxShowing -= OnDialogBoxShowing;
        }
    }

    private static Autodesk.Revit.DB.OpenOptions BuildOpenOptions(ResolvedJob job)
    {
        var revitOpts = job.Open as RevitOpenOptions ?? new RevitOpenOptions();

        var opts = new Autodesk.Revit.DB.OpenOptions
        {
            Audit = job.Open.Audit,
            OpenForeignOption = OpenForeignOption.Open,
            DetachFromCentralOption = MapCentralMode(revitOpts.DetachFromCentral),
            AllowOpeningLocalByWrongUser = revitOpts.AllowOpeningLocalByWrongUser
        };

        var worksetConfig = new WorksetConfiguration(MapWorksetMode(revitOpts.Workset));

        if (revitOpts.OpenWorksets.Count > 0)
        {
            var openIds = revitOpts.OpenWorksets.Select(id => new WorksetId(id)).ToList();
            worksetConfig.Open(openIds);
        }

        if (revitOpts.CloseWorksets.Count > 0)
        {
            var closeIds = revitOpts.CloseWorksets.Select(id => new WorksetId(id)).ToList();
            worksetConfig.Close(closeIds);
        }

        opts.SetOpenWorksetsConfiguration(worksetConfig);

        return opts;
    }

    private static void OnFailuresProcessing(object? sender, Autodesk.Revit.DB.Events.FailuresProcessingEventArgs e)
    {
        var accessor = e.GetFailuresAccessor();
        foreach (var failure in accessor.GetFailureMessages())
        {
            if (failure.GetSeverity() == FailureSeverity.Warning)
            {
                accessor.DeleteWarning(failure);
            }
            else
            {
                accessor.ResolveFailure(failure);
                e.SetProcessingResult(FailureProcessingResult.ProceedWithCommit);
            }
        }
    }

    private static void OnDialogBoxShowing(object? sender, Autodesk.Revit.UI.Events.DialogBoxShowingEventArgs e)
    {
        e.OverrideResult((int)TaskDialogResult.Close);
    }

    private static DetachFromCentralOption MapCentralMode(CentralMode mode) => mode switch
    {
        CentralMode.DetachAndPreserveWorksets => DetachFromCentralOption.DetachAndPreserveWorksets,
        CentralMode.DetachAndDiscardWorksets => DetachFromCentralOption.DetachAndDiscardWorksets,
        CentralMode.ClearTransmittedSaveAsNewCentral => DetachFromCentralOption.ClearTransmittedSaveAsNewCentral,
        CentralMode.DoNotDetach => DetachFromCentralOption.DoNotDetach,
        _ => DetachFromCentralOption.DetachAndPreserveWorksets
    };

    private static WorksetConfigurationOption MapWorksetMode(WorksetMode mode) => mode switch
    {
        WorksetMode.OpenAllWorksets => WorksetConfigurationOption.OpenAllWorksets,
        WorksetMode.CloseAllWorksets => WorksetConfigurationOption.CloseAllWorksets,
        WorksetMode.OpenLastViewed => WorksetConfigurationOption.OpenLastViewed,
        _ => WorksetConfigurationOption.OpenAllWorksets
    };
}
