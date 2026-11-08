"""Civil 3D-specific API tests — Alignments, Surfaces, Corridors, Sites.

These tests require Civil 3D (not plain AutoCAD). They use the
``Autodesk.Civil.DatabaseServices`` namespace from ``AeccDbMgd.dll``.
"""

import pytest


def _civil_db(acad_db):
    """Get the CivilDocument wrapper for the active database."""
    import clr
    clr.AddReference("AeccDbMgd")
    from Autodesk.Civil.DatabaseServices import CivilDocument

    return CivilDocument.GetCivilDocument(acad_db)


# ---------------------------------------------------------------------------
# Alignments
# ---------------------------------------------------------------------------

def test_alignment_collection(acad_db, acad_transaction):
    """Enumerate all Alignments in the document."""
    from Autodesk.AutoCAD.DatabaseServices import OpenMode

    civil_doc = _civil_db(acad_db)
    alignment_ids = civil_doc.GetAlignmentIds()
    count = alignment_ids.Count

    print(f"Total alignments: {count}")
    for i in range(min(count, 20)):
        alignment = acad_transaction.GetObject(alignment_ids[i], OpenMode.ForRead)
        print(f"  [{i}] {alignment.Name} — Length: {alignment.Length:.2f}")
    assert count >= 0


def test_alignment_details(acad_db, acad_transaction):
    """Read detailed properties of the first alignment."""
    from Autodesk.AutoCAD.DatabaseServices import OpenMode

    civil_doc = _civil_db(acad_db)
    alignment_ids = civil_doc.GetAlignmentIds()

    if alignment_ids.Count == 0:
        pytest.skip("No alignments in document")

    alignment = acad_transaction.GetObject(alignment_ids[0], OpenMode.ForRead)
    print(f"Alignment: {alignment.Name}")
    print(f"  Length: {alignment.Length:.4f}")
    print(f"  Start station: {alignment.StartingStation:.4f}")
    print(f"  End station: {alignment.EndingStation:.4f}")
    print(f"  Style: {alignment.StyleName}")
    assert alignment.Length > 0


# ---------------------------------------------------------------------------
# Surfaces
# ---------------------------------------------------------------------------

def test_surface_collection(acad_db, acad_transaction):
    """Enumerate all Surfaces in the document."""
    from Autodesk.AutoCAD.DatabaseServices import OpenMode

    civil_doc = _civil_db(acad_db)
    surface_ids = civil_doc.GetSurfaceIds()
    count = surface_ids.Count

    print(f"Total surfaces: {count}")
    for i in range(min(count, 20)):
        surface = acad_transaction.GetObject(surface_ids[i], OpenMode.ForRead)
        print(f"  [{i}] {surface.Name}")
    assert count >= 0


def test_surface_details(acad_db, acad_transaction):
    """Read properties of the first surface."""
    from Autodesk.AutoCAD.DatabaseServices import OpenMode

    civil_doc = _civil_db(acad_db)
    surface_ids = civil_doc.GetSurfaceIds()

    if surface_ids.Count == 0:
        pytest.skip("No surfaces in document")

    surface = acad_transaction.GetObject(surface_ids[0], OpenMode.ForRead)
    print(f"Surface: {surface.Name}")
    print(f"  Style: {surface.StyleName}")

    bounds = surface.GetBoundaries()
    print(f"  Boundaries: {bounds.Count}")
    assert surface.Name is not None


# ---------------------------------------------------------------------------
# Sites
# ---------------------------------------------------------------------------

def test_site_collection(acad_db, acad_transaction):
    """Enumerate all Sites in the document."""
    from Autodesk.AutoCAD.DatabaseServices import OpenMode

    civil_doc = _civil_db(acad_db)
    site_ids = civil_doc.GetSiteIds()
    count = site_ids.Count

    print(f"Total sites: {count}")
    for i in range(min(count, 20)):
        site = acad_transaction.GetObject(site_ids[i], OpenMode.ForRead)
        print(f"  [{i}] {site.Name}")
    assert count >= 0


# ---------------------------------------------------------------------------
# Pipe Networks
# ---------------------------------------------------------------------------

def test_pipe_network_collection(acad_db, acad_transaction):
    """Enumerate all Pipe Networks in the document."""
    from Autodesk.AutoCAD.DatabaseServices import OpenMode

    civil_doc = _civil_db(acad_db)
    network_ids = civil_doc.GetPipeNetworkIds()
    count = network_ids.Count

    print(f"Total pipe networks: {count}")
    for i in range(min(count, 20)):
        network = acad_transaction.GetObject(network_ids[i], OpenMode.ForRead)
        print(f"  [{i}] {network.Name}")
    assert count >= 0


# ---------------------------------------------------------------------------
# Point Groups
# ---------------------------------------------------------------------------

def test_point_group_collection(acad_db, acad_transaction):
    """Enumerate all Point Groups in the document."""
    from Autodesk.AutoCAD.DatabaseServices import OpenMode

    civil_doc = _civil_db(acad_db)
    pg_ids = civil_doc.GetPointGroupIds()
    count = pg_ids.Count

    print(f"Total point groups: {count}")
    for i in range(min(count, 20)):
        pg = acad_transaction.GetObject(pg_ids[i], OpenMode.ForRead)
        print(f"  [{i}] {pg.Name}")
    assert count >= 0
