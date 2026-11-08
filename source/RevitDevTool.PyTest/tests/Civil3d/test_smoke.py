"""Smoke test — verify Civil 3D is running and the pytest bridge works."""


def test_application_accessible(acad_app):
    """Verify the AutoCAD Application static class is reachable."""
    assert acad_app is not None
    print(f"Civil 3D Application OK")


def test_version_info(acad_app):
    """Read Civil 3D / AutoCAD version info."""
    from Autodesk.AutoCAD.ApplicationServices.Core import Application

    info_str = Application.Version.ToString()
    print(f"AutoCAD Version: {info_str}")
    assert Application.Version.Major > 0


def test_document_manager_exists(acad_app):
    """DocumentManager is available."""
    dm = acad_app.DocumentManager
    assert dm is not None
    count = dm.Count
    print(f"Open documents: {count}")
    assert count >= 0
