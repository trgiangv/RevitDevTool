{
  App / version registry — keep in sync with [Components] and PackageContents.xml
  APP_COUNT only covers Autodesk products used for XML Description matching.
}

const
  APP_COUNT     = 2;
  VERSION_COUNT = 6;   // 2022 .. 2027

{
  Per-app accessors (Autodesk)
}

function AppName(I: Integer): string;
begin
  case I of
    0: Result := 'Revit';
    1: Result := 'AutoCAD';
  else
    Result := '';
  end;
end;

function AppProcessName(I: Integer): string;
begin
  case I of
    0: Result := 'Revit.exe';
    1: Result := 'acad.exe';
  else
    Result := '';
  end;
end;

{ Must match Name= prefix in Components list (lowercase, no spaces) }
function AppCompName(I: Integer): string;
begin
  case I of
    0: Result := 'revit';
    1: Result := 'autocad';
  else
    Result := '';
  end;
end;

{ Per-version accessor }

function VersionYear(I: Integer): string;
begin
  case I of
    0: Result := '2022';
    1: Result := '2023';
    2: Result := '2024';
    3: Result := '2025';
    4: Result := '2026';
    5: Result := '2027';
  else
    Result := '';
  end;
end;

{ Entry helpers: component path e.g. revit\2024 }

function EntryCompPath(AppIdx, VerIdx: Integer): string;
begin
  Result := AppCompName(AppIdx) + '\' + VersionYear(VerIdx);
end;

{ Description attribute written in PackageContents.xml, e.g. Revit 2024 }
function EntryXmlDescription(AppIdx, VerIdx: Integer): string;
begin
  Result := AppName(AppIdx) + ' ' + VersionYear(VerIdx);
end;

function EntryIsSelected(AppIdx, VerIdx: Integer): Boolean;
begin
  Result := WizardIsComponentSelected(EntryCompPath(AppIdx, VerIdx));
end;
