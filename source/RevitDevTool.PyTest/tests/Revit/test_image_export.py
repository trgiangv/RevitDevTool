"""Image export tests — validates RevitImageExporter from RevitDevTool.Core.

All Revit API imports are lazy (inside function bodies).
Uses revit_doc fixture to ensure a document is open.
"""

import base64

import pytest


@pytest.fixture
def image_exporter():
    """Lazily import RevitImageExporter only in Revit-hosted tests."""
    from RevitDevTool.Core import RevitImageExporter

    return RevitImageExporter


@pytest.fixture
def image_settings():
    """Lazily import ImageExportSettings."""
    from RevitDevTool.Core import ImageExportSettings

    return ImageExportSettings


def test_export_active_view(revit_doc, image_exporter):
    """Export the active view and verify the result contains valid PNG data."""
    result = image_exporter.ExportActiveView()

    assert result is not None
    assert result.Base64Data
    assert result.ContentType == "image/png"
    assert result.ViewName
    assert result.FileSizeBytes > 0
    assert result.PixelSize == 1024

    raw = base64.b64decode(result.Base64Data)
    assert raw[:4] == b"\x89PNG", "Expected PNG magic bytes"
    print(f"Exported '{result.ViewName}': {result.FileSizeBytes:,} bytes")


def test_export_active_graphical_view(revit_doc, image_exporter):
    """Export the active graphical view (non-schedule)."""
    result = image_exporter.ExportActiveGraphicalView()

    assert result is not None
    assert result.ContentType == "image/png"
    assert result.FileSizeBytes > 0
    print(f"Graphical view '{result.ViewName}': {result.FileSizeBytes:,} bytes")


def test_export_view_by_name(revit_doc, image_exporter):
    """Export a view looked up by name."""
    from Autodesk.Revit.DB import FilteredElementCollector, View

    views = list(FilteredElementCollector(revit_doc).OfClass(View))
    exportable = [v for v in views if image_exporter.CanExport(v)]
    if not exportable:
        pytest.skip("No exportable views in document")

    target = exportable[0]
    result = image_exporter.ExportView[str](target.Name)

    assert result is not None
    assert result.ViewName == target.Name
    assert result.FileSizeBytes > 0
    print(f"By-name export '{result.ViewName}': {result.FileSizeBytes:,} bytes")


def test_export_view_by_element_id(revit_doc, image_exporter):
    """Export a view looked up by ElementId."""
    from Autodesk.Revit.DB import ElementId

    view = revit_doc.ActiveView
    if view is None:
        pytest.skip("No active view")

    result = image_exporter.ExportView[ElementId](view.Id)

    assert result is not None
    assert result.ViewId == view.Id
    assert result.FileSizeBytes > 0
    print(f"By-id export '{result.ViewName}' (id={view.Id}): {result.FileSizeBytes:,} bytes")


def test_export_view_by_object(revit_doc, image_exporter):
    """Export a view by passing the View object directly."""
    from Autodesk.Revit.DB import View

    view = revit_doc.ActiveView
    if view is None:
        pytest.skip("No active view")

    result = image_exporter.ExportView[View](view)

    assert result is not None
    assert result.FileSizeBytes > 0
    print(f"By-object export '{result.ViewName}': {result.FileSizeBytes:,} bytes")


def test_export_with_custom_pixel_size(revit_doc, image_exporter, image_settings):
    """Export with higher resolution settings."""
    settings = image_settings()
    settings.PixelSize = 2048

    result = image_exporter.ExportActiveView(settings)

    assert result is not None
    assert result.PixelSize == 2048
    assert result.FileSizeBytes > 0
    print(f"Custom export '{result.ViewName}': {result.FileSizeBytes:,} bytes @ {result.PixelSize}px")


def test_export_view_to_file(revit_doc, image_exporter, tmp_path):
    """Export active view to a file on disk."""
    import os

    output_path = str(tmp_path / "test_export.png")
    result_path = image_exporter.ExportActiveViewToFile(output_path)

    assert result_path is not None
    assert os.path.isfile(result_path)

    file_size = os.path.getsize(result_path)
    assert file_size > 0
    print(f"File export: {result_path} ({file_size:,} bytes)")


def test_export_multiple_views(revit_doc, image_exporter):
    """Export multiple views in batch."""
    from Autodesk.Revit.DB import FilteredElementCollector, View

    views = list(FilteredElementCollector(revit_doc).OfClass(View))
    exportable = [v for v in views if image_exporter.CanExport(v)]
    if len(exportable) < 2:
        pytest.skip("Need at least 2 exportable views")

    view_ids = [v.Id for v in exportable[:3]]
    results = image_exporter.ExportViews(view_ids)

    assert len(results) > 0
    assert len(results) <= 3
    for r in results:
        assert r.FileSizeBytes > 0
        print(f"  Batch: '{r.ViewName}' — {r.FileSizeBytes:,} bytes")


def test_export_nonexistent_view_raises(revit_doc, image_exporter):
    """Requesting a view that doesn't exist should raise."""
    with pytest.raises(Exception):
        image_exporter.ExportView[str]("__nonexistent_view_name_12345__")


def test_export_result_base64_roundtrip(revit_doc, image_exporter):
    """Verify base64 data can be decoded back to valid image bytes."""
    result = image_exporter.ExportActiveView()

    raw = base64.b64decode(result.Base64Data)
    assert len(raw) == result.FileSizeBytes
    re_encoded = base64.b64encode(raw).decode("utf-8")
    assert re_encoded == result.Base64Data
    print(f"Base64 roundtrip OK: {len(raw):,} bytes")


def test_export_all_views_to_downloads(revit_doc, image_exporter):
    """Export all exportable views as PNG files to ~/Downloads/RevitDevTool/Images."""
    import os
    from Autodesk.Revit.DB import FilteredElementCollector, View

    output_dir = os.path.join(os.path.expanduser("~"), "Downloads", "RevitDevTool", "Images")
    os.makedirs(output_dir, exist_ok=True)

    views = list(FilteredElementCollector(revit_doc).OfClass(View))
    exportable = [v for v in views if image_exporter.CanExport(v)]
    if not exportable:
        pytest.skip("No exportable views in document")

    exported = []
    for view in exportable:
        try:
            result_path = image_exporter.ExportViewToFile(view, output_dir)
            size = os.path.getsize(result_path)
            exported.append((view.Name, result_path, size))
            print(f"Exported: {view.Name} → {result_path} ({size:,} bytes)")
        except Exception as exc:
            print(f"Skipped: {view.Name} — {exc.args[0]}")

    assert len(exported) > 0, "At least one view should export successfully"
    total_bytes = sum(s for _, _, s in exported)
    print(f"\n{len(exported)}/{len(exportable)} views exported to {output_dir}")
    print(f"Total size: {total_bytes:,.0f} bytes")
    print(f"Output folder: {output_dir}")
