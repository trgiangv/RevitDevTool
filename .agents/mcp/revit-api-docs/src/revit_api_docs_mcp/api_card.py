from __future__ import annotations

import re
from typing import Any


def build_api_card(parsed: dict[str, Any]) -> dict[str, Any]:
    sections = parsed.get("sections", {})
    members = parsed.get("members", [])
    related = parsed.get("related", [])
    title = str(parsed.get("title", ""))
    text = "\n".join(
        value for value in (sections.get("summary", ""), sections.get("remarks", "")) if value
    )

    return drop_empty(
        {
            "purpose": first_meaningful_sentence(text),
            "use_cases": infer_use_cases(text),
            "lifecycle": infer_lifecycle(members),
            "constraints": infer_constraints(text, members),
            "key_members": key_members(members),
            "related_apis": key_related(related, title),
        }
    )


def first_meaningful_sentence(text: str) -> str:
    preferred = first_sentence_with(
        text,
        ("represents ", "creates ", "updates ", "gets ", "deletes ", "specifies ", "provides "),
    )
    if preferred:
        return clean_doc_artifacts(preferred)

    for sentence in split_sentences(text):
        normalized = compact_spaces(sentence)
        if is_meaningful_sentence(normalized):
            return clean_doc_artifacts(remove_doc_prefix(normalized))
    return ""


def first_sentence_with(text: str, prefixes: tuple[str, ...]) -> str:
    for sentence in split_sentences(text):
        normalized = remove_doc_prefix(compact_spaces(sentence))
        if normalized.casefold().startswith(prefixes):
            return normalized
    return ""


def remove_doc_prefix(sentence: str) -> str:
    markers = (
        " Represents ",
        " Creates ",
        " Updates ",
        " Gets ",
        " Deletes ",
        " Specifies ",
        " Provides ",
    )
    for marker in markers:
        if marker in sentence:
            return sentence[sentence.index(marker) + 1 :]
    return sentence


def infer_use_cases(text: str) -> list[str]:
    sentences = [clean_doc_artifacts(remove_doc_prefix(sentence)) for sentence in split_sentences(text)]
    return unique_non_empty(
        sentence
        for sentence in sentences
        if is_content_sentence(sentence)
        and contains_any(sentence, ("used to", "provides", "allows", "can ", "represents", "creates", "updates"))
    )[:5]


def infer_lifecycle(members: list[dict[str, str]]) -> list[str]:
    selected = sorted(
        (member for member in members if member_score(member) >= 2 and not is_noise_member(member)),
        key=lambda member: (-member_score(member), member.get("name", "")),
    )
    return [
        compact_member_action(member)
        for member in selected[:8]
        if compact_member_action(member)
    ]


def infer_constraints(text: str, members: list[dict[str, str]]) -> list[str]:
    member_descriptions = " ".join(member.get("description", "") for member in members)
    source = f"{text} {member_descriptions}"
    constraints = [
        clean_doc_artifacts(sentence)
        for sentence in split_sentences(source)
        if is_content_sentence(sentence) and contains_any(sentence, constraint_markers())
    ]
    return unique_non_empty(constraints)[:8]


def key_members(members: list[dict[str, str]]) -> list[dict[str, str]]:
    selected = sorted(
        (member for member in members if not is_noise_member(member)),
        key=lambda member: (-member_score(member), member.get("name", "")),
    )
    return selected[:10]


def key_related(related: list[dict[str, str]], current_title: str) -> list[dict[str, str]]:
    selected = [
        entry
        for entry in related
        if normalized_name(entry.get("title", "")) not in normalized_name(current_title)
        and not is_noise_related(entry)
    ]
    return selected[:10]


def member_score(member: dict[str, str]) -> int:
    name = member.get("name", "")
    description = member.get("description", "")
    score = 0
    if starts_with_any(name, action_verbs()):
        score += 3
    if contains_any(description, ("create", "update", "delete", "remove", "get", "set", "return")):
        score += 2
    if description:
        score += 1
    return score


def compact_member_action(member: dict[str, str]) -> str:
    name = member.get("name", "")
    description = member.get("description", "")
    if description:
        return f"{name}: {description}"
    return name


def is_noise_member(member: dict[str, str]) -> bool:
    name = member.get("name", "")
    return name in {"Dispose", "Equals", "Finalize", "GetHashCode", "GetType", "ToString"}


def is_noise_related(entry: dict[str, str]) -> bool:
    title = entry.get("title", "")
    return (
        title.endswith(" Namespace")
        or title in {"Autodesk.Revit.DB", "Autodesk.Revit.UI"}
        or title.endswith("Exception")
    )


def action_verbs() -> tuple[str, ...]:
    return (
        "Add",
        "Append",
        "Apply",
        "Clear",
        "Create",
        "Delete",
        "Dispose",
        "Export",
        "Find",
        "Get",
        "Import",
        "Load",
        "Open",
        "Remove",
        "Save",
        "Set",
        "Update",
    )


def constraint_markers() -> tuple[str, ...]:
    return (
        "must",
        "only",
        "cannot",
        "not ",
        "unsupported",
        "invalid",
        "exception",
        "absolute",
        "required",
        "failed",
        "subject to",
    )


def split_sentences(text: str) -> list[str]:
    sentences: list[str] = []
    for line in semantic_lines(text):
        sentences.extend(re.split(r"(?<=[.!?])\s+", line))
    return sentences


def semantic_lines(text: str) -> list[str]:
    lines: list[str] = []
    buffer = ""
    for raw_line in text.splitlines():
        line = clean_doc_artifacts(raw_line)
        if not line or is_metadata_line(line):
            continue
        buffer = compact_spaces(f"{buffer} {line}")
        if line.endswith((".", "!", "?")):
            lines.append(buffer)
            buffer = ""
    if buffer:
        lines.append(buffer)
    return lines


def unique_non_empty(values: Any) -> list[str]:
    result: list[str] = []
    seen: set[str] = set()
    for value in values:
        cleaned = compact_spaces(str(value))
        key = cleaned.casefold()
        if cleaned and key not in seen:
            seen.add(key)
            result.append(cleaned)
    return result


def is_meaningful_sentence(sentence: str) -> bool:
    noise = ("type exposes the following members",)
    folded = sentence.casefold()
    return len(sentence) >= 20 and is_content_sentence(sentence) and not any(item in folded for item in noise)


def is_content_sentence(sentence: str) -> bool:
    return not is_metadata_line(sentence)


def is_metadata_line(value: str) -> bool:
    ignored = (
        "revit ",
        "namespace:",
        "assembly:",
        "revitapi ",
        "inheritance hierarchy",
        "system ",
        "autodesk.revit.",
        "top",
    )
    folded = value.casefold()
    return folded.startswith(ignored)


def clean_doc_artifacts(value: str) -> str:
    value = re.sub(r"\[!:([^\]]+)\]", clean_cref, value)
    return compact_spaces(value)


def clean_cref(match: re.Match[str]) -> str:
    return match.group(1).replace("::", ".").rsplit(".", 1)[-1]


def normalized_name(value: str) -> str:
    return re.sub(r"[^a-z0-9]", "", value.casefold())


def compact_spaces(value: str) -> str:
    return re.sub(r"\s+", " ", value).strip()


def contains_any(value: str, needles: tuple[str, ...]) -> bool:
    folded = value.casefold()
    return any(needle.casefold() in folded for needle in needles)


def starts_with_any(value: str, prefixes: tuple[str, ...]) -> bool:
    return any(value.startswith(prefix) for prefix in prefixes)


def drop_empty(data: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in data.items() if value}
