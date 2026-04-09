"""Smoke test — minimal test to verify RevitDevTool.PyTest protocol works."""


def test_revit_application_available():
    """Verify __revit__ is injected and Application is accessible."""
    app = __revit__.Application  # noqa: F821
    version = app.VersionNumber
    assert version is not None
    assert len(version) == 4  # e.g. "2025"
    print(f"Revit {app.VersionName} (build {app.VersionBuild})")


def test_uiapplication_available():
    """Verify UIApplication is accessible."""
    uiapp = __revit__  # noqa: F821
    assert uiapp is not None
    assert uiapp.Application is not None
    print(f"UIApplication OK — version {uiapp.Application.VersionNumber}")
