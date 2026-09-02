"""Shared DTOs for toolset responses."""

from typing import Self

from pydantic import BaseModel, Field


class ToolErrorEntry(BaseModel):
    element_id: int | None = Field(default=None, alias="elementId")
    code: str = "not_found"
    message: str = ""
    recoverable: bool = True
    suggested_action: str | None = Field(default=None, alias="suggestedAction")

    model_config = {"populate_by_name": True}

    @classmethod
    def from_message(
        cls, message: str, element_id: int | None = None
    ) -> Self:
        code = "not_found"
        recoverable = True
        suggested = None
        lower = message.lower()
        if "borrowed" in lower or "workset" in lower:
            code, suggested = "element_borrowed", "release workset"
        elif "pinned" in lower:
            code, suggested = "element_pinned", "unpin element"
        elif "read-only" in lower or "readonly" in lower:
            code, suggested = "param_readonly", "remove readonly params"
        elif "group" in lower:
            code = "group_member"
        elif "not found" in lower:
            code = "not_found"
        elif "constraint" in lower:
            code = "constraint_violation"
        elif "type" in lower:
            code = "type_mismatch"
        return cls(
            elementId=element_id,
            code=code,
            message=message,
            recoverable=recoverable,
            suggestedAction=suggested,
        )

    @classmethod
    def from_exception(
        cls, exc: Exception, element_id: int | None = None
    ) -> Self:
        return cls.from_message(str(exc), element_id)
