"""AutoCAD-family discovery."""

import winreg

from revitdevtool_pytest.hosts import acad, find_executable
from revitdevtool_pytest.hosts.version import version_from_folder
from revitdevtool_pytest.hosts.winreg import subkey_count


def test_subkey_count_reads_subkeys_not_values():
    path = r"Software\RevitDevToolPyTestEnum"
    key = winreg.CreateKey(winreg.HKEY_CURRENT_USER, path)
    try:
        winreg.SetValueEx(key, "marker", 0, winreg.REG_SZ, "x")
        for name in ("A", "B"):
            child = winreg.CreateKey(key, name)
            winreg.CloseKey(child)
        assert subkey_count(key) == 2
    finally:
        for name in ("A", "B"):
            try:
                winreg.DeleteKey(key, name)
            except OSError:
                pass
        winreg.CloseKey(key)
        winreg.DeleteKey(winreg.HKEY_CURRENT_USER, path)


def test_version_token_is_the_last_folder_word():
    assert version_from_folder("AutoCAD 2026") == "2026"
    assert version_from_folder("Civil 3D 2027") == "2027"
    assert version_from_folder("AutoCAD 12026") is None
    assert version_from_folder("AutoCAD 2026.1") is None
    assert version_from_folder("AutoCAD 2021") is None


def test_filesystem_fallback_is_vanilla_autocad_only(monkeypatch):
    monkeypatch.setattr(acad, "_from_registry", lambda code, cfg, version: None)
    fallback = r"C:\Program Files\Autodesk\AutoCAD 2026\acad.exe"
    monkeypatch.setattr(acad, "_from_filesystem", lambda cfg, version: fallback)

    assert find_executable("autocad", "2026") == fallback
    assert find_executable("civil3d", "2026") is None
    assert find_executable("plant3d", "2027") is None
    assert find_executable("autocad", "2026.1") is None
