# Filter Engine

How a custom filter goes from the editor to Revit element ids, and why each
step looks the way it does.

## Filter model (core)

`FilterDefinition` = checked category ids (`Groups`) + a root `FilterSet`.

- `FilterSet`: `Match` (All / Any), `Inverted` (NOT), children.
- `FilterRule`: `ParameterKey`, `FilterOperator`, value text, `IsCaseSensitive`.
- `UnsupportedRule`: a rule read from a Revit filter that the panel cannot show.

Rules carry no value kind. The kind always comes from the parameter at compile
time, so a stored filter can never disagree with the model.

## Parameter identity

`ParameterKey` is the one match key for "the same parameter" across documents:

| Origin | Key |
|--------|-----|
| Built-in | `BuiltInParameter` name |
| Shared | GUID |
| Project or family (not shared) | Name + data type |

- The data type is the spec's `ForgeTypeId` string without its trailing version
  suffix (`ParameterResolver.DataTypeOf`), because two documents saved by
  different Revit builds can carry the same spec with different suffixes. When a
  definition has no spec, the storage type stands in.
- **Same name, other data type** stays a separate key: "Width" as length and
  "Width" as text are different parameters and cannot share a rule.
- **Family + Project with the same name and data type** merge into one key.
  Users see one parameter; which documents bind it as a project parameter is
  noise for filtering.
- **Origin and binding are tags, not part of the key.** `ParameterInfo.Origins`
  and `Bindings` are flags merged across documents for display only.

`ParameterCatalog.Merge` merges per-document catalogs by key and sets coverage
(how many of the ticked documents have the key).

## Family parameters

Found on a live Revit 2026 model, and the reason for most of the resolver code.
A non-shared family parameter seen on a project element:

- has a **positive** id and a `ParameterElement` with an `InternalDefinition` in
  the project;
- has **no `ParameterBindings` entry** (that is how the catalog tells Family from
  Project);
- is **not** in `ParameterFilterUtilities.GetFilterableParametersInCommon`;
- is **not** returned by `FilteredElementCollector.OfClass(typeof(ParameterElement))`;
- **does** work in `ElementParameterFilter`, so it compiles to native rules like
  any project parameter;
- gets a **different id per family**. "Swing Angle" on two door families is two
  ids.

So a name + data type key resolves to a set of ids. The catalog records the ids
it saw on sampled elements; `ParameterResolver.IdsOf` uses those when cached,
else collects `ParameterElement`s by name and samples one element per type.

## Compiling a rule (`FilterCompiler`)

Per target document:

1. **Resolve** the key to `ResolvedParameter`s (id, storage type, spec). For each
   candidate id the resolver finds one element in the checked categories that
   has it and reads storage and spec from that element's `Parameter`. An id no
   element carries is dropped. A key the document lacks resolves to nothing.
2. **Build one leaf per id** and OR them:

   ```text
   rule(key)   = OR over ids:  Exists(id) AND Rule(id)
   Exists(id)  = HasValue(id) OR HasNoValue(id)
   Missing(key)= NOT( OR over ids: Exists(id) )
   ```

   The `Exists` guard is there because a native not-equals rule passes elements
   that do not have the parameter at all. Without it, `Swing Angle ≠ 90°` would
   also match doors whose family has no Swing Angle.
3. **No ids**: the rule matches nothing, except *parameter missing*, which
   matches everything in that document.
4. **Sets** compile to `LogicalAndFilter` / `LogicalOrFilter`. NOT is pushed down
   with De Morgan until it reaches `ElementParameterFilter`, whose `inverted`
   flag does the rest. `Compiled.All` / `Compiled.None` fold away so an empty
   branch costs nothing.
5. The final filter runs on the candidate ids (model elements of the checked
   categories, intersected with the scope) in slices of 5,000.

### Value kinds and operators

`ValueKinds.Infer` is the only place a kind is decided:

