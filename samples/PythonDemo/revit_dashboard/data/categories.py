# -*- coding: utf-8 -*-
"""BuiltInCategory registry.

Groups are plain string keys. Callers can pass a subset
(for example STRUCTURAL and MEP) to ``get_model_categories``.
"""

from __future__ import division, print_function

from collections import OrderedDict

from Autodesk.Revit import DB


class CategoryGroup(object):
    """Logical discipline grouping keys."""

    STRUCTURAL = "STRUCTURAL"
    ARCHITECTURAL = "ARCHITECTURAL"
    MEP_MECHANICAL = "MEP_MECHANICAL"
    MEP_ELECTRICAL = "MEP_ELECTRICAL"
    MEP_PLUMBING = "MEP_PLUMBING"
    MEP_PIPING = "MEP_PIPING"
    SITE = "SITE"
    ROOMS_SPACES = "ROOMS_SPACES"
    GENERIC = "GENERIC"


# Insertion order is the collection order when no groups are requested.
CATEGORY_REGISTRY = OrderedDict((  # type: OrderedDict[str, list[DB.BuiltInCategory]]
    (CategoryGroup.STRUCTURAL, [
        DB.BuiltInCategory.OST_StructuralColumns,
        DB.BuiltInCategory.OST_StructuralFraming,
        DB.BuiltInCategory.OST_StructuralFoundation,
        DB.BuiltInCategory.OST_StructuralTruss,
        DB.BuiltInCategory.OST_StructuralStiffener,
        DB.BuiltInCategory.OST_Rebar,
    ]),
    (CategoryGroup.ARCHITECTURAL, [
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
    ]),
    (CategoryGroup.MEP_MECHANICAL, [
        DB.BuiltInCategory.OST_DuctCurves,
        DB.BuiltInCategory.OST_DuctFitting,
        DB.BuiltInCategory.OST_DuctAccessory,
        DB.BuiltInCategory.OST_DuctTerminal,
        DB.BuiltInCategory.OST_DuctInsulations,
        DB.BuiltInCategory.OST_DuctLinings,
        DB.BuiltInCategory.OST_FlexDuctCurves,
        DB.BuiltInCategory.OST_MechanicalEquipment,
    ]),
    (CategoryGroup.MEP_ELECTRICAL, [
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
    ]),
    (CategoryGroup.MEP_PLUMBING, [
        DB.BuiltInCategory.OST_PlumbingFixtures,
        DB.BuiltInCategory.OST_Sprinklers,
    ]),
    (CategoryGroup.MEP_PIPING, [
        DB.BuiltInCategory.OST_PipeCurves,
        DB.BuiltInCategory.OST_PipeFitting,
        DB.BuiltInCategory.OST_PipeAccessory,
        DB.BuiltInCategory.OST_PipeInsulations,
        DB.BuiltInCategory.OST_FlexPipeCurves,
    ]),
    (CategoryGroup.SITE, [
        DB.BuiltInCategory.OST_Topography,
        DB.BuiltInCategory.OST_Parking,
        DB.BuiltInCategory.OST_Planting,
        DB.BuiltInCategory.OST_Site,
    ]),
    (CategoryGroup.ROOMS_SPACES, [
        DB.BuiltInCategory.OST_Rooms,
        DB.BuiltInCategory.OST_MEPSpaces,
        DB.BuiltInCategory.OST_Areas,
    ]),
    (CategoryGroup.GENERIC, [
        DB.BuiltInCategory.OST_MassFloor,
        DB.BuiltInCategory.OST_Parts,
    ]),
))


def get_model_categories(groups=None):
    # type: (list[str] | None) -> list[DB.BuiltInCategory]
    """Return a flat list of ``BuiltInCategory`` for the requested groups.

    ``None`` or an empty list includes every registered group.
    """
    targets = groups or list(CATEGORY_REGISTRY.keys())
    result = []  # type: list[DB.BuiltInCategory]
    for group in targets:
        result.extend(CATEGORY_REGISTRY[group])
    return result


def get_group_names():
    # type: () -> list[str]
    """Return all available group names."""
    return list(CATEGORY_REGISTRY.keys())
