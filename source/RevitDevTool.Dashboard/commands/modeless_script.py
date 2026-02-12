from System import Action
from System.Windows import Window, Controls, WindowStartupLocation
from UIFramework import MainWindow
from Revit.Async import RevitTask
from Autodesk.Revit import DB, UI

uiapp : UI.UIApplication = __revit__ # type: ignore

class MyModelessForm(Window):
    def __init__(self):
        super(MyModelessForm, self).__init__()
        self.Text = "Revit DevTool Modeless"
        self.Width = 300
        self.Height = 150
        self.Title = "Revit DevTool Modeless"
        self.WindowStartupLocation = WindowStartupLocation.CenterOwner
        self.Owner = MainWindow.getMainWnd()

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
        def create_wall_action():
            doc = uiapp.ActiveUIDocument.Document
            level = DB.FilteredElementCollector(doc).OfClass(DB.Level).FirstElement()
            wall_type = DB.FilteredElementCollector(doc).OfClass(DB.WallType).FirstElement()
            if level is None or wall_type is None:
                print("Error: No level or wall type found in project.")
                return

            line = DB.Line.CreateBound(DB.XYZ(0, 0, 0), DB.XYZ(10, 0, 0))
            t = DB.Transaction(doc, "Create Wall Async")
            try:
                t.Start()
                DB.Wall.Create(doc, line, wall_type.Id, level.Id, 10, 0, False, False)
                t.Commit()
            except Exception as e:
                print("Error creating wall:", e)
                if t.HasStarted:
                    t.RollBack()
            finally:
                if t.HasEnded:
                    t.Dispose()

        RevitTask.RunAsync(Action(create_wall_action))

# modeless form
form = MyModelessForm()
form.Show()