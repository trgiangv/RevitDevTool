"""Revit application context and host application wrapper.

Provides access to UIApplication, Application, active Document,
active view, version info, and other useful properties.

IMPORTANT: Always use HOST_APP.doc to access the document, never cache it.

Usage:
    from revit_dashboard.context import HOST_APP
    
    doc = HOST_APP.doc  # Always get fresh document
    view = HOST_APP.active_view
"""

from __future__ import annotations

from Autodesk.Revit import DB, UI, ApplicationServices


class _HostApplication:
    """Private Wrapper for Current Instance of Revit.

    Provides access to UIApplication, Application, active Document,
    active view, version info, and other useful properties.

    IMPORTANT: Always access doc via HOST_APP.doc property, never cache the document.
    """

    @property
    def uiapp(self) -> UI.UIApplication:
        """Return UIApplication provided to the running command."""
        return __revit__  # type: ignore

    @property
    def app(self) -> ApplicationServices.Application:
        """Return Application provided to the running command."""
        return self.uiapp.Application

    @property
    def addin_id(self) -> DB.AddInId:
        """Return active addin id."""
        return self.app.ActiveAddInId

    @property
    def has_api_context(self) -> bool:
        """Determine if host application is in API context."""
        return self.app.ActiveAddInId is not None

    @property
    def uidoc(self) -> UI.UIDocument:
        """Return active UIDocument."""
        return self.uiapp.ActiveUIDocument

    @property
    def doc(self) -> DB.Document:
        """Return active Document.
        
        IMPORTANT: Always use this property to access the document.
        Never cache the return value as the active document can change.
        """
        return self.uidoc.Document

    @property
    def active_view(self) -> DB.View:
        """Return view that is active (UIDocument.ActiveView)."""
        return self.uidoc.ActiveView

    @property
    def docs(self) -> list[DB.Document]:
        """Return :obj:`list` of open :obj:`Document` objects."""
        return list(self.app.Documents)

    @property
    def available_servers(self) -> list[str]:
        """Return :obj:`list` of available Revit server names."""
        return list(self.app.GetRevitServerNetworkHosts())

    @property
    def version(self) -> int:
        """int: Return version number (e.g. '2018')."""
        return int(self.app.VersionNumber)

    @property
    def version_name(self) -> str:
        """str: Return version name (e.g. 'Autodesk Revit 2018')."""
        return self.app.VersionName

    @property
    def language(self) -> ApplicationServices.LanguageType:
        """str: Return language type (e.g. 'LanguageType.English_USA')."""
        return self.app.Language

    @property
    def username(self) -> str:
        """str: Return the username from Revit API (Application.Username)."""
        return self.app.Username


HOST_APP = _HostApplication()
