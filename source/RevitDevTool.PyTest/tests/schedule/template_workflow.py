"""Template option helpers for schedule formatting tests."""

from tests.schedule.constants import FIELD_REQUIRED_OPTIONS
from tests.schedule.constants import OPTION_FIELDS
from tests.schedule.constants import TEMPLATE_OPTIONS
from tests.schedule.constants import get_option_builtin_parameters
from tests.schedule.model import element_id_value
from tests.schedule.model import get_schedule


class ScheduleTemplateOptions(object):
    @staticmethod
    def build(controls, user_selection=None):
        if controls is None or not hasattr(controls, "get"):
            controls = {}

        options = {}
        for name in TEMPLATE_OPTIONS:
            control = controls.get(name)
            options[name] = {
                "control": control,
                "selected": True if user_selection is None else name in user_selection,
            }

        if not options[OPTION_FIELDS]["selected"]:
            for option_name in FIELD_REQUIRED_OPTIONS:
                options[option_name]["selected"] = False

        return options

    @staticmethod
    def flags(selected_options):
        return {
            name: option["selected"] for name, option in selected_options.items()
        }


class ScheduleTemplateConfigurer(object):
    def __init__(self, doc):
        self.doc = doc

    def find_by_name(self, template_name):
        from Autodesk.Revit import DB

        collector = DB.FilteredElementCollector(self.doc).OfClass(DB.ViewSchedule)
        for schedule in collector:
            if schedule.IsTemplate and schedule.Name == template_name:
                return schedule
        return None

    def get_controls(self, template):
        from Autodesk.Revit import DB

        option_builtin_parameters = get_option_builtin_parameters()
        template_parameter_ids = {
            element_id_value(parameter_id)
            for parameter_id in list(template.GetTemplateParameterIds() or [])
        }
        non_controlled_ids = {
            element_id_value(parameter_id)
            for parameter_id in list(template.GetNonControlledTemplateParameterIds() or [])
        }

        controls = {}
        for option_name, built_in_parameter in option_builtin_parameters.items():
            parameter_id = DB.ElementId(built_in_parameter)
            parameter_value = element_id_value(parameter_id)
            controls[option_name] = {
                "id": parameter_value,
                "include": parameter_value in template_parameter_ids
                and parameter_value not in non_controlled_ids,
            }
        return controls

    def set_selected_options(self, template, selected_names=None):
        from Autodesk.Revit import DB
        from System.Collections.Generic import List

        controls = self.get_controls(template)
        selected_options = ScheduleTemplateOptions.build(
            controls,
            user_selection=selected_names,
        )
        selected_ids = {
            option["control"]["id"]
            for option in selected_options.values()
            if option["selected"] and option["control"] is not None
        }

        non_controlled_ids = List[DB.ElementId]()
        for parameter_id in list(template.GetTemplateParameterIds() or []):
            if element_id_value(parameter_id) not in selected_ids:
                non_controlled_ids.Add(parameter_id)

        template.SetNonControlledTemplateParameterIds(non_controlled_ids)
        return selected_options, controls

    def configure_temp_template(self, temp_template, template_name, selected_names):
        temp_template.Name = template_name
        return self.set_selected_options(temp_template, selected_names=selected_names)


class ScheduleTemplateWorkflow(object):
    def __init__(self, doc, source_schedule_id, target_schedule_id):
        self.doc = doc
        self.source = get_schedule(doc, source_schedule_id)
        self.target = get_schedule(doc, target_schedule_id)
        self.configurer = ScheduleTemplateConfigurer(doc)

    def exercise(self, selected_names, template_name):
        from Autodesk.Revit import DB

        created_template_id = None
        controls = None
        selected_options = None
        group = DB.TransactionGroup(
            self.doc,
            "pytest: exercise temp schedule template workflow",
        )
        group.Start()

        try:
            tx = DB.Transaction(self.doc, "pytest: cleanup temp schedule template")
            tx.Start()
            stale_template = self.configurer.find_by_name(template_name)
            if stale_template is not None:
                self.doc.Delete(stale_template.Id)
            tx.Commit()

            tx = DB.Transaction(self.doc, "pytest: create temp schedule template")
            tx.Start()
            temp_template = self.source.CreateViewTemplate()
            created_template_id = element_id_value(temp_template.Id)
            selected_options, controls = self.configurer.configure_temp_template(
                temp_template,
                template_name,
                selected_names,
            )
            tx.Commit()

            tx = DB.Transaction(self.doc, "pytest: apply temp schedule template")
            tx.Start()
            temp_template = self.doc.GetElement(DB.ElementId(created_template_id))
            self.target.ApplyViewTemplateParameters(temp_template)
            self.target.ViewTemplateId = temp_template.Id
            tx.Commit()

            target_template_id = element_id_value(self.target.ViewTemplateId)

            tx = DB.Transaction(self.doc, "pytest: detach and delete temp template")
            tx.Start()
            self.target.ViewTemplateId = DB.ElementId.InvalidElementId
            self.doc.Delete(DB.ElementId(created_template_id))
            tx.Commit()

            removed_template_id = element_id_value(self.target.ViewTemplateId)
            deleted_template = self.doc.GetElement(DB.ElementId(created_template_id))

            result = {
                "controls": controls,
                "selected_options": selected_options,
                "created_template_id": created_template_id,
                "target_template_id": target_template_id,
                "removed_template_id": removed_template_id,
                "template_deleted": deleted_template is None,
            }

            group.Assimilate()
            return result
        except Exception:
            try:
                group.RollBack()
            except Exception:
                pass
            raise


def template_copy_options(controls, user_selection=None):
    return ScheduleTemplateOptions.build(controls, user_selection=user_selection)


def selected_option_flags(selected_options):
    return ScheduleTemplateOptions.flags(selected_options)


def slugify_option_name(option_name):
    return "".join(
        character.lower() if character.isalnum() else "_"
        for character in option_name
    ).strip("_")


def find_schedule_template_by_name(doc, template_name):
    return ScheduleTemplateConfigurer(doc).find_by_name(template_name)


def configure_temp_template(doc, temp_template, template_name, selected_names):
    return ScheduleTemplateConfigurer(doc).configure_temp_template(
        temp_template,
        template_name,
        selected_names,
    )


def exercise_temp_template_workflow(revit_doc, selected_names, template_name, source_schedule_id, target_schedule_id):
    return ScheduleTemplateWorkflow(
        revit_doc,
        source_schedule_id,
        target_schedule_id,
    ).exercise(selected_names, template_name)