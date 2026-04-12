"""Deterministic selection tests for discovered Revit instances."""

from __future__ import annotations

import random

from revitdevtool_pytest.discovery import RevitInstance, select_instance


def test_select_instance_prefers_highest_version_then_pid():
    instances = [
        RevitInstance(pipe_name="Revit_2024_100", version=2024, process_id=100),
        RevitInstance(pipe_name="Revit_2025_101", version=2025, process_id=101),
        RevitInstance(pipe_name="Revit_2025_222", version=2025, process_id=222),
    ]

    selected = select_instance(instances)
    assert selected is not None
    assert selected.version == 2025
    assert selected.process_id == 222


def test_select_instance_for_specific_version_prefers_highest_pid():
    instances = [
        RevitInstance(pipe_name="Revit_2025_200", version=2025, process_id=200),
        RevitInstance(pipe_name="Revit_2025_150", version=2025, process_id=150),
        RevitInstance(pipe_name="Revit_2024_300", version=2024, process_id=300),
    ]

    selected = select_instance(instances, version=2025)
    assert selected is not None
    assert selected.version == 2025
    assert selected.process_id == 200


def test_select_instance_is_order_independent_under_stress():
    baseline = [
        RevitInstance(pipe_name=f"Revit_2025_{pid}", version=2025, process_id=pid)
        for pid in range(100, 140)
    ] + [
        RevitInstance(pipe_name=f"Revit_2024_{pid}", version=2024, process_id=pid)
        for pid in range(500, 520)
    ]

    expected = select_instance(baseline)
    assert expected is not None

    for seed in range(30):
        shuffled = baseline.copy()
        random.Random(seed).shuffle(shuffled)
        actual = select_instance(shuffled)
        assert actual == expected


def test_select_instance_large_stress_matrix():
    # Stress with many versions and pids to guard against accidental
    # regression when selection logic is refactored.
    baseline = [
        RevitInstance(pipe_name=f"Revit_{version}_{pid}", version=version, process_id=pid)
        for version in range(2020, 2027)
        for pid in range(100, 200)
    ]
    expected_latest = max(baseline, key=lambda i: (i.version, i.process_id))
    expected_2024 = max((i for i in baseline if i.version == 2024), key=lambda i: i.process_id)

    for seed in range(100):
        shuffled = baseline.copy()
        random.Random(seed).shuffle(shuffled)
        assert select_instance(shuffled) == expected_latest
        assert select_instance(shuffled, version=2024) == expected_2024
