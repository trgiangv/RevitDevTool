"""Identify the heaviest families in the Revit model by instance/type count."""

from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from revit_dashboard.contracts.payload import HeavyFamily


def collect_heavy_families(
    rows: list[dict],
    top_n: int = 10,
) -> list[HeavyFamily]:
    """Rank families by a complexity proxy (instance count * type count).

    Since extracting real polygon counts requires tessellation (expensive),
    we use a heuristic: families with many instances **and** many types
    tend to be complex.  The ``estimated_complexity`` field is a synthetic
    score = ``instance_count * type_count * 10``.

    Args:
        rows: Element row dicts from the collector.
        top_n: How many families to return.
    """
    family_data: dict[str, dict] = {}

    for row in rows:
        family_name = row.get("family", "")
        if not family_name:
            continue

        if family_name not in family_data:
            family_data[family_name] = {
                "category": row.get("category", ""),
                "instance_count": 0,
                "types": set(),
            }

        entry = family_data[family_name]
        entry["instance_count"] += 1
        entry["types"].add(row.get("type", ""))

    result: list[HeavyFamily] = []
    for name, data in family_data.items():
        instance_count = data["instance_count"]
        type_count = len(data["types"])
        # Heuristic complexity score
        estimated_complexity = instance_count * max(type_count, 1) * 10

        result.append({
            "family_name": name,
            "category": data["category"],
            "instance_count": instance_count,
            "type_count": type_count,
            "estimated_complexity": estimated_complexity,
        })

    result.sort(key=lambda x: x["estimated_complexity"], reverse=True)
    return result[:top_n]
