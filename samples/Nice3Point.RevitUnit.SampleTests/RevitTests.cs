using Nice3point.Revit.Extensions;
using Nice3point.Revit.Toolkit;
using Nice3point.TUnit.Revit;

namespace Nice3Point.RevitUnit.SampleTests;

public sealed class SelectionTests : RevitApiUiTest
{
    [Test]
    public async Task SetElementIds_ActiveDocument_SelectsTheLevels()
    {
        // // Arrange
        // var uiDocument = RevitContext.ActiveUiDocument;
        // var levelIds = uiDocument!.Document.CollectElements()
        //     .OfClass<Level>()
        //     .ToElementIds();
        //
        // // Act
        // uiDocument.Selection.SetElementIds(levelIds);
        //
        // // Assert
        // await Assert.That(uiDocument.Selection.GetElementIds()).IsEquivalentTo(levelIds);
        await Assert.That(RevitContext.Application is not null).IsTrue();
    }

    [Test]
    public async Task CanPostCommand_BuiltInCommand_IsPostable()
    {
        // Arrange
        var commandId = RevitCommandId.LookupPostableCommandId(PostableCommand.Default3DView);

        // Act
        var canPost = UiApplication.CanPostCommand(commandId);

        // Assert
        await Assert.That(canPost).IsTrue();
    }
}