{
  Legacy cleanup — removes old RevitDevTool installs from per-user Revit Addins folder.
  Targets: %APPDATA%\Autodesk\Revit\Addins\<Year>\RevitDevTool\
           %APPDATA%\Autodesk\Revit\Addins\<Year>\RevitDevTool.addin
}

procedure RemoveLegacyRevitDevTool;
var
  Year: Integer;
  AddinsBase, AddinFolder, AddinFile: string;
begin
  AddinsBase := ExpandConstant('{userappdata}\Autodesk\Revit\Addins');

  for Year := 2022 to 2026 do
  begin
    AddinFolder := AddinsBase + '\' + IntToStr(Year) + '\RevitDevTool';
    AddinFile   := AddinsBase + '\' + IntToStr(Year) + '\RevitDevTool.addin';

    if DirExists(AddinFolder) then
    begin
      Log('LegacyCleanup: removing folder ' + AddinFolder);
      if not DelTree(AddinFolder, True, True, True) then
        Log('LegacyCleanup: WARNING — could not fully remove ' + AddinFolder);
    end;

    if FileExists(AddinFile) then
    begin
      Log('LegacyCleanup: removing file ' + AddinFile);
      if not DeleteFile(AddinFile) then
        Log('LegacyCleanup: WARNING — could not remove ' + AddinFile);
    end;
  end;
end;
