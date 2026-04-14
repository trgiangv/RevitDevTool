"""Independent header copy workflow helpers for schedule formatting tests."""

from tests.schedule.constants import FIELD_REQUIRED_OPTIONS
from tests.schedule.constants import FORMATTING_REQUIRED_OPTIONS
from tests.schedule.constants import HEADER_COPY_OPTION_SET
from tests.schedule.constants import OPTION_FIELDS
from tests.schedule.constants import OPTION_FORMATTING
from tests.schedule.constants import OPTION_HEADER
from tests.schedule.contracts import HeaderContractInspector
from tests.schedule.contracts import capture_header_contract
from tests.schedule.contracts import compare_header_contracts
from tests.schedule.model import element_id_value
from tests.schedule.model import get_schedule_pair
from tests.schedule.model import get_section
from tests.schedule.template_workflow import find_schedule_template_by_name


def effective_selected_names(
    selected_names,
    field_required_options=FIELD_REQUIRED_OPTIONS,
    formatting_required_options=FORMATTING_REQUIRED_OPTIONS,
):
    effective_names = set(selected_names)
    if OPTION_FIELDS not in effective_names:
        for option_name in field_required_options:
            effective_names.discard(option_name)
    if OPTION_FORMATTING not in effective_names:
        for option_name in formatting_required_options:
            effective_names.discard(option_name)
    return effective_names


class TransactionRunner(object):
    def __init__(self, doc):
        self.doc = doc

    def run(self, name, action):
        from Autodesk.Revit import DB

        tx = DB.Transaction(self.doc, name)
        tx.Start()
        try:
            result = action()
            tx.Commit()
            return result
        except Exception:
            tx.RollBack()
            raise


class TableCellFormatter(object):
    def copy_style(self, source_style, target_style):
        if source_style is None or target_style is None:
            return

        source_override = source_style.GetCellStyleOverrideOptions()
        target_override = target_style.GetCellStyleOverrideOptions()
        if source_override is not None and target_override is not None:
            if source_override.BackgroundColor is not None:
                target_override.BackgroundColor = source_override.BackgroundColor
            if source_override.Bold is not None:
                target_override.Bold = source_override.Bold
            if source_override.BorderBottomLineStyle is not None:
                target_override.BorderBottomLineStyle = source_override.BorderBottomLineStyle
            if source_override.BorderLeftLineStyle is not None:
                target_override.BorderLeftLineStyle = source_override.BorderLeftLineStyle
            if source_override.BorderLineStyle is not None:
                target_override.BorderLineStyle = source_override.BorderLineStyle
            if source_override.BorderRightLineStyle is not None:
                target_override.BorderRightLineStyle = source_override.BorderRightLineStyle
            if source_override.BorderTopLineStyle is not None:
                target_override.BorderTopLineStyle = source_override.BorderTopLineStyle
            if source_override.Font is not None:
                target_override.Font = source_override.Font
            if source_override.FontColor is not None:
                target_override.FontColor = source_override.FontColor
            if source_override.FontSize is not None:
                target_override.FontSize = source_override.FontSize
            if source_override.HorizontalAlignment is not None:
                target_override.HorizontalAlignment = source_override.HorizontalAlignment
            if source_override.Italics is not None:
                target_override.Italics = source_override.Italics
            if source_override.TextOrientation is not None:
                target_override.TextOrientation = source_override.TextOrientation
            if source_override.Underline is not None:
                target_override.Underline = source_override.Underline
            if source_override.VerticalAlignment is not None:
                target_override.VerticalAlignment = source_override.VerticalAlignment
            target_style.SetCellStyleOverrideOptions(target_override)

        if source_style.FontName is not None:
            target_style.FontName = source_style.FontName
        if source_style.TextSize is not None:
            target_style.TextSize = source_style.TextSize
        if source_style.TextOrientation is not None:
            target_style.TextOrientation = source_style.TextOrientation
        if source_style.IsFontBold is not None:
            target_style.IsFontBold = source_style.IsFontBold
        if source_style.IsFontItalic is not None:
            target_style.IsFontItalic = source_style.IsFontItalic
        if source_style.IsFontUnderline is not None:
            target_style.IsFontUnderline = source_style.IsFontUnderline
        if source_style.FontHorizontalAlignment is not None:
            target_style.FontHorizontalAlignment = source_style.FontHorizontalAlignment
        if source_style.FontVerticalAlignment is not None:
            target_style.FontVerticalAlignment = source_style.FontVerticalAlignment
        if source_style.TextColor is not None:
            target_style.TextColor = source_style.TextColor
        if source_style.BackgroundColor is not None:
            target_style.BackgroundColor = source_style.BackgroundColor
        if source_style.SheetBackgroundColor is not None:
            target_style.SheetBackgroundColor = source_style.SheetBackgroundColor
        if source_style.BorderTopLineStyle is not None:
            target_style.BorderTopLineStyle = source_style.BorderTopLineStyle
        if source_style.BorderBottomLineStyle is not None:
            target_style.BorderBottomLineStyle = source_style.BorderBottomLineStyle
        if source_style.BorderLeftLineStyle is not None:
            target_style.BorderLeftLineStyle = source_style.BorderLeftLineStyle
        if source_style.BorderRightLineStyle is not None:
            target_style.BorderRightLineStyle = source_style.BorderRightLineStyle

    def copy_format(self, source_format, target_format):
        if source_format is None or target_format is None:
            return

        target_format.UseDefault = source_format.UseDefault
        if source_format.UseDefault:
            return

        if source_format.Accuracy is not None:
            target_format.Accuracy = source_format.Accuracy
        if source_format.SuppressLeadingZeros is not None:
            target_format.SuppressLeadingZeros = source_format.SuppressLeadingZeros
        if source_format.SuppressTrailingZeros is not None:
            target_format.SuppressTrailingZeros = source_format.SuppressTrailingZeros
        if source_format.SuppressSpaces is not None:
            target_format.SuppressSpaces = source_format.SuppressSpaces
        if source_format.UseDigitGrouping is not None:
            target_format.UseDigitGrouping = source_format.UseDigitGrouping
        if source_format.UsePlusPrefix is not None:
            target_format.UsePlusPrefix = source_format.UsePlusPrefix

    def copy_range(self, doc, source_header, target_header, row, source_col, target_left, target_right):
        source_style = source_header.GetTableCellStyle(row, source_col)
        source_format = source_header.GetCellFormatOptions(row, source_col, doc)
        for target_col in range(target_left, target_right + 1):
            target_style = target_header.GetTableCellStyle(row, target_col)
            self.copy_style(source_style, target_style)
            target_header.SetCellStyle(row, target_col, target_style)

            target_format = target_header.GetCellFormatOptions(row, target_col, doc)
            self.copy_format(source_format, target_format)
            target_header.SetCellFormatOptions(row, target_col, target_format)


