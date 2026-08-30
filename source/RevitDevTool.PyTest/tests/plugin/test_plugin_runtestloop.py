from unittest.mock import MagicMock

from revitdevtool_pytest import plugin


def test_runtestloop_disable_defers_to_pytest(monkeypatch):
    monkeypatch.setenv("REVITDEVTOOL_PYTEST_DISABLE", "1")
    session = MagicMock()
    session.config.option.collectonly = False
    assert plugin.pytest_runtestloop(session) is None


def test_runtestloop_collect_only_is_noop(monkeypatch):
    monkeypatch.delenv("REVITDEVTOOL_PYTEST_DISABLE", raising=False)
    session = MagicMock()
    session.config.option.collectonly = True
    assert plugin.pytest_runtestloop(session) is True
