"""Test-side custom schedule copy workflows using Revit API only."""

from tests.schedule.field_mapping import FieldMappingService
from tests.schedule.model import get_section
from tests.schedule.table_cell import TableCellCopier


OPTION_COLUMN_TITLE = "Column Title"
OPTION_COLUMN_GROUPING = "Column Grouping"


class ColumnTitleCopyService(object):
    def __init__(self, doc, source_schedule, target_schedule, mapping):
        self.doc = doc
        self.source_schedule = source_schedule
        self.target_schedule = target_schedule
        self.mapping = mapping
        self.source_body = get_section(source_schedule, "Body")
        self.target_body = get_section(target_schedule, "Body")
        self.body_cell_copier = TableCellCopier(doc, self.source_body, self.target_body)

    def apply(self):
        if not self.mapping["matches"]:
            return False
        if not self._supports_column_titles():
            return False

        copied_anything = False
        for match in self.mapping["matches"]:
            source_field = match["source"]["field"]
            target_field = match["target"]["field"]
            if source_field is None or target_field is None:
                continue
            if self._copy_field_title(source_field, target_field):
                copied_anything = True

        if not copied_anything:
            return False

        self.doc.Regenerate()

        for match in self.mapping["matches"]:
            try:
                self._copy_title_cell_style(match)
            except Exception:
                pass

        self.doc.Regenerate()
        return copied_anything

    def _supports_column_titles(self):
        if not self.source_schedule.Definition.ShowHeaders:
            return False
        if not self.target_schedule.Definition.ShowHeaders:
            return False
        return True

    def _copy_field_title(self, source_field, target_field):
        copied_anything = False
        try:
            target_field.ColumnHeading = source_field.ColumnHeading
            copied_anything = True
        except Exception:
            pass
        try:
            target_field.SetStyle(source_field.GetStyle())
            copied_anything = True
        except Exception:
            pass
        try:
            target_field.SetFormatOptions(source_field.GetFormatOptions())
            copied_anything = True
        except Exception:
            pass
        try:
            target_field.GridColumnWidth = source_field.GridColumnWidth
            copied_anything = True
        except Exception:
            pass
        return copied_anything

    def _copy_title_cell_style(self, match):
        source_row = self._find_column_title_row(
            self.source_body,
            [mapped_match["source"] for mapped_match in self.mapping["matches"]],
        )
        target_row = self._find_column_title_row(
            self.target_body,
            [mapped_match["target"] for mapped_match in self.mapping["matches"]],
        )
        source_col = self.source_body.FirstColumnNumber + match["source"]["field_index"]
        target_col = self.target_body.FirstColumnNumber + match["target"]["field_index"]
        self.body_cell_copier.copy_cell(source_row, target_row, source_col, target_col)
        self.target_body.SetCellText(
            target_row,
            target_col,
            self.source_body.GetCellText(source_row, source_col),
        )

    def _find_column_title_row(self, body_section, field_infos):
        first_col = body_section.FirstColumnNumber
        last_col = body_section.LastColumnNumber
        best_row = body_section.FirstRowNumber
        best_match_count = -1
        for row in range(body_section.FirstRowNumber, body_section.LastRowNumber + 1):
            matched_heading_count = 0
            compared_count = 0
            for field_info in field_infos:
                col = first_col + field_info["field_index"]
                if col > last_col:
                    continue
                compared_count += 1
                try:
                    text = body_section.GetCellText(row, col)
                except Exception:
                    text = None
                if text == field_info["heading"]:
                    matched_heading_count += 1
            if matched_heading_count > best_match_count:
                best_match_count = matched_heading_count
                best_row = row
            if compared_count > 0 and matched_heading_count == compared_count:
                return row

        return best_row


