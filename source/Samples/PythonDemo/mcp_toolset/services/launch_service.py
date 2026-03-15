"""Service for discovering and launching Revit installations."""

from __future__ import annotations

import os

import anyio

from services.status_service import StatusService
from shared.responses import ToolError

_REGISTRY_BASE_PATH = r"SOFTWARE\Autodesk\Revit"
_REGISTRY_VALUE_NAMES = ("InstallationLocation", "InstallPath", "")


class LaunchService:
    def list_revit_installations(self) -> dict:
        installations = self._find_revit_installations()
        if not installations:
            return {"installations": [], "count": 0}
        return {"installations": installations, "count": len(installations)}

    async def launch_revit(
        self,
        file_path: str | None = None,
        version: str | None = None,
        language: str | None = None,
        timeout_seconds: int = 120,
    ) -> dict:
        self._validate_file_path(file_path)

        installations = self._find_revit_installations()
        if not installations:
            raise ToolError("No Revit installations found on this system.")

        selected = self._select_revit(installations, version)
        if not selected:
            available = ", ".join(i["year"] for i in installations)
            raise ToolError("Revit {} not found. Available versions: {}".format(version, available))

        cmd = self._build_launch_command(selected["path"], file_path, language)
        try:
            await anyio.open_process(cmd)
        except OSError as exc:
            raise ToolError("Failed to launch Revit: {}".format(exc)) from exc

        ready, status_response = await self._wait_for_revit_ready(timeout_seconds)
        result = {
            "revit_version": selected["year"],
            "revit_path": selected["path"],
            "file_opened": file_path,
            "revit_ready": ready,
        }

        if ready:
            result["message"] = "Revit {} is running and Revit API bridge is active.".format(selected["year"])
            if status_response:
                result["revit_status"] = status_response
        else:
            result["message"] = (
                "Revit {} was launched but did not respond within {} seconds. "
                "Ensure Revit is fully initialized and the direct bridge host is available."
            ).format(selected["year"], timeout_seconds)

        if file_path:
            result["worksharing_note"] = (
                "If this is a workshared (central) file, Revit will show its native dialog for creating a local copy. "
                "For programmatic control over worksharing options (detach, audit), use the open_document tool after Revit is ready."
            )
        return result

    @staticmethod
    async def _wait_for_revit_ready(timeout_seconds: int, poll_interval: int = 5) -> tuple[bool, dict | None]:
        status_service = StatusService()
        with anyio.move_on_after(timeout_seconds):
            while True:
                try:
                    response = status_service.get_status()
                    if response.revit_available:
                        return True, response.model_dump()
                except Exception:
                    pass
                await anyio.sleep(poll_interval)
        return False, None

    @staticmethod
    def _validate_file_path(file_path: str | None) -> None:
        if not file_path:
            return
        if not os.path.isfile(file_path):
            raise ToolError("File not found: {}".format(file_path))
        ext = os.path.splitext(file_path)[1].lower()
        if ext not in (".rvt", ".rfa", ".rte"):
            raise ToolError("Unsupported file type '{}'. Expected .rvt, .rfa, or .rte".format(ext))

    @staticmethod
    def _select_revit(installations: list[dict], version: str | None = None) -> dict | None:
        if not installations:
            return None
        if version:
            for inst in installations:
                if inst["year"] == str(version):
                    return inst
            return None
        return installations[0]

    @staticmethod
    def _build_launch_command(revit_path: str, file_path: str | None = None, language: str | None = None) -> list[str]:
        args = [revit_path]
        if language:
            args.extend(["/language", language])
        if file_path:
            args.append(file_path)
        return args

    @staticmethod
    def _find_revit_installations() -> list[dict]:
        found: dict[str, str] = {}
        _scan_registry(found)
        _scan_program_files(found)
        return [{"year": year, "path": path} for year, path in sorted(found.items(), key=lambda x: x[0], reverse=True)]


def _scan_registry(found: dict[str, str]) -> None:
    try:
        import winreg
    except ImportError:
        return

    for hive in (winreg.HKEY_LOCAL_MACHINE, winreg.HKEY_CURRENT_USER):
        try:
            base_key = winreg.OpenKey(hive, _REGISTRY_BASE_PATH)
        except OSError:
            continue
        try:
            _scan_registry_hive(winreg, base_key, found)
        finally:
            winreg.CloseKey(base_key)


def _scan_registry_hive(winreg, base_key, found: dict[str, str]) -> None:
    index = 0
    while True:
        try:
            subkey_name = winreg.EnumKey(base_key, index)
            index += 1
        except OSError:
            break

        year = _extract_year(subkey_name)
        if not year:
            continue

        subkey = winreg.OpenKey(base_key, subkey_name)
        try:
            _try_resolve_exe(winreg, subkey, year, found)
        finally:
            winreg.CloseKey(subkey)


def _extract_year(subkey_name: str) -> str | None:
    for token in subkey_name.split():
        if token.isdigit() and len(token) == 4:
            return token
    return None


def _try_resolve_exe(winreg, subkey, year: str, found: dict[str, str]) -> None:
    for value_name in _REGISTRY_VALUE_NAMES:
        try:
            value, _ = winreg.QueryValueEx(subkey, value_name)
        except OSError:
            continue
        if not value or not os.path.isdir(value):
            continue
        exe = os.path.join(value, "Revit.exe")
        if os.path.isfile(exe):
            found[year] = exe
        break


def _scan_program_files(found: dict[str, str]) -> None:
    program_files = os.environ.get("ProgramFiles", r"C:\Program Files")
    for year in range(2027, 2019, -1):
        year_str = str(year)
        if year_str in found:
            continue
        exe = os.path.join(program_files, "Autodesk", "Revit {}".format(year_str), "Revit.exe")
        if os.path.isfile(exe):
            found[year_str] = exe
