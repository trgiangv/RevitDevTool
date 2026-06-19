from Autodesk.Revit import DB, UI
from RevitDevTool.Core import RevitContextExecutor
from System import Action, Guid
from System.Windows import Controls, Window, WindowStartupLocation
from UIFramework import MainWindow


class RevitExternalEventHandler(UI.IExternalEventHandler):
    __namespace__ = str(Guid.NewGuid())  # must be unique for each execution (pythonnet3 limitation)

    def __init__(self):
        self.action = None
        self.external_event = UI.ExternalEvent.Create(self)

    def Execute(self, app):
        self.action.Invoke()

    def GetName(self):
        return "Revit DevTool Modeless External Event Handler"

    def Dispose(self):
        if self.external_event is not None:
            self.external_event.Dispose()

    def Raise(self, action : Action):
        self.action = action
        self.external_event.Raise()


class MyModelessForm(Window):
    def __init__(self, event_handler: UI.IExternalEventHandler):
        super(MyModelessForm, self).__init__()
        self.Text = "Revit DevTool Modeless"
        self.Width = 300
        self.Height = 150
        self.Title = "Revit DevTool Modeless"
        self.WindowStartupLocation = WindowStartupLocation.CenterOwner
        self.Owner = MainWindow.getMainWnd()
        self.event_handler = event_handler

        # create a grid to hold the button
        grid = Controls.Grid()
        self.Content = grid
        button = Controls.Button()
        button.Content = "Create Wall Async"
        button.Width = 200
        button.Height = 50
        button.Click += self.on_click
        grid.Children.Add(button)

    def on_click(self, sender, args):
        def create_wall_action(uiapp: UI.UIApplication):
            doc = uiapp.ActiveUIDocument.Document
            level = DB.FilteredElementCollector(doc).OfClass(DB.Level).FirstElement()
            wall_type = DB.FilteredElementCollector(doc).OfClass(DB.WallType).FirstElement()
            if level is None or wall_type is None:
                print("Error: No level or wall type found in project.")
                return

            line = DB.Line.CreateBound(DB.XYZ(0, 0, 0), DB.XYZ(10, 0, 0))
            with DB.Transaction(doc, "Create Wall") as t:
                t.Start()
                try:
                    DB.Wall.Create(doc, line, wall_type.Id, level.Id, 10, 0, False, False)
                    t.Commit()
                except Exception as e:
                    print("Error creating wall:", e)
                    t.RollBack()
            print("Wall created successfully")

        # 2. use RevitContextExecutor.Raise to run the wall creation code in Revit API context (preferred)
        RevitContextExecutor.Raise(Action[UI.UIApplication](create_wall_action))

        # 1. use ExternalEvent to run the wall creation code in Revit API context (alternative approach, not recommended due to small memory leak)
        # event_handler.Raise(Action(create_wall_action))

def main():
    event_handler = RevitExternalEventHandler()  # create an instance of the event handler in Revit API Context

    # modeless form
    form = MyModelessForm(event_handler)
    form.Show()
    form.Closed += lambda sender, args: event_handler.Dispose()  # dispose the event handler when the form is closed


if __name__ == "__main__":
    main()