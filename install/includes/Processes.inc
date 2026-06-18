{
  Monitored EXE names. Each instance:  ImageName - file version (PE resource) - PID: nnnnn
  Extend: MONITORED_EXE_COUNT + MonitoredExe case branches.
}

const
  MONITORED_EXE_COUNT = 4;

function MonitoredExe(I: Integer): string;
begin
  case I of
    0: Result := 'Revit.exe';
    1: Result := 'acad.exe';
    2: Result := 'acadlt.exe';
    3: Result := 'DevTools.Daemon.exe';
  else
    Result := '';
  end;
end;

function WqlDoubleBackslashes(const S: string): string;
var
  I: Integer;
begin
  Result := '';
  for I := 1 to Length(S) do
    if S[I] = '\' then
      Result := Result + '\\'
    else
      Result := Result + S[I];
end;

{ File version string from the PE resource (same idea as FileVersionInfo.FileVersion in .NET). }
function FileVersionOrUnknown(const ExePath: string): string;
var
  Locator, Service, ObjSet, Obj: Variant;
  V: Variant;
begin
  Result := '?';
  if ExePath = '' then Exit;
  if not FileExists(ExePath) then Exit;
  try
    Locator := CreateOleObject('WBEMScripting.SWBEMLocator');
    Service := Locator.ConnectServer('', 'root\CIMV2', '', '');
    ObjSet := Service.ExecQuery(
      'SELECT Version FROM CIM_DataFile WHERE Name="' +
      WqlDoubleBackslashes(ExePath) + '"');
    if VarIsNull(ObjSet) or (ObjSet.Count <= 0) then Exit;
    Obj := ObjSet.ItemIndex(0);
    if VarIsNull(Obj) then Exit;
    V := Obj.Version;
    if VarIsNull(V) then Exit;
    Result := V;
    if Result = '' then Result := '?';
  except
    Result := '?';
  end;
end;

function WmiProcessCount(const ImageName: string): Integer;
var
  Locator, Service, ObjSet: Variant;
begin
  Result := 0;
  try
    Locator := CreateOleObject('WBEMScripting.SWBEMLocator');
    Service := Locator.ConnectServer('', 'root\CIMV2', '', '');
    ObjSet  := Service.ExecQuery(
      Format('SELECT ProcessId FROM Win32_Process WHERE Name="%s"', [ImageName]));
    if not VarIsNull(ObjSet) then
      Result := ObjSet.Count;
  except
    Log(Format('WMI count failed for "%s": %s', [ImageName, GetExceptionMessage]));
  end;
end;

procedure AppendLinesForExe(const ImageName: string; var Lines: string);
var
  Locator, Service, ObjSet, Obj: Variant;
  I, C: Integer;
  Path, Ver, Pid: string;
begin
  if ImageName = '' then Exit;
  try
    Locator := CreateOleObject('WBEMScripting.SWBEMLocator');
    Service := Locator.ConnectServer('', 'root\CIMV2', '', '');
    ObjSet  := Service.ExecQuery(
      Format('SELECT ProcessId, ExecutablePath FROM Win32_Process WHERE Name="%s"', [ImageName]));
    if VarIsNull(ObjSet) then Exit;
    C := ObjSet.Count;
    if C <= 0 then Exit;

    for I := 0 to C - 1 do
    begin
      Obj := ObjSet.ItemIndex(I);
      if VarIsNull(Obj) then Continue;

      try
        Pid := IntToStr(Obj.ProcessId);
      except
        Pid := '?';
      end;

      try
        Path := Obj.ExecutablePath;
      except
        Path := '';
      end;
      if VarIsNull(Path) then
        Path := '';

      Ver := FileVersionOrUnknown(Path);

      Lines := Lines + '  - ' + ImageName + ' - ' + Ver + ' - PID: ' + Pid + #13#10;
    end;
  except
    Log(Format('WMI enumerate failed for "%s": %s', [ImageName, GetExceptionMessage]));
  end;
end;

function AnyBlockingProcessRunning: Boolean;
var
  I: Integer;
begin
  for I := 0 to MONITORED_EXE_COUNT - 1 do
    if MonitoredExe(I) <> '' then
      if WmiProcessCount(MonitoredExe(I)) > 0 then
      begin
        Result := True;
        Exit;
      end;
  Result := False;
end;

function BuildBlockingProcessesList: string;
var
  I: Integer;
begin
  Result := '';
  for I := 0 to MONITORED_EXE_COUNT - 1 do
    AppendLinesForExe(MonitoredExe(I), Result);
end;

procedure TerminateProcessesByImageName(const ImageName: string);
var
  Locator, Service, ObjSet, Obj: Variant;
  Attempt: Integer;
begin
  if ImageName = '' then Exit;
  for Attempt := 1 to 64 do
  begin
    if WmiProcessCount(ImageName) <= 0 then Exit;
    try
      Locator := CreateOleObject('WBEMScripting.SWBEMLocator');
      Service := Locator.ConnectServer('', 'root\CIMV2', '', '');
      ObjSet  := Service.ExecQuery(
        Format('SELECT * FROM Win32_Process WHERE Name="%s"', [ImageName]));
      if VarIsNull(ObjSet) or (ObjSet.Count <= 0) then Exit;
      Obj := ObjSet.ItemIndex(0);
      if VarIsNull(Obj) then Exit;
      Log(Format('Terminating %s PID %s', [ImageName, IntToStr(Obj.ProcessId)]));
      Obj.Terminate(0);
    except
      Log(Format('Terminate failed for "%s": %s', [ImageName, GetExceptionMessage]));
      Exit;
    end;
    Sleep(150);
  end;
end;

procedure TerminateAllMonitoredProcesses;
var
  Sweep, I: Integer;
begin
  for Sweep := 1 to 8 do
  begin
    if not AnyBlockingProcessRunning then Exit;
    for I := 0 to MONITORED_EXE_COUNT - 1 do
      TerminateProcessesByImageName(MonitoredExe(I));
    Sleep(400);
  end;
end;