class ColumnGroupingCopyService(object):
    def __init__(self, doc, source_schedule, target_schedule, mapping):
        self.doc = doc
        self.source_schedule = source_schedule
        self.target_schedule = target_schedule
        self.mapping = mapping
        self.source_body = get_section(source_schedule, "Body")
        self.target_body = get_section(target_schedule, "Body")
        self.source_to_target = self._build_source_to_target_map()
        self.body_cell_copier = TableCellCopier(doc, self.source_body, self.target_body)

    def apply(self):
        if not self.source_to_target:
            return False
        if not self.source_schedule.Definition.ShowHeaders:
            return False
        if not self.target_schedule.Definition.ShowHeaders:
            return False
        grouped_anything = False
        for source_group in self._extract_source_groups():
            target_cols = [
                self.source_to_target[source_index]
                for source_index in source_group["member_indexes"]
                if source_index in self.source_to_target
            ]
            target_cols = sorted(target_cols)
            if len(target_cols) < 2:
                continue

            try:
                self.target_schedule.GroupHeaders(
                    source_group["group_row_index"],
                    target_cols[0],
                    source_group["group_row_index"],
                    target_cols[-1],
                    source_group["text"],
                )
                self.doc.Regenerate()
                self._copy_group_cell_style(source_group, target_cols[0])
                self.doc.Regenerate()
                grouped_anything = True
            except Exception:
                pass
        return grouped_anything

    def _build_source_to_target_map(self):
        if self.source_body is None:
            return {}

        pairs = {}
        for match in self.mapping["matches"]:
            pairs[match["source"]["field_index"]] = match["target"]["field_index"]
        return pairs

    def _extract_source_groups(self):
        groups = []
        title_row = self._find_column_title_row(
            self.source_body,
            [mapped_match["source"] for mapped_match in self.mapping["matches"]],
        )
        for row in range(self.source_body.FirstRowNumber, title_row):
            col = self.source_body.FirstColumnNumber
            while col <= self.source_body.LastColumnNumber:
                merged_cell = self.source_body.GetMergedCell(row, col)
                if merged_cell is None:
                    col += 1
                    continue
                if merged_cell.Left != col:
                    col += 1
                    continue
                if merged_cell.Right <= merged_cell.Left:
                    col += 1
                    continue

                text = self.source_body.GetCellText(row, merged_cell.Left)
                if text in (None, "") or self._is_schedule_title_text(text):
                    col = merged_cell.Right + 1
                    continue

                groups.append(
                    {
                        "row": row,
                        "group_row_index": row - self.source_body.FirstRowNumber,
                        "left": merged_cell.Left,
                        "right": merged_cell.Right,
                        "text": text,
                        "member_indexes": list(
                            range(
                                merged_cell.Left - self.source_body.FirstColumnNumber,
                                merged_cell.Right - self.source_body.FirstColumnNumber + 1,
                            )
                        ),
                    }
                )
                col = merged_cell.Right + 1
        return sorted(
            groups,
            key=lambda group: (group["row"], group["right"] - group["left"]),
            reverse=True,
        )

    def _copy_group_cell_style(self, source_group, target_left_col):
        source_row = source_group["row"]
        source_col = source_group["left"]
        target_row = self.target_body.FirstRowNumber + source_group["group_row_index"]
        target_col = self.target_body.FirstColumnNumber + target_left_col
        self.body_cell_copier.copy_cell(source_row, target_row, source_col, target_col)
        self.target_body.SetCellText(
            target_row,
            target_col,
            self.source_body.GetCellText(source_row, source_col),
        )

    def _is_schedule_title_text(self, text):
        schedule_name = self.source_schedule.Name.strip().lower()
        normalized_text = text.strip().lower()
        return normalized_text == schedule_name or normalized_text == "<%s>" % schedule_name

    def _are_contiguous(self, values):
        if not values:
            return False
        start = values[0]
        for offset, value in enumerate(values):
            if value != start + offset:
                return False
        return True
    def _find_column_title_row(self, body_section, field_infos):
        first_col = body_section.FirstColumnNumber
        last_col = body_section.LastColumnNumber
        best_row = body_section.FirstRowNumber
        best_match_count = -1
        for row in range(body_section.FirstRowNumber, body_section.LastRowNumber + 1):
            matched_heading_count = 0
            compared_count = 0
            for field_info in field_infos:
                col = first_col + field_info["field_index"]
                if col > last_col:
                    continue
                compared_count += 1
                try:
                    text = body_section.GetCellText(row, col)
                except Exception:
                    text = None
                if text == field_info["heading"]:
                    matched_heading_count += 1
            if matched_heading_count > best_match_count:
                best_match_count = matched_heading_count
                best_row = row
            if compared_count > 0 and matched_heading_count == compared_count:
                return row

        return best_row


class CustomCopyService(object):
    def __init__(self, doc, source_schedule, target_schedule):
        self.doc = doc
        self.source_schedule = source_schedule
        self.target_schedule = target_schedule
        self.mapping = FieldMappingService(source_schedule, target_schedule).build_mapping()

    def apply(self, selected_names):
        result = {"applied": [], "skipped": [], "warnings": []}
        if not self.mapping["matches"]:
            result["warnings"].append("No matching target columns found for custom copy.")
            return result

        if OPTION_COLUMN_TITLE in selected_names:
            if ColumnTitleCopyService(
                self.doc,
                self.source_schedule,
                self.target_schedule,
                self.mapping,
            ).apply():
                result["applied"].append(OPTION_COLUMN_TITLE)
            else:
                result["skipped"].append(
                    {
                        "option": OPTION_COLUMN_TITLE,
                        "reason": "No matching target column found for custom title copy",
                    }
                )

        if OPTION_COLUMN_GROUPING in selected_names:
            if ColumnGroupingCopyService(
                self.doc,
                self.source_schedule,
                self.target_schedule,
                self.mapping,
            ).apply():
                result["applied"].append(OPTION_COLUMN_GROUPING)
            else:
                result["skipped"].append(
                    {
                        "option": OPTION_COLUMN_GROUPING,
                        "reason": "Source group has fewer than 2 matched target columns, grouping skipped",
                    }
                )

        return result
