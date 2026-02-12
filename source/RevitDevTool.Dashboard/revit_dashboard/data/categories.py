"""Data-driven Revit category registry.

All category filtering is driven by ``BuiltInCategory`` enum values — zero
hardcoded category name strings.  Groups can be combined or filtered by the
caller (e.g. the frontend requests only STRUCTURAL + MEP).
"""

from __future__ import annotations

from enum import Enum, auto

import Autodesk.Revit.DB as DB


class CategoryGroup(Enum):
    """Logical discipline grouping of Revit built-in categories."""

    STRUCTURAL = auto()
    ARCHITECTURAL = auto()
    MEP_MECHANICAL = auto()
    MEP_ELECTRICAL = auto()
    MEP_PLUMBING = auto()
    MEP_PIPING = auto()
    SITE = auto()
    ROOMS_SPACES = auto()
    GENERIC = auto()


# Registry — maps group → list of BuiltInCategory enum values.
CATEGORY_REGISTRY: dict[CategoryGroup, list[DB.BuiltInCategory]] = {
    CategoryGroup.STRUCTURAL: [
        DB.BuiltInCategory.OST_StructuralColumns,
        DB.BuiltInCategory.OST_StructuralFraming,
        DB.BuiltInCategory.OST_StructuralFoundation,
        DB.BuiltInCategory.OST_StructuralTruss,
        DB.BuiltInCategory.OST_StructuralStiffener,
        DB.BuiltInCategory.OST_Rebar,
    ],
    CategoryGroup.ARCHITECTURAL: [
        DB.BuiltInCategory.OST_Walls,
        DB.BuiltInCategory.OST_Doors,
        DB.BuiltInCategory.OST_Windows,
        DB.BuiltInCategory.OST_Floors,
        DB.BuiltInCategory.OST_Ceilings,
        DB.BuiltInCategory.OST_Roofs,
        DB.BuiltInCategory.OST_Stairs,
        DB.BuiltInCategory.OST_StairsRailing,
        DB.BuiltInCategory.OST_Ramps,
        DB.BuiltInCategory.OST_Columns,
        DB.BuiltInCategory.OST_CurtainWallPanels,
        DB.BuiltInCategory.OST_CurtainWallMullions,
        DB.BuiltInCategory.OST_Casework,
        DB.BuiltInCategory.OST_Furniture,
        DB.BuiltInCategory.OST_FurnitureSystems,
        DB.BuiltInCategory.OST_SpecialityEquipment,
        DB.BuiltInCategory.OST_Entourage,
        DB.BuiltInCategory.OST_GenericModel,
    ],
    CategoryGroup.MEP_MECHANICAL: [
        DB.BuiltInCategory.OST_DuctCurves,
        DB.BuiltInCategory.OST_DuctFitting,
        DB.BuiltInCategory.OST_DuctAccessory,
        DB.BuiltInCategory.OST_DuctTerminal,
        DB.BuiltInCategory.OST_DuctInsulations,
        DB.BuiltInCategory.OST_DuctLinings,
        DB.BuiltInCategory.OST_FlexDuctCurves,
        DB.BuiltInCategory.OST_MechanicalEquipment,
    ],
    CategoryGroup.MEP_ELECTRICAL: [
        DB.BuiltInCategory.OST_ElectricalEquipment,
        DB.BuiltInCategory.OST_ElectricalFixtures,
        DB.BuiltInCategory.OST_CableTray,
        DB.BuiltInCategory.OST_CableTrayFitting,
        DB.BuiltInCategory.OST_Conduit,
        DB.BuiltInCategory.OST_ConduitFitting,
        DB.BuiltInCategory.OST_LightingFixtures,
        DB.BuiltInCategory.OST_LightingDevices,
        DB.BuiltInCategory.OST_CommunicationDevices,
        DB.BuiltInCategory.OST_DataDevices,
        DB.BuiltInCategory.OST_FireAlarmDevices,
        DB.BuiltInCategory.OST_SecurityDevices,
        DB.BuiltInCategory.OST_NurseCallDevices,
        DB.BuiltInCategory.OST_TelephoneDevices,
    ],
    CategoryGroup.MEP_PLUMBING: [
        DB.BuiltInCategory.OST_PlumbingFixtures,
        DB.BuiltInCategory.OST_Sprinklers,
    ],
    CategoryGroup.MEP_PIPING: [
        DB.BuiltInCategory.OST_PipeCurves,
        DB.BuiltInCategory.OST_PipeFitting,
        DB.BuiltInCategory.OST_PipeAccessory,
        DB.BuiltInCategory.OST_PipeInsulations,
        DB.BuiltInCategory.OST_FlexPipeCurves,
    ],
    CategoryGroup.SITE: [
        DB.BuiltInCategory.OST_Topography,
        DB.BuiltInCategory.OST_Parking,
        DB.BuiltInCategory.OST_Planting,
        DB.BuiltInCategory.OST_Site,
    ],
    CategoryGroup.ROOMS_SPACES: [
        DB.BuiltInCategory.OST_Rooms,
        DB.BuiltInCategory.OST_MEPSpaces,
        DB.BuiltInCategory.OST_Areas,
    ],
    CategoryGroup.GENERIC: [
        DB.BuiltInCategory.OST_MassFloor,
        DB.BuiltInCategory.OST_Parts,
    ],
}


def get_model_categories(
    groups: list[CategoryGroup] | None = None,
) -> list[DB.BuiltInCategory]:
    """Return a flat list of ``BuiltInCategory`` for the requested groups.

    If *groups* is ``None`` every registered group is included.
    """
    targets = groups or list(CategoryGroup)
    result: list[DB.BuiltInCategory] = []
    for group in targets:
        result.extend(CATEGORY_REGISTRY.get(group, []))
    return result


def get_group_names() -> list[str]:
    """Return all available group names (for frontend display)."""
    return [g.name for g in CategoryGroup]
