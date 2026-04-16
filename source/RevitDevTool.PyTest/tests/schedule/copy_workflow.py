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


def set_attr_if_value(obj, attr_name, value):
    if obj is None or value is None:
        return
    try:
        setattr(obj, attr_name, value)
    except Exception:
        pass


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
    OVERRIDE_ATTRIBUTES = (
        "BackgroundColor",
        "SheetBackgroundColor",
        "Bold",
        "BorderBottomLineStyle",
        "BorderLeftLineStyle",
        "BorderLineStyle",
        "BorderRightLineStyle",
        "BorderTopLineStyle",
        "Font",
        "FontColor",
        "FontSize",
        "HorizontalAlignment",
        "Italics",
        "TextOrientation",
        "Underline",
        "VerticalAlignment",
    )

    STYLE_ATTRIBUTES = (
        "FontName",
        "TextSize",
        "TextOrientation",
        "IsFontBold",
        "IsFontItalic",
        "IsFontUnderline",
        "FontHorizontalAlignment",
        "FontVerticalAlignment",
        "TextColor",
        "BackgroundColor",
        "SheetBackgroundColor",
        "BorderTopLineStyle",
        "BorderBottomLineStyle",
        "BorderLeftLineStyle",
        "BorderRightLineStyle",
    )

    FORMAT_ATTRIBUTES = (
        "UseDefault",
        "Accuracy",
        "SuppressLeadingZeros",
        "SuppressTrailingZeros",
        "SuppressSpaces",
        "UseDigitGrouping",
        "UsePlusPrefix",
    )

    def copy_style(self, source_style, target_style):
        if source_style is None or target_style is None:
            return

        source_override = source_style.GetCellStyleOverrideOptions()
        target_override = target_style.GetCellStyleOverrideOptions()
        if source_override is not None and target_override is not None:
            for attr_name in self.OVERRIDE_ATTRIBUTES:
                set_attr_if_value(
                    target_override,
                    attr_name,
                    getattr(source_override, attr_name, None),
                )
            try:
                target_style.SetCellStyleOverrideOptions(target_override)
            except Exception:
                pass

        for attr_name in self.STYLE_ATTRIBUTES:
            set_attr_if_value(
                target_style, attr_name, getattr(source_style, attr_name, None)
            )

    def copy_format(self, source_format, target_format):
        if source_format is None or target_format is None:
            return

        for attr_name in self.FORMAT_ATTRIBUTES:
            set_attr_if_value(
                target_format, attr_name, getattr(source_format, attr_name, None)
            )

    def copy_range(
        self,
        doc,
        source_header,
        target_header,
        row,
        source_col,
        target_left,
        target_right,
    ):
        source_style = source_header.GetTableCellStyle(row, source_col)
        source_format = source_header.GetCellFormatOptions(row, source_col, doc)
        for target_col in range(target_left, target_right + 1):
            target_style = target_header.GetTableCellStyle(row, target_col)
            self.copy_style(source_style, target_style)
            target_header.SetCellStyle(row, target_col, target_style)

            target_format = target_header.GetCellFormatOptions(row, target_col, doc)
            self.copy_format(source_format, target_format)
            target_header.SetCellFormatOptions(row, target_col, target_format)

    def reset_row_merges(self, target_header, row, target_last_col):
        from Autodesk.Revit import DB

        for target_col in range(target_header.FirstColumnNumber, target_last_col + 1):
            try:
                target_header.SetMergedCell(
                    row,
                    target_col,
                    DB.TableMergedCell(row, target_col, row, target_col),
                )
            except Exception:
                pass


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

    def column_count(self, section):
        return section.LastColumnNumber - section.FirstColumnNumber + 1

    def body_column_count(self, schedule):
        body = get_section(schedule, "Body")
        return self.column_count(body)

    def body_last_column(self, schedule):
        body = get_section(schedule, "Body")
        return body.LastColumnNumber

    def find_special_title_row(self, header, body_last_col):
        if body_last_col is None:
            return None
        first_col = header.FirstColumnNumber
        visible_last_col = min(header.LastColumnNumber, body_last_col)
        for row in range(header.FirstRowNumber, header.LastRowNumber + 1):
            merged_cell = header.GetMergedCell(row, first_col)
            if merged_cell is None:
                continue
            if (
                merged_cell.Top == merged_cell.Bottom
                and merged_cell.Left == merged_cell.Right
            ):
                continue
            if merged_cell.Left != first_col or merged_cell.Right < visible_last_col:
                continue

            has_text = False
            for col in range(first_col, visible_last_col + 1):
                text = header.GetCellText(row, col)
                if text not in (None, ""):
                    has_text = True
                    break
            if not has_text:
                return row
        return None

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

    def try_copy_text(self, source_row, target_row, col):
        text = self.source_header.GetCellText(source_row, col)
        if text in (None, ""):
            return False
        if self.is_schedule_title_text(text):
            return False
        try:
            self.target_header.SetCellText(target_row, col, text)
            return True
        except Exception:
            return False

    def apply_visual_cell_widths(self, target_last_col):
        source_row = self.source_header.FirstRowNumber
        source_spans = self.source_inspector.row_spans(source_row)
        source_last_col = self.source_header.LastColumnNumber

        for span in source_spans:
            target_right = span["right"]
            if span["right"] == source_last_col:
                target_right = target_last_col

            source_width = self.source_inspector.span_width(span)
            target_column_count = target_right - span["left"] + 1
            if target_column_count <= 0:
                continue
            width_per_column = source_width / float(target_column_count)
            for col in range(span["left"], target_right + 1):
                self.target_header.SetColumnWidth(col, width_per_column)

    def synchronize_header_columns(self, required_last_col):
        while self.target_header.LastColumnNumber < required_last_col:
            self.target_header.InsertColumn(self.target_header.LastColumnNumber + 1)

        while self.target_header.LastColumnNumber > required_last_col:
            try:
                self.target_header.RemoveColumn(self.target_header.LastColumnNumber)
            except Exception:
                break

    def copy_column_widths(self, last_col):
        for col in range(self.source_header.FirstColumnNumber, last_col + 1):
            try:
                self.target_header.SetColumnWidth(
                    col, self.source_header.GetColumnWidth(col)
                )
            except Exception:
                pass

    def synchronize_title_rows(self, existing_target_last_row, title_last_row):
        _ = existing_target_last_row
        while self.target_header.LastRowNumber < title_last_row:
            self.target_header.InsertRow(self.target_header.LastRowNumber + 1)

        while self.target_header.LastRowNumber > title_last_row:
            try:
                self.target_header.RemoveRow(self.target_header.LastRowNumber)
            except Exception:
                break

    def clear_extra_rows(self, existing_target_last_row, title_last_row):
        _ = existing_target_last_row
        _ = title_last_row
        return False

    def clear_header_range(self, last_row, last_col):
        from Autodesk.Revit import DB

        for row in range(self.target_header.FirstRowNumber, last_row + 1):
            for col in range(self.target_header.FirstColumnNumber, last_col + 1):
                try:
                    self.target_header.ClearCell(row, col)
                except Exception:
                    pass
                try:
                    self.target_header.SetMergedCell(
                        row,
                        col,
                        DB.TableMergedCell(row, col, row, col),
                    )
                except Exception:
                    pass

    def apply_header_merges(self, last_row, last_col):
        from Autodesk.Revit import DB

        for row in range(self.source_header.FirstRowNumber, last_row + 1):
            for col in range(self.source_header.FirstColumnNumber, last_col + 1):
                merged_cell = self.source_header.GetMergedCell(row, col)
                if merged_cell is None:
                    continue
                if (
                    merged_cell.Top == merged_cell.Bottom
                    and merged_cell.Left == merged_cell.Right
                ):
                    continue
                if merged_cell.Left != col:
                    continue
                try:
                    self.target_header.SetMergedCell(
                        row,
                        col,
                        DB.TableMergedCell(
                            merged_cell.Top,
                            merged_cell.Left,
                            min(merged_cell.Bottom, last_row),
                            min(merged_cell.Right, last_col),
                        ),
                    )
                except Exception:
                    pass

    def copy(self):
        from Autodesk.Revit import DB

        if self.body_column_count(self.source_schedule) != self.body_column_count(
            self.target_schedule
        ):
            return False

        target_body_last_col = self.body_last_column(self.target_schedule)

        try:
            self.target_schedule.HeaderTextTypeId = (
                self.source_schedule.HeaderTextTypeId
            )
        except Exception:
            pass
        try:
            self.target_schedule.TitleTextTypeId = self.source_schedule.TitleTextTypeId
        except Exception:
            pass

        existing_target_last_row = self.target_inspector.title_last_row()
        source_last_row = self.source_inspector.title_last_row()
        if source_last_row is None:
            return False

        source_last_col = min(self.source_header.LastColumnNumber, target_body_last_col)
        source_title_row = self.find_special_title_row(
            self.source_header, target_body_last_col
        )
        target_title_row = self.find_special_title_row(
            self.target_header, target_body_last_col
        )

        self.synchronize_header_columns(source_last_col)
        if source_title_row is not None and target_title_row is not None:
            while target_title_row < source_title_row:
                self.target_header.InsertRow(self.target_header.FirstRowNumber)
                target_title_row += 1
            while target_title_row > source_title_row:
                try:
                    self.target_header.RemoveRow(self.target_header.FirstRowNumber)
                    target_title_row -= 1
                except Exception:
                    break
            while self.target_header.LastRowNumber < source_last_row:
                self.target_header.InsertRow(self.target_header.LastRowNumber + 1)
            while self.target_header.LastRowNumber > source_last_row:
                if self.target_header.LastRowNumber == target_title_row:
                    break
                try:
                    self.target_header.RemoveRow(self.target_header.LastRowNumber)
                except Exception:
                    break
        else:
            self.synchronize_title_rows(existing_target_last_row, source_last_row)
        self.target_inspector = HeaderContractInspector(self.target_schedule)
        self.target_header = self.target_inspector.header
        target_last_row = min(self.target_header.LastRowNumber, source_last_row)
        target_last_col = min(self.target_header.LastColumnNumber, source_last_col)

        for row in range(self.target_header.FirstRowNumber, target_last_row + 1):
            if (
                target_title_row is not None
                and source_title_row is not None
                and row == target_title_row
            ):
                continue
            for col in range(self.target_header.FirstColumnNumber, target_last_col + 1):
                try:
                    self.target_header.ClearCell(row, col)
                except Exception:
                    pass
                try:
                    self.target_header.SetMergedCell(
                        row, col, DB.TableMergedCell(row, col, row, col)
                    )
                except Exception:
                    pass

        for row in range(self.source_header.FirstRowNumber, source_last_row + 1):
            target_row = row
            try:
                self.target_header.SetRowHeight(
                    target_row, self.source_header.GetRowHeight(row)
                )
            except Exception:
                pass
        self.copy_column_widths(target_last_col)
        for row in range(self.source_header.FirstRowNumber, source_last_row + 1):
            for col in range(self.source_header.FirstColumnNumber, target_last_col + 1):
                merged_cell = self.source_header.GetMergedCell(row, col)
                if merged_cell is None:
                    continue
                if (
                    merged_cell.Top == merged_cell.Bottom
                    and merged_cell.Left == merged_cell.Right
                ):
                    continue
                if merged_cell.Left != col:
                    continue
                target_row = row
                try:
                    self.target_header.SetMergedCell(
                        target_row,
                        col,
                        DB.TableMergedCell(
                            target_row,
                            merged_cell.Left,
                            target_row + (merged_cell.Bottom - merged_cell.Top),
                            min(merged_cell.Right, target_last_col),
                        ),
                    )
                except Exception:
                    pass

        copied_anything = False
        last_col = min(self.source_header.LastColumnNumber, target_last_col)

        for row in range(self.source_header.FirstRowNumber, source_last_row + 1):
            target_row = row

            for col in range(self.source_header.FirstColumnNumber, last_col + 1):
                merged_cell = self.source_header.GetMergedCell(row, col)
                if (
                    merged_cell is not None
                    and not (
                        merged_cell.Top == merged_cell.Bottom
                        and merged_cell.Left == merged_cell.Right
                    )
                    and merged_cell.Left != col
                ):
                    continue

                target_right = col
                if merged_cell is not None and not (
                    merged_cell.Top == merged_cell.Bottom
                    and merged_cell.Left == merged_cell.Right
                ):
                    target_right = min(merged_cell.Right, target_last_col)

                try:
                    self.cell_formatter.copy_range(
                        self.doc,
                        self.source_header,
                        self.target_header,
                        row,
                        col,
                        col,
                        target_right,
                    )
                    copied_anything = True
                except Exception:
                    pass

                if (
                    source_title_row is not None
                    and target_title_row is not None
                    and row == source_title_row
                ):
                    continue

                if self.try_copy_text(row, target_row, col):
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

        template_name = "__schedule_formatting_temp__%s" % element_id_value(
            self.source.Id
        )
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

        if (
            OPTION_FORMATTING in selected_option_names
            and OPTION_HEADER in selected_option_names
        ):

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

    def run(
        self,
        mode,
        field_required_options,
        formatting_required_options,
        template_builder,
    ):
        from Autodesk.Revit import DB

        result = None
        if mode == "direct":
            tx = DB.Transaction(self.doc, "pytest: direct header copy")
            tx.Start()
            try:
                copied = HeaderCopyEngine(self.source, self.target).copy()
                tx.Commit()
            except Exception:
                tx.RollBack()
                raise

            result = {
                "source_name": self.source.Name,
                "selected_names": sorted(
                    [OPTION_FIELDS, OPTION_FORMATTING, OPTION_HEADER]
                ),
                "applied": [self.target.Name] if copied else [],
                "skipped": [],
                "warnings": [] if copied else ["No header title rows were updated."],
            }
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
