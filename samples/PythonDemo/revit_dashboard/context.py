# -*- coding: utf-8 -*-
"""Revit application context.

Always read the document through ``HOST_APP.doc``. Do not cache it.
"""

from __future__ import division, print_function

from Autodesk.Revit import DB, UI, ApplicationServices


class _HostApplication(object):
    """Wrapper around the ``__revit__`` instance supplied to the command."""

    @property
    def uiapp(self):
        # type: () -> UI.UIApplication
        """Return the UIApplication provided to the running command."""
        return __revit__  # noqa: F821  # pyright: ignore[reportUndefinedVariable]

    @property
    def app(self):
        # type: () -> ApplicationServices.Application
        """Return the Application provided to the running command."""
        return self.uiapp.Application

    @property
    def addin_id(self):
        # type: () -> DB.AddInId
        """Return the active add-in id."""
        return self.app.ActiveAddInId

    @property
    def has_api_context(self):
        # type: () -> bool
        """Determine if the host application is in an API context."""
        return self.app.ActiveAddInId is not None

    @property
    def uidoc(self):
        # type: () -> UI.UIDocument
        """Return the active UIDocument."""
        return self.uiapp.ActiveUIDocument

    @property
    def doc(self):
        # type: () -> DB.Document
        """Return the active Document.

        Always use this property. The active document can change.
        """
        return self.uidoc.Document

    @property
    def active_view(self):
        # type: () -> DB.View
        """Return the active view (UIDocument.ActiveView)."""
        return self.uidoc.ActiveView

    @property
    def docs(self):
        # type: () -> list[DB.Document]
        """Return the open documents."""
        return list(self.app.Documents)

    @property
    def available_servers(self):
        # type: () -> list[str]
        """Return available Revit server names."""
        return list(self.app.GetRevitServerNetworkHosts())

    @property
    def version(self):
        # type: () -> int
        """Return the Revit version number (for example 2025)."""
        return int(self.app.VersionNumber)

    @property
    def version_name(self):
        # type: () -> str
        """Return the Revit version name."""
        return self.app.VersionName

    @property
    def language(self):
        # type: () -> ApplicationServices.LanguageType
        """Return the Revit language type."""
        return self.app.Language

    @property
    def username(self):
        # type: () -> str
        """Return the Revit username (Application.Username)."""
        return self.app.Username


HOST_APP = _HostApplication()
