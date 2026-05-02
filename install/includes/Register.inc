{
  Post-install registration — add new app types here (XML filter runs once for Autodesk).
}

procedure OnPostInstall(AppIdx: Integer);
begin
  case AppIdx of
    0: begin
      FilterPackageContentsXml;
    end;
    1: begin
      { AutoCAD: XML pruning already done in branch 0 }
    end;
  else
    Log('OnPostInstall: no handler for app ' + IntToStr(AppIdx) + ' ("' + AppName(AppIdx) + '"), skipping');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  I: Integer;
begin
  if CurStep <> ssPostInstall then Exit;

  for I := 0 to APP_COUNT - 1 do
    OnPostInstall(I);
end;
