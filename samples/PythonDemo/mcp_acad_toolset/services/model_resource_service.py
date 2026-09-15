"""Live drawing resources returning JSON."""

import json

from services.query_service import QueryService


class ModelResourceService:
    def __init__(self) -> None:
        self._query = QueryService()

    def get_context(self) -> str:
        return _serialize(self._query.get_status().model_dump())

    def get_layers(self) -> str:
        return _serialize(self._query.list_layers().model_dump())

    def get_selection(self) -> str:
        return _serialize(self._query.get_selection().model_dump())

    def get_entity(self, handle: str) -> str:
        return _serialize(self._query.get_entity_by_handle(handle).model_dump())


def _serialize(payload: dict) -> str:
    return json.dumps(payload, ensure_ascii=False)
