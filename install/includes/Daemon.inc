{
  DevTools.Daemon lifecycle — auto-start registry, launch after install, cleanup on uninstall.
  Registry key: HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run\DevToolsDaemon
}

const
  DAEMON_EXE       = 'DevTools.Daemon.exe';
  DAEMON_RUN_KEY   = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run';
  DAEMON_RUN_VALUE = 'DevToolsDaemon';

function DaemonExePath: string;
begin
  Result := ExpandConstant('{app}\Contents\') + DAEMON_EXE;
end;

procedure RegisterDaemonAutoStart;
begin
  if not RegWriteStringValue(HKCU, DAEMON_RUN_KEY, DAEMON_RUN_VALUE,
    '"' + DaemonExePath + '"') then
    Log('Daemon: WARNING — could not write Run key');
end;

procedure UnregisterDaemonAutoStart;
begin
  if not RegDeleteValue(HKCU, DAEMON_RUN_KEY, DAEMON_RUN_VALUE) then
    Log('Daemon: Run key not found or already removed');
end;

procedure LaunchDaemonAfterInstall;
var
  ResultCode: Integer;
begin
  if not FileExists(DaemonExePath) then
  begin
    Log('Daemon: exe not found at ' + DaemonExePath);
    Exit;
  end;

  if not Exec(DaemonExePath, '', '', SW_HIDE, ewNoWait, ResultCode) then
    Log('Daemon: failed to launch (error ' + IntToStr(ResultCode) + ')')
  else
    Log('Daemon: launched successfully');
end;