class HeaderCopyEngine(object):
    def __init__(self, source_schedule, target_schedule):
        self.source_schedule = source_schedule
        self.target_schedule = target_schedule
        self.doc = source_schedule.Document
        self.source_header = get_section(source_schedule, "Header")
        self.target_header = get_section(target_schedule, "Header")
        self.source_inspector = HeaderContractInspector(source_schedule)
        self.target_inspector = HeaderContractInspector(target_schedule)
        self.cell_formatter = TableCellFormatter()

    def is_schedule_title_text(self, text):
        if text in (None, ""):
            return False

        normalized_text = text.strip().lower()
        source_name = self.source_schedule.Name.strip().lower()
        if normalized_text == source_name:
            return True
        if normalized_text == "<%s>" % source_name:
            return True
        return False

    def target_title_text_for_source_text(self, text):
        if text in (None, ""):
            return text

        normalized_text = text.strip().lower()
        source_name = self.source_schedule.Name.strip().lower()
        target_name = self.target_schedule.Name.strip()
        if normalized_text == source_name:
            return target_name
        if normalized_text == "<%s>" % source_name:
            return "<%s>" % target_name
        return text

    def try_copy_text(self, row, col):
        text = self.source_header.GetCellText(row, col)
        if text in (None, ""):
            return False
        if self.is_schedule_title_text(text):
            self.target_header.SetCellText(
                row,
                col,
                self.target_title_text_for_source_text(text),
            )
            return True
        self.target_header.SetCellText(row, col, text)
        return True

    def pick_width_driver_row(self):
        best_row = self.source_header.FirstRowNumber
        best_count = -1
        title_last_row = self.source_header.FirstRowNumber
        for row in range(self.source_header.FirstRowNumber, self.source_header.LastRowNumber + 1):
            spans = self.source_inspector.row_spans(row)
            span_count = len(spans)
            if span_count > best_count:
                best_row = row
                best_count = span_count

            has_nontrivial_merge = False
            for span in spans:
                if span["left"] != span["right"]:
                    has_nontrivial_merge = True
                    break
            if has_nontrivial_merge:
                title_last_row = row
                continue
            if title_last_row != self.source_header.FirstRowNumber:
                break

        return best_row

    def apply_visual_cell_widths(self):
        driver_row = self.pick_width_driver_row()
        source_spans = self.source_inspector.row_spans(driver_row)
        for source_span in source_spans:
            target_right = source_span["right"]
            if source_span["right"] == self.source_header.LastColumnNumber:
                target_right = self.target_header.LastColumnNumber

            source_width = self.source_inspector.span_width(source_span)
            target_column_count = target_right - source_span["left"] + 1
            if target_column_count <= 0:
                continue
            width_per_column = source_width / float(target_column_count)
            for col in range(source_span["left"], target_right + 1):
                self.target_header.SetColumnWidth(col, width_per_column)

    def clear_extra_rows(self, existing_target_last_row, title_last_row):
        from Autodesk.Revit import DB

        if existing_target_last_row is None or existing_target_last_row <= title_last_row:
            return False

        copied_anything = False
        for row in range(existing_target_last_row, title_last_row, -1):
            removed = False
            remove_row = getattr(self.target_header, "RemoveRow", None)
            if remove_row is not None:
                try:
                    remove_row(row)
                    removed = True
                    copied_anything = True
                except Exception:
                    removed = False
            if not removed:
                delete_row = getattr(self.target_header, "DeleteRow", None)
                if delete_row is not None:
                    try:
                        delete_row(row)
                        removed = True
                        copied_anything = True
                    except Exception:
                        removed = False
            if removed:
                continue

            for col in range(self.target_header.FirstColumnNumber, self.target_header.LastColumnNumber + 1):
                try:
                    self.target_header.SetMergedCell(
                        row,
                        col,
                        DB.TableMergedCell(row, col, row, col),
                    )
                except Exception:
                    pass
                try:
                    self.target_header.SetCellText(row, col, "")
                except Exception:
                    pass
        return copied_anything

    def synchronize_title_rows(self, existing_target_last_row, title_last_row):
        first_row = self.source_header.FirstRowNumber
        source_count = title_last_row - first_row + 1
        if existing_target_last_row is None:
            target_count = 0
        else:
            target_count = existing_target_last_row - self.target_header.FirstRowNumber + 1

        while target_count < source_count:
            self.target_header.InsertRow(self.target_header.FirstRowNumber)
            target_count += 1

        while target_count > source_count:
            row_to_remove = self.target_header.FirstRowNumber + source_count
            removed = False
            remove_row = getattr(self.target_header, "RemoveRow", None)
            if remove_row is not None:
                try:
                    remove_row(row_to_remove)
                    removed = True
                except Exception:
                    removed = False
            if not removed:
                delete_row = getattr(self.target_header, "DeleteRow", None)
                if delete_row is not None:
                    try:
                        delete_row(row_to_remove)
                        removed = True
                    except Exception:
                        removed = False
            if not removed:
                break
            target_count -= 1

    def copy(self):
        from Autodesk.Revit import DB

        self.target_schedule.HeaderTextTypeId = self.source_schedule.HeaderTextTypeId
        self.target_schedule.TitleTextTypeId = self.source_schedule.TitleTextTypeId

        existing_target_last_row = self.target_inspector.title_last_row()
        title_last_row = self.source_inspector.title_last_row()
        assert title_last_row is not None

        self.synchronize_title_rows(existing_target_last_row, title_last_row)
        self.target_inspector = HeaderContractInspector(self.target_schedule)
        self.target_header = self.target_inspector.header
        existing_target_last_row = self.target_inspector.title_last_row()

        self.apply_visual_cell_widths()

        copied_anything = False
        source_last_col = self.source_header.LastColumnNumber
        target_last_col = self.target_header.LastColumnNumber

        for row in range(self.source_header.FirstRowNumber, title_last_row + 1):
            self.target_header.SetRowHeight(row, self.source_header.GetRowHeight(row))
            copied_anything = True
            for target_col in range(self.target_header.FirstColumnNumber, self.target_header.LastColumnNumber + 1):
                self.target_header.SetMergedCell(
                    row,
                    target_col,
                    DB.TableMergedCell(row, target_col, row, target_col),
                )
            for span in self.source_inspector.row_spans(row):
                left = span["left"]
                right = span["right"]
                target_right = right
                if right == source_last_col and target_last_col > source_last_col:
                    target_right = target_last_col

                self.target_header.SetMergedCell(
                    row,
                    left,
                    DB.TableMergedCell(row, left, row, target_right),
                )
                self.cell_formatter.copy_range(
                    self.doc,
                    self.source_header,
                    self.target_header,
                    row,
                    left,
                    left,
                    target_right,
                )
                copied_anything = True

                if self.try_copy_text(row, left):
                    copied_anything = True

        if self.clear_extra_rows(existing_target_last_row, title_last_row):
            copied_anything = True

        return copied_anything


