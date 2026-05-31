"""Test-side schedule field mapping helpers using Revit API only."""

from tests.schedule.model import element_id_value


def get_field_order(schedule):
    definition = schedule.Definition
    try:
        return list(definition.GetFieldOrder() or [])
    except Exception:
        return []


def get_schedule_field(schedule, field_id):
    definition = schedule.Definition
    if field_id is None:
        return None
    try:
        return definition.GetField(field_id)
    except Exception:
        return None


def normalize_text(value):
    if value is None:
        return None
    return value.strip().lower()


def serialize_field_identity(schedule, field, field_index):
    try:
        parameter_id = element_id_value(field.ParameterId)
    except Exception:
        parameter_id = None
    try:
        heading = field.ColumnHeading
    except Exception:
        heading = None
    try:
        name = field.GetName(schedule.Document)
    except Exception:
        name = heading
    return {
        "field": field,
        "field_index": field_index,
        "parameter_id": parameter_id,
        "heading": heading,
        "name": name,
        "fallback_key": (
            normalize_text(name),
            normalize_text(heading),
            field_index,
        ),
    }


def iter_schedule_field_identities(schedule):
    identities = []
    for field_index, field_id in enumerate(get_field_order(schedule)):
        field = get_schedule_field(schedule, field_id)
        if field is None:
            continue
        identities.append(serialize_field_identity(schedule, field, field_index))
    return identities


class FieldMappingService(object):
    def __init__(self, source_schedule, target_schedule):
        self.source_schedule = source_schedule
        self.target_schedule = target_schedule

    def build_mapping(self):
        source_fields = iter_schedule_field_identities(self.source_schedule)
        target_fields = iter_schedule_field_identities(self.target_schedule)
        target_by_parameter = self._group_target_fields_by_parameter(target_fields)
        target_by_fallback = self._group_target_fields_by_fallback(target_fields)
        used_target_indexes = set()
        matches = []
        unmatched_source = []

        for source_field in source_fields:
            target_field = self._match_target_field(
                source_field,
                target_by_parameter,
                target_by_fallback,
                used_target_indexes,
            )
            if target_field is None:
                unmatched_source.append(source_field)
                continue

            used_target_indexes.add(target_field["field_index"])
            matches.append({"source": source_field, "target": target_field})

        unmatched_target = [
            target_field
            for target_field in target_fields
            if target_field["field_index"] not in used_target_indexes
        ]
        return {
            "matches": matches,
            "unmatched_source": unmatched_source,
            "unmatched_target": unmatched_target,
        }

    def _group_target_fields_by_parameter(self, target_fields):
        grouped = {}
        for target_field in target_fields:
            parameter_id = target_field["parameter_id"]
            if parameter_id is None:
                continue
            grouped.setdefault(parameter_id, []).append(target_field)
        return grouped

    def _group_target_fields_by_fallback(self, target_fields):
        grouped = {}
        for target_field in target_fields:
            fallback_key = target_field["fallback_key"]
            if fallback_key is None:
                continue
            grouped.setdefault(fallback_key, []).append(target_field)
        return grouped

    def _match_target_field(
        self,
        source_field,
        target_by_parameter,
        target_by_fallback,
        used_target_indexes,
    ):
        parameter_id = source_field["parameter_id"]
        if parameter_id is not None:
            target_field = self._first_unused(
                target_by_parameter.get(parameter_id, []),
                used_target_indexes,
            )
            if target_field is not None:
                return target_field

        return self._first_unused(
            target_by_fallback.get(source_field["fallback_key"], []),
            used_target_indexes,
        )

    def _first_unused(self, candidates, used_target_indexes):
        for candidate in candidates:
            if candidate["field_index"] in used_target_indexes:
                continue
            return candidate
        return None
