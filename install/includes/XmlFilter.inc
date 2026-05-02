{
  PackageContents.xml — prune <Components> not selected in the wizard.
}

function XmlComponentIsSelected(const Description: string): Boolean;
var
  AppIdx, VerIdx: Integer;
begin
  for AppIdx := 0 to APP_COUNT - 1 do
    for VerIdx := 0 to VERSION_COUNT - 1 do
      if Description = EntryXmlDescription(AppIdx, VerIdx) then
      begin
        Result := EntryIsSelected(AppIdx, VerIdx);
        Exit;
      end;

  Log('XmlComponentIsSelected: unrecognised description, keeping — "' + Description + '"');
  Result := True;
end;

procedure FilterPackageContentsXml;
var
  XmlPath: string;
  XmlDoc, NodeList, Node: Variant;
  I, RemovedCount, NodeCount: Integer;
  Description: string;
begin
  XmlPath := ExpandConstant('{app}\PackageContents.xml');

  if not FileExists(XmlPath) then
  begin
    Log('FilterPackageContentsXml: file not found — ' + XmlPath);
    Exit;
  end;

  XmlDoc := CreateOleObject('MSXML2.DOMDocument');
  XmlDoc.async := False;
  XmlDoc.load(XmlPath);

  if XmlDoc.parseError.errorCode <> 0 then
  begin
    Log('FilterPackageContentsXml: XML parse error — ' + XmlDoc.parseError.reason);
    Exit;
  end;

  NodeList     := XmlDoc.selectNodes('//Components');
  RemovedCount := 0;
  NodeCount := NodeList.length;

  Log('FilterPackageContentsXml: ' + IntToStr(NodeCount) + ' node(s) found');

  for I := NodeCount - 1 downto 0 do
  begin
    Node        := NodeList.item[I];
    Description := Node.getAttribute('Description');

    if XmlComponentIsSelected(Description) then
      Log('Kept:    ' + Description)
    else
    begin
      Node.parentNode.removeChild(Node);
      Inc(RemovedCount);
      Log('Removed: ' + Description);
    end;
  end;

  XmlDoc.save(XmlPath);
  Log('FilterPackageContentsXml: done, ' + IntToStr(RemovedCount) + ' node(s) removed');
end;
