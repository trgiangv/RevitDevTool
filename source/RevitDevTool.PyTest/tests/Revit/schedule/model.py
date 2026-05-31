"""Basic Revit schedule access helpers for tests."""


def get_schedule(doc, schedule_id):
    from Autodesk.Revit import DB

    schedule = doc.GetElement(DB.ElementId(schedule_id))
    assert schedule is not None
    return schedule


def get_schedule_pair(doc, case):
    return get_schedule(doc, case["source_id"]), get_schedule(doc, case["target_id"])


def get_section(schedule, section_name):
    from Autodesk.Revit import DB

    table_data = schedule.GetTableData()
    assert table_data is not None

    section_type = getattr(DB.SectionType, section_name, None)
    assert section_type is not None
    section = table_data.GetSectionData(section_type)
    assert section is not None
    return section


def table_section_names(schedule):
    from Autodesk.Revit import DB

    table_data = schedule.GetTableData()
    assert table_data is not None

    section_names = []
    for section_name in ("Title", "Header", "Body", "Summary", "Footer"):
        section_type = getattr(DB.SectionType, section_name, None)
        if section_type is None:
            continue
        try:
            section = table_data.GetSectionData(section_type)
        except Exception:
            continue
        if section is not None:
            section_names.append(section_name)
    return section_names


def element_id_value(element_id):
    if element_id is None:
        return None
    if hasattr(element_id, "IntegerValue"):
        return element_id.IntegerValue
    return getattr(element_id, "Value", None)