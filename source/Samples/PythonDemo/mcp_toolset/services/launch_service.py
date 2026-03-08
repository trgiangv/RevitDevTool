"""Service for discovering and launching Revit installations."""

from __future__ import annotations

import os
import subprocess
import time

import anyio

from shared.responses import ToolError


class LaunchService:
    def __init__(self, status_service):
        self._status_service = status_service

    def list_revit_installations(self) -> dict:
        installations = self._find_revit_installations()
        if not installations:
            return {"installations": [], "count": 0}
        return {"installations": installations, "count": len(installations)}

    async def launch_revit(self, file_path=None, version=None, language=None, timeout=120) -> dict:
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
            subprocess.Popen(cmd)
        except OSError as exc:
            raise ToolError("Failed to launch Revit: {}".format(exc)) from exc

        ready, status_response = await self._wait_for_revit_ready(timeout)
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
            ).format(selected["year"], timeout)

        if file_path:
            result["worksharing_note"] = (
                "If this is a workshared (central) file, Revit will show its native dialog for creating a local copy. "
                "For programmatic control over worksharing options (detach, audit), use the open_document tool after Revit is ready."
            )
        return result

    async def _wait_for_revit_ready(self, timeout: int, poll_interval: int = 5):
        start = time.time()
        while time.time() - start < timeout:
            try:
                response = self._status_service.get_status()
                if isinstance(response, dict) and response.get("revit_available") is True:
                    return True, response
            except Exception:
                pass
            await anyio.sleep(poll_interval)
        return False, None

    @staticmethod
    def _validate_file_path(file_path):
        if not file_path:
            return
        if not os.path.isfile(file_path):
            raise ToolError("File not found: {}".format(file_path))
        ext = os.path.splitext(file_path)[1].lower()
        if ext not in (".rvt", ".rfa", ".rte"):
            raise ToolError("Unsupported file type '{}'. Expected .rvt, .rfa, or .rte".format(ext))

    @staticmethod
    def _select_revit(installations, version=None):
        if not installations:
            return None
        if version:
            for inst in installations:
                if inst["year"] == str(version):
                    return inst
            return None
        return installations[0]

    @staticmethod
    def _build_launch_command(revit_path, file_path=None, language=None):
        args = [revit_path]
        if language:
            args.extend(["/language", language])
        if file_path:
            args.append(file_path)
        return args

    @staticmethod
    def _find_revit_installations():
        found = {}
        try:
            import winreg

            base_key_path = r"SOFTWARE\Autodesk\Revit"
            for hive in (winreg.HKEY_LOCAL_MACHINE, winreg.HKEY_CURRENT_USER):
                try:
                    base_key = winreg.OpenKey(hive, base_key_path)
                except OSError:
                    continue
                index = 0
                while True:
                    try:
                        subkey_name = winreg.EnumKey(base_key, index)
                        index += 1
                    except OSError:
                        break
                    year = next((token for token in subkey_name.split() if token.isdigit() and len(token) == 4), None)
                    if not year:
                        continue
                    subkey = winreg.OpenKey(base_key, subkey_name)
                    for value_name in ("InstallationLocation", "InstallPath", ""):
                        try:
                            value, _ = winreg.QueryValueEx(subkey, value_name)
                            if value and os.path.isdir(value):
                                exe = os.path.join(value, "Revit.exe")
                                if os.path.isfile(exe):
                                    found[year] = exe
                                break
                        except OSError:
                            continue
                    winreg.CloseKey(subkey)
                winreg.CloseKey(base_key)
        except ImportError:
            pass

        program_files = os.environ.get("ProgramFiles", r"C:\Program Files")
        for year in range(2027, 2019, -1):
            year_str = str(year)
            if year_str in found:
                continue
            exe = os.path.join(program_files, "Autodesk", "Revit {}".format(year_str), "Revit.exe")
            if os.path.isfile(exe):
                found[year_str] = exe

        return [{"year": year, "path": path} for year, path in sorted(found.items(), key=lambda x: x[0], reverse=True)]
