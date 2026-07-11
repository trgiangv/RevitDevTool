"""Toolset error type for the Python MCP toolset."""


class ToolError(Exception):
    """Raised when a tool encounters an expected operational error.

    FastMCP catches exceptions and returns them as error content to the MCP client.
    """

    def __init__(self, message: str, *, code: str = "tool.error"):
        super().__init__(message)
        self.code = code
