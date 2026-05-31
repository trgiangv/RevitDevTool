"""Test-side table cell copy helpers using Revit API only."""


class TableCellCopier(object):
    def __init__(self, doc, source_header, target_header):
        self.doc = doc
        self.source_header = source_header
        self.target_header = target_header

    def copy_override_options(self, source_override, target_override):
        if source_override is None or target_override is None:
            return

        target_override.BackgroundColor = source_override.BackgroundColor
        target_override.Bold = source_override.Bold
        target_override.BorderBottomLineStyle = source_override.BorderBottomLineStyle
        target_override.BorderLeftLineStyle = source_override.BorderLeftLineStyle
        target_override.BorderLineStyle = source_override.BorderLineStyle
        target_override.BorderRightLineStyle = source_override.BorderRightLineStyle
        target_override.BorderTopLineStyle = source_override.BorderTopLineStyle
        target_override.Font = source_override.Font
        target_override.FontColor = source_override.FontColor
        target_override.FontSize = source_override.FontSize
        target_override.HorizontalAlignment = source_override.HorizontalAlignment
        target_override.Italics = source_override.Italics
        target_override.TextOrientation = source_override.TextOrientation
        target_override.Underline = source_override.Underline
        target_override.VerticalAlignment = source_override.VerticalAlignment

    def copy_style_properties(self, source_style, target_style):
        if source_style is None or target_style is None:
            return

        target_style.FontName = source_style.FontName
        target_style.TextSize = source_style.TextSize
        target_style.TextOrientation = source_style.TextOrientation
        target_style.IsFontBold = source_style.IsFontBold
        target_style.IsFontItalic = source_style.IsFontItalic
        target_style.IsFontUnderline = source_style.IsFontUnderline
        target_style.FontHorizontalAlignment = source_style.FontHorizontalAlignment
        target_style.FontVerticalAlignment = source_style.FontVerticalAlignment
        target_style.TextColor = source_style.TextColor
        target_style.BackgroundColor = source_style.BackgroundColor
        target_style.BorderTopLineStyle = source_style.BorderTopLineStyle
        target_style.BorderBottomLineStyle = source_style.BorderBottomLineStyle
        target_style.BorderLeftLineStyle = source_style.BorderLeftLineStyle
        target_style.BorderRightLineStyle = source_style.BorderRightLineStyle

    def copy_format_properties(self, source_format, target_format):
        if source_format is None or target_format is None:
            return

        target_format.UseDefault = source_format.UseDefault
        if source_format.UseDefault:
            return

        target_format.Accuracy = source_format.Accuracy
        target_format.SuppressLeadingZeros = source_format.SuppressLeadingZeros
        target_format.SuppressTrailingZeros = source_format.SuppressTrailingZeros
        target_format.SuppressSpaces = source_format.SuppressSpaces
        target_format.UseDigitGrouping = source_format.UseDigitGrouping
        target_format.UsePlusPrefix = source_format.UsePlusPrefix

    def copy_style(self, source_style, target_style):
        if source_style is None or target_style is None:
            return

        source_override = source_style.GetCellStyleOverrideOptions()
        target_override = target_style.GetCellStyleOverrideOptions()
        if source_override is not None and target_override is not None:
            self.copy_override_options(source_override, target_override)
            target_style.SetCellStyleOverrideOptions(target_override)

        self.copy_style_properties(source_style, target_style)

    def copy_format(self, source_format, target_format):
        if source_format is None or target_format is None:
            return

        self.copy_format_properties(source_format, target_format)

    def copy_cell(self, source_row, target_row, source_col, target_col):
        source_style = self.source_header.GetTableCellStyle(source_row, source_col)
        source_format = self.source_header.GetCellFormatOptions(
            source_row,
            source_col,
            self.doc,
        )

        target_style = self.target_header.GetTableCellStyle(target_row, target_col)
        self.copy_style(source_style, target_style)
        self.target_header.SetCellStyle(target_row, target_col, target_style)

        target_format = self.target_header.GetCellFormatOptions(
            target_row,
            target_col,
            self.doc,
        )
        self.copy_format(source_format, target_format)
        self.target_header.SetCellFormatOptions(target_row, target_col, target_format)
