{
  Wizard lifecycle — same AppId = in-place upgrade (no separate uninstall step required).
}

function InitializeSetup: Boolean;
var
  Running: string;
begin
  Result  := True;
  Running := BuildBlockingProcessesList;

  if Running = '' then
  begin
    Log('InitializeSetup: no blocking processes detected');
    Exit;
  end;

  Log('InitializeSetup: blocking processes:' + #13#10 + Running);

  if MsgBox('The following applications are currently running:' + #13#10 + #13#10 +
    Running + #13#10 + #13#10 +
    'Click OK to end these processes and continue setup, or Cancel to abort.' + #13#10 + #13#10 +
    'Unsaved work in those applications may be lost.',
    mbConfirmation, MB_OKCANCEL or $30) = IDCANCEL then
  begin
    Log('InitializeSetup: cancelled by user');
    Result := False;
  end
  else
  begin
    Log('InitializeSetup: user OK — terminating blocking processes via WMI');
    TerminateAllMonitoredProcesses;
    Sleep(500);
    if AnyBlockingProcessRunning then
    begin
      MsgBox('Setup could not end all listed processes (access denied or they restarted).' + #13#10 + #13#10 +
        'Close them manually, then run setup again.',
        mbError, MB_OK);
      Log('InitializeSetup: processes still running after Terminate');
      Result := False;
    end
    else
      Log('InitializeSetup: blocking processes cleared');
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := True;

  while AnyBlockingProcessRunning do
  begin
    Log('InitializeUninstall: blocking process still running');

    if MsgBox('One or more monitored applications are still running.' + #13#10 + #13#10 +
      'Click Retry to try ending them automatically, or Cancel to abort uninstall.' + #13#10 + #13#10 +
      'Unsaved work may be lost.',
      mbConfirmation, MB_RETRYCANCEL or $30) = IDCANCEL then
    begin
      Log('InitializeUninstall: cancelled by user');
      Result := False;
      Exit;
    end;

    Log('InitializeUninstall: user Retry — terminating blocking processes');
    TerminateAllMonitoredProcesses;
    Sleep(500);
  end;

  Log('InitializeUninstall: no blocking processes, proceeding');
end;
