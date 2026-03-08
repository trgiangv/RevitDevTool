from __future__ import annotations

from typing import Any

from pydantic import BaseModel, ConfigDict, Field, field_validator


_EXECUTION_MODE_NAMES = {
    0: "Script",
    1: "Assembly",
    2: "Python",
    3: "FSharp",
}


class ToolDefinition(BaseModel):
    model_config = ConfigDict(extra="forbid")

    toolId: str | None = None
    name: str
    description: str
    inputSchemaJson: str
    sourceAddress: str | None = None
    groupKey: str | None = None
    groupName: str | None = None
    sourceKind: str | None = None
    containerType: str | None = None
    methodName: str | None = None
    sourcePath: str | None = None
    outputSchemaJson: str | None = None
    annotationsJson: str | None = None
    metaJson: str | None = None
    structuredOutput: bool = True

    @field_validator("sourceKind", mode="before")
    @classmethod
    def normalize_source_kind(cls, value: object) -> object:
        if isinstance(value, int):
            return _EXECUTION_MODE_NAMES.get(value, str(value))
        return value


class BridgeError(BaseModel):
    model_config = ConfigDict(extra="forbid")

    code: str
    message: str
    details: str | None = None


class BridgeEnvelope(BaseModel):
    model_config = ConfigDict(extra="forbid", strict=True)

    id: str
    version: str
    schemaVersion: str
    schemaChecksum: str
    kind: str
    action: str
    payloadJson: str = "{}"
    executionId: str | None = None
    toolId: str | None = None
    toolName: str | None = None
    message: str | None = None
    resultKind: str | None = None
    metadata: dict[str, Any] | None = None
    execution: dict[str, Any] | None = None
    progressUpdates: list[dict[str, Any]] | None = None
    error: BridgeError | None = None


class ToolListPayload(BaseModel):
    model_config = ConfigDict(extra="forbid")

    tools: list[ToolDefinition]


class ToolCallPayload(BaseModel):
    model_config = ConfigDict(extra="forbid", strict=True)

    payload: Any
    message: str = ""
    resultKind: str
    metadata: dict[str, Any] = Field(default_factory=dict)
    progressUpdates: list[dict[str, Any]] = Field(default_factory=list)
