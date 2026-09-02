"""Partial-success aggregation for write tools."""

from dto.common import ToolErrorEntry


class OperationOutcome:
    def __init__(self) -> None:
        self._success_count = 0
        self._failures: list[ToolErrorEntry] = []

    def record_success(self) -> None:
        self._success_count += 1

    def record(
        self, success: bool, message: str, element_id: int | None = None
    ) -> None:
        if success:
            self._success_count += 1
        else:
            self._failures.append(ToolErrorEntry.from_message(message, element_id))

    def record_failure(self, element_id: int | None, message: str) -> None:
        self._failures.append(ToolErrorEntry.from_message(message, element_id))

    def record_exception(self, element_id: int | None, exc: Exception) -> None:
        self._failures.append(ToolErrorEntry.from_exception(exc, element_id))

    def summarize(self) -> dict:
        return {
            "success_count": self._success_count,
            "failure_count": len(self._failures),
            "failures": [f.model_dump(by_alias=True) for f in self._failures] or None,
        }