| Kind | From | Native rule |
|------|------|-------------|
| YesNo | spec is Yes/No (checked first) | integer rule on 1 / 0 (`TryParseYesNo` accepts Yes, No, true, false, 1, 0) |
| Number | measurable spec, or double storage | double rule, tolerance 1e-6, value parsed with the Current model's units |
| Reference | `ElementId` storage | equals: OR over matching ids; not equals: has value AND each not-equals |
| Enumerated ("Enum") | integer whose sampled display text is not a number | integer rule on the value mapped from its name |
| Integer | other integers | integer rule |
| Text | everything else | string rule |

Operators per kind (`FilterOperators.For`): Text has all of them; Integer and
Number have equals and the comparisons; YesNo, Reference and Enumerated have
equals and not equals. Every kind adds has value, has no value, parameter exists
and parameter missing.

Notes per kind:

- **Number.** The Current model's `Units` parse the text for every document,
  host and link alike, so "90°" means the same internal value everywhere even
  if a link uses other display units.
- **Reference.** A name is looked up among levels, phases and materials, then
  element types, in the target document; a number is an element id.
- **Enumerated.** The value index records each display text with its integer
  (`ValueEntry.EnumValues`), so "Single" maps back to its stored integer. An
  unknown name on *does not equal* becomes "has a value".
- **Labels.** Enum values show text from their `[Description]` attribute:
  `EnumDescriptionConverter` in XAML, `EnumText.Of` in code (for example the
  "Enum" spec label). Both cache per value.

## Case-sensitive text rules

Revit string rules always ignore case, and no API switch changes that. A rule
with **Aa** on runs in two passes (`CaseSensitiveRules` + `FilterPlan`):

1. **Native pass on a widened tree.** The tree is rewritten so the native filter
   returns a superset of the exact answer:
   - in a positive position, a positive leaf (equals, contains, begins, ends)
     keeps its case-insensitive rule, which matches more; a negative leaf (not
     equals, not contains ...) becomes *parameter exists*;
   - under an inverted set the roles swap, because the leaf will be negated: a
     negative leaf keeps its case-insensitive rule (a subset, so its negation is
     a superset), and a positive leaf becomes *match nothing*.
2. **Exact pass** on that superset only: for each element, read the value (the
   instance first, then its type) and run the whole original tree with ordinal
   comparison (`TextRules.Matches`). Non-text leaves reuse their native filter
   per element.

Only the superset is read in memory, so the cost follows the native result, not
the model size.

## Revit's own filter dialog

A decompile of the Revit 2026 UI assemblies shows the Edit Filters dialog is
native C++. The managed `UIFramework` side only asks native code, per parameter
id, for the allowed operators, whether the value is free text or a drop-down,
and the (id, name) value list. Nothing there exposes a managed rule builder or
case-sensitive matching, so the panel builds its own rules on the public
`ParameterFilterRuleFactory`.

## Revit filters as sources

`RevitFilterCatalog` lists each ticked document's `ParameterFilterElement`s and
`SelectionFilterElement`s.

- Running one uses the filter itself in its own document:
  `GetElementFilter()` AND `ElementMulticategoryFilter(GetCategories())`, or the
  stored ids of a selection filter. The panel's read-back of its rules is for
  display only and never drives the result.
- The read-back turns `ElementLogicalFilter` / `ElementParameterFilter` /
  `FilterRule` trees into rule rows; anything else (for example a global
  parameter rule) becomes an `UnsupportedRule` row.
- "In view" reads the active view's filters; for a link on Revit 2024 and later
  it reads the filters of the link's linked view.

## Live checks

Revit 2026, Snowdon Towers HVAC with the Architectural link, Doors (142
elements), driven through the panel with FlaUI and compared with direct API
counts. All matched.

| Rule | Count |
|------|-------|
| Swing Angle has a value | 131 |
| Swing Angle parameter missing | 11 |
| Swing Angle > 45° | 130 |
| Swing Angle = 90° | 130 |
| Swing Angle ≠ 90° | 1 |
| Swing Angle ≤ 45° | 1 |
| Mark = "s10", Aa off | 1 |
| Mark = "s10", Aa on | 0 |
| Mark = "S10", Aa on | 1 |
| Mark ≠ "S10", Aa on | 141 |
| Mark ≠ "s10", Aa on | 142 |

Swing Angle is a family parameter with two ids in that link, so it exercises the
OR over ids, the Exists guard and *parameter missing*.
