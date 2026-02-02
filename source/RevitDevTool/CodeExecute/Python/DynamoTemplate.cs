namespace RevitDevTool.CodeExecute.Python;

public static class DynamoTemplate
{
  public const string TemplateName = "RevitDevTool_DynamoScriptItem_Name";
  public const string TemplateScripPath = "RevitDevTool_DynamoScriptItem_ScripPath";
  public const string TemplateDyn = """
                                       {
                                         "Uuid": "b354541b-6c89-4db0-80c0-bad37f4e48e1",
                                         "IsCustomNode": false,
                                         "Description": "",
                                         "Name": "{RevitDevTool_DynamoScriptItem_Name}",
                                         "ElementResolver": {
                                           "ResolutionMap": {}
                                         },
                                         "Inputs": [],
                                         "Outputs": [],
                                         "Nodes": [
                                           {
                                             "ConcreteType": "PythonNodeModels.PythonStringNode, PythonNodeModels",
                                             "Engine": "CPython3",
                                             "EngineName": "CPython3",
                                             "VariableInputPorts": true,
                                             "Id": "aab9496123214c8980e98421ba057f41",
                                             "NodeType": "ExtensionNode",
                                             "Inputs": [
                                               {
                                                 "Id": "c053ae633ce74e5494fbef24abed78ab",
                                                 "Name": "script",
                                                 "Description": "Python script to run.",
                                                 "UsingDefaultValue": false,
                                                 "Level": 2,
                                                 "UseLevels": false,
                                                 "KeepListStructure": false
                                               },
                                               {
                                                 "Id": "a95a85160e9745389a57cd3e00b80290",
                                                 "Name": "IN[0]",
                                                 "Description": "Input #0",
                                                 "UsingDefaultValue": false,
                                                 "Level": 2,
                                                 "UseLevels": false,
                                                 "KeepListStructure": false
                                               }
                                             ],
                                             "Outputs": [
                                               {
                                                 "Id": "7e32a32a5e75486f95b4821321eef8de",
                                                 "Name": "OUT",
                                                 "Description": "Result of the python script",
                                                 "UsingDefaultValue": false,
                                                 "Level": 2,
                                                 "UseLevels": false,
                                                 "KeepListStructure": false
                                               }
                                             ],
                                             "Replication": "Disabled",
                                             "Description": "Runs a Python script from a string."
                                           },
                                           {
                                             "ConcreteType": "PythonNodeModels.PythonNode, PythonNodeModels",
                                             "Code": "from System import IO\r\n\r\npython_file = r\"{RevitDevTool_DynamoScriptItem_ScripPath}\"\r\npython_content = IO.File.ReadAllText(python_file)\r\n\r\nOUT = python_content\r\n",
                                             "Engine": "CPython3",
                                             "EngineName": "CPython3",
                                             "VariableInputPorts": true,
                                             "Id": "382915cc7f0646d5b61cf9e5dff623b5",
                                             "NodeType": "PythonScriptNode",
                                             "Inputs": [
                                               {
                                                 "Id": "fe60fd388a084f5093d00a0f41060da6",
                                                 "Name": "IN[0]",
                                                 "Description": "Input #0",
                                                 "UsingDefaultValue": false,
                                                 "Level": 2,
                                                 "UseLevels": false,
                                                 "KeepListStructure": false
                                               }
                                             ],
                                             "Outputs": [
                                               {
                                                 "Id": "ad8d2f11e87d41828eed23ed25f43976",
                                                 "Name": "OUT",
                                                 "Description": "Result of the python script",
                                                 "UsingDefaultValue": false,
                                                 "Level": 2,
                                                 "UseLevels": false,
                                                 "KeepListStructure": false
                                               }
                                             ],
                                             "Replication": "Disabled",
                                             "Description": "Runs an embedded Python script."
                                           }
                                         ],
                                         "Connectors": [
                                           {
                                             "Start": "ad8d2f11e87d41828eed23ed25f43976",
                                             "End": "c053ae633ce74e5494fbef24abed78ab",
                                             "Id": "8995fcd21c8a472a8c0fe614d286971b",
                                             "IsHidden": "False"
                                           }
                                         ],
                                         "Dependencies": [],
                                         "NodeLibraryDependencies": [],
                                         "Thumbnail": "",
                                         "GraphDocumentationURL": null,
                                         "ExtensionWorkspaceData": [
                                           {
                                             "ExtensionGuid": "28992e1d-abb9-417f-8b1b-05e053bee670",
                                             "Name": "Properties",
                                             "Version": "2.19",
                                             "Data": {}
                                           },
                                           {
                                             "ExtensionGuid": "DFBD9CC0-DB40-457A-939E-8C8555555A9D",
                                             "Name": "Generative Design",
                                             "Version": "6.1",
                                             "Data": {}
                                           }
                                         ],
                                         "Author": "",
                                         "Linting": {
                                           "activeLinter": "None",
                                           "activeLinterId": "7b75fb44-43fd-4631-a878-29f4d5d8399a",
                                           "warningCount": 0,
                                           "errorCount": 0
                                         },
                                         "Bindings": [],
                                         "View": {
                                           "Dynamo": {
                                             "ScaleFactor": 1.0,
                                             "HasRunWithoutCrash": true,
                                             "IsVisibleInDynamoLibrary": true,
                                             "Version": "2.19.3.6394",
                                             "RunType": "Automatic",
                                             "RunPeriod": "1000"
                                           },
                                           "Camera": {
                                             "Name": "_Background Preview",
                                             "EyeX": -17.0,
                                             "EyeY": 24.0,
                                             "EyeZ": 50.0,
                                             "LookX": 12.0,
                                             "LookY": -13.0,
                                             "LookZ": -58.0,
                                             "UpX": 0.0,
                                             "UpY": 1.0,
                                             "UpZ": 0.0
                                           },
                                           "ConnectorPins": [],
                                           "NodeViews": [
                                             {
                                               "Id": "aab9496123214c8980e98421ba057f41",
                                               "Name": "Python Script From String",
                                               "IsSetAsInput": false,
                                               "IsSetAsOutput": false,
                                               "Excluded": false,
                                               "ShowGeometry": true,
                                               "X": 618.614802426862,
                                               "Y": 173.60212891862571
                                             },
                                             {
                                               "Id": "382915cc7f0646d5b61cf9e5dff623b5",
                                               "Name": "Python Script",
                                               "IsSetAsInput": false,
                                               "IsSetAsOutput": false,
                                               "Excluded": false,
                                               "ShowGeometry": true,
                                               "X": 345.59700291599569,
                                               "Y": 166.90033527876477
                                             }
                                           ],
                                           "Annotations": [],
                                           "X": -107.63214227560144,
                                           "Y": 118.31390037893523,
                                           "Zoom": 0.5724337066798143
                                         }
                                       }
                                       """;
  public const string TemplatePythonScript = """
                                               import clr
                                               import os

                                               # .NET References
                                               clr.AddReference("System")
                                               clr.AddReference("System.Windows.Forms")
                                               clr.AddReference("System.Drawing")

                                               try:
                                                   clr.AddReference("Microsoft.Web.WebView2.WinForms")
                                                   clr.AddReference("Microsoft.Web.WebView2.Core")
                                                   from Microsoft.Web.WebView2.WinForms import WebView2
                                                   from Microsoft.Web.WebView2.Core import CoreWebView2Environment, CoreWebView2HostResourceAccessKind
                                               except:
                                                   pass

                                               try:
                                                   clr.AddReference("System.Diagnostics.TraceSource")
                                                   clr.AddReference("System.Console")
                                               except:
                                                   pass

                                               from System import Action, Uri, Console
                                               from System.Diagnostics import Debug, Trace
                                               from System.Windows.Forms import Form, DockStyle, Application
                                               from System.Windows.Forms import MethodInvoker
                                               from System.Threading import Thread, ThreadStart, ApartmentState
                                               from System.Drawing import Size


                                               # Revit API
                                               clr.AddReference('RevitNodes')
                                               import Revit
                                               clr.ImportExtensions(Revit.GeometryConversion)
                                               clr.ImportExtensions(Revit.Elements)

                                               clr.AddReference('RevitServices')
                                               import RevitServices
                                               from RevitServices.Persistence import DocumentManager
                                               from RevitServices.Transactions import TransactionManager

                                               clr.AddReference('RevitAPI')
                                               from Autodesk.Revit import DB

                                               clr.AddReference('RevitAPIUI')
                                               from Autodesk.Revit import UI

                                               # Global Context
                                               uiapp = DocumentManager.Instance.CurrentUIApplication


                                               Console.WriteLine("RevitDevTool Python Script started.")
                                               OUT = "[Success] Executed"

                                               """;
}