class ScheduleCopyScenarioRunner(object):
    def __init__(self, doc, case, source_schedule=None, target_schedule=None):
        self.doc = doc
        if source_schedule is not None and target_schedule is not None:
            self.source = source_schedule
            self.target = target_schedule
        else:
            self.source, self.target = get_schedule_pair(doc, case)
        self.case = case
        self.transaction_runner = TransactionRunner(doc)

    def apply_copy_independently(
        self,
        selected_names,
        field_required_options,
        formatting_required_options,
        template_builder,
    ):
        from Autodesk.Revit import DB

        template_name = "__schedule_formatting_temp__%s" % element_id_value(self.source.Id)
        selected_option_names = sorted(
            list(
                effective_selected_names(
                    selected_names,
                    field_required_options,
                    formatting_required_options,
                )
            )
        )
        result = {
            "source_name": self.source.Name,
            "template_name": template_name,
            "selected_names": selected_option_names,
            "applied": [],
            "skipped": [],
            "warnings": [],
        }

        try:
            def create_template_action():
                stale_template = find_schedule_template_by_name(self.doc, template_name)
                if stale_template is not None:
                    self.doc.Delete(stale_template.Id)
                temp_template = self.source.CreateViewTemplate()
                temp_template.Name = template_name
                template_builder(self.doc, temp_template, template_name, selected_names)
                return temp_template

            temp_template = self.transaction_runner.run(
                "pytest: create temp schedule template",
                create_template_action,
            )

            def apply_template_action():
                applied_targets = []
                if self.target.ViewTemplateId != DB.ElementId.InvalidElementId:
                    result["skipped"].append(
                        {
                            "name": self.target.Name,
                            "reason": "target already has a view template assigned",
                        }
                    )
                    return applied_targets

                self.target.ApplyViewTemplateParameters(temp_template)
                self.target.ViewTemplateId = temp_template.Id
                applied_targets.append(self.target)
                result["applied"].append(self.target.Name)
                return applied_targets

            applied_targets = self.transaction_runner.run(
                "pytest: apply temp schedule template",
                apply_template_action,
            )

            def detach_template_action():
                for target in applied_targets:
                    target.ViewTemplateId = DB.ElementId.InvalidElementId
                self.doc.Delete(temp_template.Id)

            self.transaction_runner.run(
                "pytest: detach and delete temp template",
                detach_template_action,
            )

            if OPTION_FORMATTING in selected_option_names and OPTION_HEADER in selected_option_names:
                def apply_header_action():
                    if HeaderCopyEngine(self.source, self.target).copy():
                        return [self.target.Name]
                    return []

                copied_targets = self.transaction_runner.run(
                    "pytest: apply header title rows",
                    apply_header_action,
                )
                if not copied_targets:
                    result["warnings"].append("No header title rows were updated.")

            if not result["applied"]:
                result["warnings"].append("No targets were updated.")
            return result
        except Exception:
            raise

    def run(self, mode, field_required_options, formatting_required_options, template_builder):
        from Autodesk.Revit import DB

        result = None
        if mode == "direct":
            tx = DB.Transaction(self.doc, "pytest: direct header copy")
            tx.Start()
            try:
                HeaderCopyEngine(self.source, self.target).copy()
                tx.Commit()
            except Exception:
                tx.RollBack()
                raise
        elif mode == "apply":
            result = self.apply_copy_independently(
                HEADER_COPY_OPTION_SET,
                field_required_options,
                formatting_required_options,
                template_builder,
            )
            assert result["applied"] == [self.target.Name], result
        else:
            raise AssertionError("Unsupported mode: %s" % mode)

        source_contract = capture_header_contract(self.source)
        target_contract = capture_header_contract(self.target)
        issues = compare_header_contracts(
            self.source,
            self.target,
            target_contract,
            source_contract,
        )
        return {
            "mode": mode,
            "case": None if self.case is None else self.case["name"],
            "source": self.source.Name,
            "target": self.target.Name,
            "row_count": source_contract["row_count"],
            "source_contract": source_contract,
            "target_contract": target_contract,
            "comparison_issues": issues,
            "matches_expected": len(issues) == 0,
            "undo_items": None,
            "result": result,
        }


def run_header_copy_case(
    revit_doc,
    case,
    mode,
    field_required_options,
    formatting_required_options,
    template_builder,
):
    return ScheduleCopyScenarioRunner(revit_doc, case).run(
        mode,
        field_required_options,
        formatting_required_options,
        template_builder,
    )