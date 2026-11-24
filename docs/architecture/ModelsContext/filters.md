# Filter Engine

How a custom filter goes from the editor to Revit element ids, and why each
step looks the way it does.

## Filter model (core)

`FilterDefinition` = checked category ids (`Groups`) + a root `FilterSet`.

- `FilterSet` (a group): `Match` (All = AND, Any = OR), children, and an
  optional `FilterScope`: document `SourceId`s and category ids. Empty means
  the same as the parent. There is no NOT; negation lives in the operators.
- `FilterRule`: `ParameterKey`, `FilterOperator`, value text, `IsCaseSensitive`,
  and `Picked` (the stored number range of a value picked from the list).
- `UnsupportedRule`: a rule read from a Revit filter that the panel cannot show.

Rules carry no value kind.

## Group scope

- **Documents** are `SourceId`s, so a scope means a file: every instance of a
  link shares the link's source (its path, lower case), and the Current model
  is `model:` plus its path.
- **Per document, before compiling**, `FilterScopes.ForDocument` prunes the
  tree: a group whose documents leave out that document, or that names a source
  the model does not have (*unresolved*), is false. Under an AND parent the
  parent becomes false; under an OR parent the group is dropped. When the root
  is false, that document is not evaluated. The panel does this in the core, so
  the evaluator port never sees document scope.
- **Categories** stay in the tree. `FilterCompiler` ANDs a group's children with
  `ElementMulticategoryFilter(categories)`; the fake matcher checks the row's
  category the same way. Nested scopes therefore intersect.
- **View models**: `EffectiveScope` is the parent's scope narrowed by the
  group's own. The condition picker, coverage and value counts use the
  group's effective scope; the scope lists show only the parent's items, plus
  any stale value with a warning. Each group reads its parameter list from
  `ParameterScopes` (core), which merges the documents' catalogs once per
  distinct scope and drops the cache when the catalogs reload.
- **Filter tab parts**: `FilterTabViewModel` only composes
  `FilterLibraryViewModel` (sources, switch, save, rename, duplicate; store edits
  through the pure `CustomFilterLibrary`), `CategoryPickerViewModel`,
  `FilterRunner` (evaluate, selection sync, baseline, mode, pause) and the rule tree. Moves
  in the tree go through the pure `FilterTreeMoves`.
- **Stored** filters write `scope` (`documents`, `categories`) on a group; the
  mapping lives in the core (`StoredFilters`) and `CustomFilterStore` only reads
  and writes the file. The kind always comes from the parameter at compile
time, so a stored filter can never disagree with the model.

## Parameter identity

`ParameterKey` is the one match key for "the same parameter" across documents:

| Origin | Key |
|--------|-----|
| Built-in | `BuiltInParameter` name |
| Shared | GUID |
| Project or family (not shared) | Name + data type |

- The data type is the spec's `ForgeTypeId` string without its trailing version
  suffix (`ParameterDataTypes.Of`, core), because two documents saved by
  different Revit builds can carry the same spec with different suffixes. When a
  definition has no spec, the storage type stands in.
- **Same name, other data type** stays a separate key: "Width" as length and
  "Width" as text are different parameters and cannot share a rule.
- **Family + Project with the same name and data type** merge into one key.
  Users see one parameter; which documents bind it as a project parameter is
  noise for filtering.
- **Origin and binding are tags, not part of the key.** `ParameterInfo.Origins`
  and `Bindings` are flags merged across documents for display only.

`ParameterCatalog.Merge` merges per-document catalogs by key and lists the
models that have the key (`ParameterInfo.Models`, for the tooltip). A key that
only some models in a group's scope have is not a warning: the rule matches in
those models and resolves to nothing in the others (see *No ids* below).

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
it saw on sampled elements; `ParameterLocator.IdsOf` uses those when cached,
else collects `ParameterElement`s by name and samples one element per type.

## Compiling a rule (`FilterCompiler`)

Per target document, `RevitFilterEvaluator` first warms the cache in short
Revit calls awaited one after another: the candidates, then three calls per
parameter key (`ParameterLocator.Locate`, `WithTypes`, `WithLacking`: the
parameter, its type presence, its lacking elements), for a key a presence rule
uses also the silent-element calls (below), then the candidates' type ids when
a key has type presence or silent elements. A compile call that has to resolve
inline skips the silent elements. A model change between those calls only
leaves an entry stamped with the old version, so the compile call resolves it
again. The
compile call then reads those entries and resolves inline only what a model
change dropped in between.

1. **Resolve** the key to `ResolvedParameter`s (id, storage type, spec, type
   presence) with `ParameterLocator.Resolve`, cached per source, key and
   categories. For each candidate id the resolver finds one element in the
   checked categories that has it, or else one element type, and reads storage
   and spec from that `Parameter`. It probes in order has value, holds a number,
   holds an integer, has no value, and keeps the first element that really
   carries the parameter: has-no-value also passes elements without it (Volume
   on a Shaft Opening), and neither has-value rule passes a calculated parameter
   (Calculated Flow on Mechanical Equipment). It also runs the has-value and has-no-value
   rules on the element types of those categories (`TypePresence`: types with a
   value, types without one that carry the parameter), and lists the elements
   that pass has-no-value without carrying it (`Lacking`: Shaft Openings and
   Model Text for Volume). An id neither carries is dropped. A key the
   document lacks resolves to nothing.
2. **Build one leaf per id** and OR them. `HasValue` and `HasNoValue` are the
   native has-value rules, narrowed per storage: for text an empty string `""`
   is no value (`HasValue AND ≠ ""`, `HasNoValue OR (HasValue AND = "")`), for
   element ids -1 is no value (see *Reference* below). `Holds(id)` is
   `≥ int.MinValue` for integers and `≥ double.MinValue` for doubles (nothing
   for other storage). In *defined* and *empty*, `HasNoValue(id)` also excludes
   `Lacking`, and *undefined* adds `Lacking` back:

   ```text
   rule(key)      = OR over ids:  Guard(id) AND Rule(id)
   Guard(id)      = Defined(id) for text, NotEmpty(id) for every other storage
   Blind(id)      = Holds(id) AND NOT HasValue(id) AND NOT HasNoValue(id)
   Held(id)       = Holds(id), or nothing when Held is Absent
   Defined(id)    = HasValue(id) OR HasNoValue(id) OR Held(id) OR TypeIn(types with id)
                    OR In(Silent)
   NotEmpty(id)   = HasValue(id) OR TypeIn(types with a value) OR In(Silent with a value)
                    OR (Held is Value AND Holds(id) AND NOT HasNoValue(id)
                        AND NOT TypeIn(types with id))
   Empty(id)      = (HasNoValue(id) AND NOT TypeIn(types with a value))
                    OR (NOT HasValue(id) AND TypeIn(types without a value))
                    OR (Held is Empty AND Blind(id) AND NOT TypeIn(types with id))
                    OR In(Silent without a value)
   Undefined(key) = AND over ids: ((NOT(HasValue(id) OR HasNoValue(id)) AND NOT Held(id))
                    OR In(Lacking)) AND NOT In(Silent) AND NOT TypeIn(types with id)
   ```

   *Empty* means what the Properties palette shows as blank
   (`Parameter.HasValue` false, `""`, or id -1), also where Revit's own rules
   read the element otherwise. An element has a built-in when a native rule
   sees it or it lists it in its own `Parameters` (per element: within one
   type, 186 Golden Nugget pipes without flow do not list Reynolds Number while
   their siblings do; Properties shows the row only on the siblings). Two
   per-id facts come from the resolver:

   - `Held` (`WithTypes` call): some parameters are *blind*, both has-value
     rules fail and only `Holds` passes. The resolver reads the first 25 blind
     elements the way the value list does (the element's own parameter, else
     its type's) and takes the majority, live on Golden Nugget, Revit 2026:
     - *Value*: Workset (12 of 12), Voltage Loss on lighting fixtures (the
       type holds 1, the type is blind too), CW Connection (the type holds No);
     - *Empty*: the parameter is there without a value;
     - *Absent*: family parameters "Festanschluss - angeschlossen aktuell"
       (4 of 4) and "Allgemein - Gleichzeitigkeit" (3 of 3): `≥ double.MinValue`
       passes Electrical Equipment that do not have the parameter at all, so
       blind elements are not the parameter's elements.
     No blind element gives *Value*.
   - `Silent` (`SilentReader`, its own Revit calls after `WithLacking`: one
     native call for the ids, then slices of at most 30 ms; only for a key
     that a *not empty*, *empty*, *defined* or *undefined* rule uses, cached
     with the resolved parameter): elements no rule sees at all, not even
     `Holds` while the element lists the parameter. The resolver keeps the
     checked categories where some element passes one of the three rules
     (one native first-element check each), walks their elements that fail all
     three, skips types with type presence (and skips the walk for a text or
     reference parameter with type presence, where every instance fails the
     has-value rules), checks per element that it lists the built-in in its
     own `Parameters` (so `get_Parameter` phantoms stay undefined: Reynolds
     Number on pipes without flow, End Attachment Level Reference on 36
     unattached beams, IfcGUID on circuit 6728139), and splits the rest by
     their own value into *with a value* and *without*. A category whose
     elements are all silent is not walked: those stay *undefined*.

   A key with several ids on one element is *empty* only when none of them has
   a value: `Empty(key) = OR(Empty(id)) AND each id (Empty(id) OR
   Undefined(id))`. The Japanese sample's link rooms carry two project
   parameters named "仕上表 グループ名" (both element ids); the second is -1 on
   all 113 rooms, so an OR over ids called 102 rooms with a group empty. A
   value read without an id takes the one with a value.

   The same per-element rule holds for shared parameters: `get_Parameter(Guid)`
   returns the curtain panels' type parameter "建具 形式" on 104 panels whose
   type is a wall type and that do not list it; they are undefined, as in
   Properties.

   Reading a value (`ParameterLocator.Find`, used by the value list and the
   exact text pass) takes the type's parameter over an instance's built-in
   that has a value no native rule sees: an instance's `IFC_TYPE_GUID` returns
   a per-instance GUID that Properties never shows (Type IfcGUID is a type
   property). The resolver's probe tries up to 25 elements per rule, so a key
   whose first has-no-value element lacks it still resolves (Volume on Detail
   Items).

   The guard is there because a native not-equals rule passes elements that do
   not have the parameter at all. Without it, `Swing Angle ≠ 90°` would also
   match doors whose family has no Swing Angle. Positive rules need it too: a
   native string rule reads a missing parameter as empty text, so
   `Fire Rating = ""` would match furniture. For numbers, Yes/No, enums and
   references the guard is *not empty*: a value comparison never matches an
   element whose parameter has no value. Wall 1996153 (Parapet Cap Bandstand)
   has a type without a Function value, and a native integer not-equals passes
   it.

### Type parameters

Found on the live Revit 2026 model: the has-value and has-no-value rules do
**not** see a type parameter from an instance, while every value rule (equals,
not equals, contains, greater, …) does. On the Architectural link's 1,128
walls, `FUNCTION_PARAM` and `FIRE_RATING` (both type parameters) pass has-value
0 times and has-no-value 0 times, yet `Function = Exterior` passes 311 and
`Fire Rating = 2 HR` passes 293. On element types the has-value rules work.

So presence is decided per type: `TypePresence` lists the types of the checked
categories, plus every type used by an element of those categories, that have
the id with and without a value (one collector over a few
hundred types, cached with the resolved parameter). The used types matter
because an element's type can sit in another category: the Architectural
link's curtain panel 1055511 is a `Wall` whose type is a wall type (category
Walls), so with only Curtain Panels types its type parameters (Automatically
Embed, Join Condition, the border mullion types, …) failed *defined* and every
rule on them missed it. `TypeFilters` turns a type
set into a native filter over the candidates: the candidates are split once by
`GetTypeId` (`CandidateSet.MembersIn`, cached with the candidate ids, about
1 µs per element) and the side outside the set becomes an `ExclusionFilter`. A
set every candidate is in folds to *all*, an empty one to *none*. Measured on
1,629 Architectural elements, a filter of `ELEM_TYPE_PARAM` rules costs about
0.3 µs per rule per element (110 types: 28 ms), so the type-id rule form was not
used.

Three more quirks the formulas cover:

- Some instance parameters are blind to both has-value rules. The
  Architectural link's curtain walls carry `WALL_KEY_REF_PARAM` (Location Line,
  `HasValue` true in the API), yet has-value and has-no-value both fail on all
  65 of them, while `= 0` passes 64. `Holds` (a value rule every stored integer
  passes) finds them: `≥ int.MinValue` passes all 1,168 elements with the
  parameter and none of the 27,821 without. Without it, Location Line = Wall
  Centerline missed 41 walls.

- Some families also expose the built-in on the instance without a value. On the
  Architectural doors `FIRE_RATING` exists on the instance (no value, neither
  has-value rule passes) while the type holds "60 MIN"; on ducts
  `ALL_MODEL_FAMILY_NAME` passes has-no-value on all 1,053 instances while the
  type holds "Round Duct". Revit's equals rule reads the type value in both, so
  a type with a value wins over an empty instance: `Empty` excludes it and
  `ParameterLocator.Find` returns the type's parameter when the instance one has
  no value.
- *undefined* is built as the AND of the per-id negations, so the
  `ExclusionFilter` is never inverted.
3. **No ids**: the rule matches nothing, except *undefined*, which matches
   everything in that document.
4. **Groups** compile to `LogicalAndFilter` / `LogicalOrFilter`, ANDed with the
   group's category filter. Only *undefined* negates, through the
   `ElementParameterFilter` `inverted` flag. `Compiled.All` / `Compiled.None`
   fold away so an empty branch costs nothing.
5. The final filter runs on the candidate ids (model elements of the checked
   categories in any design option, narrowed to the scope as described in
   [Design options](#design-options)) in slices of at most 5,000 elements
   and about 30 ms. The candidate ids are cached per source, category set and
   version in `ParameterCache` and dropped with the values (any
   `DocumentChanged` of that source, link reload). Inside a slice the batches
   are sized by `SlicePace` from the measured cost per element (first batch 100,
   then what fits in the time left, 25 to 500), so a slow filter does not run a
   whole 500-element batch past the budget.

### Design options

One rule for Explorer counts, Filter matches and host filters, per scope:

| Scope | Elements | Why |
|-------|----------|-----|
| Whole model | Main model + primary options | Same set as a Revit schedule; a secondary option is not part of the design until it is made primary, and the user cannot see it to check a selection. |
| Active view | Exactly what the view collector returns | The view collector applies the view's design option settings; a view set to a secondary option shows that option and hides the primary. |
| Selection | Exactly the selected ids | Elements can only be picked where they are visible. |

`CandidateSet` holds the model element ids of the checked categories in every
option plus the set of those in a secondary option. Whole model takes
`ModelIds` (all minus secondary); a scope takes `Ids` intersected with the
scope ids. Host filters use the same set (a selection filter's stored ids are
first narrowed to model elements). The value index counts only `ModelIds` but
maps text to stored values over all `Ids`, so a value that exists only in a
secondary option still compiles in a view that shows it.

Measured on Revit 2026, Architectural link (design option sets Bandstand
Options and Bleacher Seating; "Continuous Bleachers no Stairs" is secondary
and holds 8 walls), rule E1 = Walls, Function = Exterior AND Fire Rating ≠
1 HR AND ≠ 2 HR:

| What | Walls | E1 |
|------|-------|----|
| `OfCategory(OST_Walls)`, no option filter | 1,128 | 215 |
| Main model + primary (`ModelFilter`) | 1,120 | 207 |
| Wall schedule with Include elements in links, all links (main + primary per link: 1,269 walls, 345 E1) | 1,269 rows | 345 rows |
| Panel Whole model: `CandidateSet.ModelIds` | 1,120 | 207 |
| Host `{3D}` view, link view collector (`LinkElements.InView`) | 1,120 (0 secondary, 38 primary-option elements of all categories) | 207 |
| Panel Active view in `{3D}`: `Ids` ∩ view ids | 1,120 | 207 |

All 51 plan, section, elevation and 3D views of the Architectural link that
show bleacher walls show primary-option walls and none of the 8 secondary
ones.
Host views were not swept: building the link view collector in a loop over
host views crashed Revit ([README gotcha 1](README.md#gotchas)). So the
secondary-option view case is covered by the core tests (`DesignOptionTests`)
rather than a live count; the API has no setter for a view's design option
visibility, and Select All Instances cannot run on linked elements (the host
has no design options).

### Value kinds and operators

`ValueKinds.Infer` is the only place a kind is decided:

| Kind | From | Native rule |
|------|------|-------------|
| YesNo | spec is Yes/No (checked first) | integer rule on 1 / 0 (`TryParseYesNo` accepts Yes, No, true, false, 1, 0) |
| Number | measurable spec, or double storage | double rule; a typed value is parsed once in the Current model's units and matches within half its display step there; a value picked from the value list uses the stored values behind its text; anything else uses tolerance 1e-6 |
| Reference | `ElementId` storage | equals: OR over matching ids; not equals: AND of each not-equals |
| Enumerated ("Enum") | integer whose sampled display text is not a number | integer rule on the value mapped from its name |
| Integer | other integers | integer rule |
| Text | everything else | string rule |

Operators per kind (`FilterOperators.For`): Text has all of them; Integer and
Number have equals and the comparisons; YesNo, Reference and Enumerated have
equals and not equals. Every kind adds *not empty* (`NotEmpty`), *empty*
(`Empty`), *defined* (`Defined`) and *undefined* (`Undefined`), the words
Revizto uses.

*Not equals* is the same for every kind (decided 2026-11-24): *defined* AND
(*empty* OR the native not-equals). An empty value (blank text, no number, a
reference of -1) does not equal anything, so it matches. Every other value rule
stays guarded by *defined* for text and *not empty* for other storage.
`FilterKeys.Presence` warms the silent-value scan for *not equals* too.

Notes per kind:

- **Number, typed.** `RuleValueParser` parses the text with the **Current
  model's** units (`ResolvedDocument.Model`), so the rule turns into one
  internal value that every document in the group's scope uses. "300" in a
  Current model set to millimeters is 300 mm in a link set to feet and inches
  too (L2 LU-01). The text becomes the range of values that display as it in
  the Current model: half a display step (`FormatOptions.Accuracy` in internal
  units) around the parsed value, shifted for *Up* and *Down* rounding. A value
  exactly half a step away is matched to the text Revit shows for it: Revit 2026
  shows 0.585 in³ as "0.59 in³" and 0.775 in³ as "0.78 in³", so a positive tie
  belongs to the upper text (a negative one is taken to mirror it). The same
  tolerance applies to the comparisons, so "> 10'" means above what displays as
  10'. This is decided (2026-11-19): 10 Golden Nugget cable tray fittings show
  "3 mm" (stored 2.54 mm), so "≥ 3 mm" finds them, while Revit's view filter
  with its tiny tolerance does not. The live oracle compares the same way
  (the stored value formatted and parsed back in the Current model's units,
  or half a display step when the typed text is not on the display grid), and
  the native reference uses half a display step as its epsilon. The value box placeholder is an example in the Current model's format
  (`FormatExampleAsync`: one internal unit rounded to one significant digit in
  the display unit, "300 mm", "1' - 0"").
- **Number, picked.** The value list shows each document's own display text.
  `ValueReader` records, per text, the smallest and largest stored double that
  displays as it (`ValueEntry.Numbers`), the core unions them across documents
  (`ValueCount.Stored`), and a pick stores that range on the rule
  (`FilterRule.Picked`, saved as `picked`). The compiler then matches the
  stored range plus 1e-6 and never reads the text again. Typing over a pick
  drops it. A number read back from a Revit filter keeps its rule value the
  same way.
- **Why not the target document's units.** An earlier rule parsed each rule in
  each target document's units, so one typed value meant different internal
  values per document: "300" was 300 mm in one model and 300 feet in another.
  The rule now means one value everywhere, and the user reads and types it in
  the Current model's format, which the placeholder shows. The case that rule
  was added for, a link slope shown as a bare "2" (degrees in the
  Architectural link, rise per 12" in the HVAC model), is now a pick: the 8
  roofs that show "2" keep their stored 2° (L2 PS-01), while a typed "2" is
  2"/12" and matches none (PS-02).
- **Reference.** The value index records each display text with the stored
  ids that produced it (`ValueEntry.RawValues`, filled by `ValueReader`), and
  equals is an OR over those ids. That covers every target the drop-down can
  show: levels and types, but also categories (`ELEM_CATEGORY_PARAM` shows
  "Walls", and `GetElement` returns nothing for a category id) and design
  options (`DESIGN_OPTION_ID`). The earlier name lookup among levels, phases,
  materials and types missed those, so Category and Design Option rules
  matched nothing. A text that is not in the index but is a number is an
  element id. An `ElementId` parameter that holds -1 has no value text, so the
  value list counts it as `[No parameter]`, yet Revit's has-value rule passes it
  (all 1,128 Architectural walls pass has-value on `DESIGN_OPTION_ID`). So for
  `ElementId` storage *not empty* is has-value AND ≠ -1 and *empty* is
  has-no-value OR (has-value AND = -1), on instances and in `TypePresence`.
  This is decided (2026-11-19): Properties shows *None* or a blank for -1, so
  the panel calls it empty while Revit's has-value rule calls it a value (Host
  Id on 509 unhosted air terminals, Phase Demolished on 997 duct fittings). The
  sweep marks those rows `none reference`.
  Measured on the Architectural walls, Whole model: Design Option *empty*
  1,096 and *not empty* 24, the same as the value list (1,096 without a value,
  8 Middle Stair, 16 Twisty Trellis); = "Continuous Bleachers no Stairs" is 0
  in Whole model and 8 over all options.
- **Enumerated.** The same index maps each display text to the integers stored
  under it, so "Single" maps back to its stored integer. An unknown name on
  *does not equal* matches every element that has the parameter.
- **Labels.** Enum values show text from their `[Description]` attribute:
  `EnumDescriptionConverter` in XAML, `EnumText.Of` in code (for example the
  "Enum" spec label). Both cache per value.

## Exact text rules

Revit string rules always ignore case, and no API switch changes that. Native
*greater* and *less* on text also sort numbers inside the text by value
(`S10` > `S5`), while the panel compares text in ordinal order (`S10` < `S5`),
the same with Aa off and on. A rule with **Aa** on, and any text comparison
(greater, less, or equal to either), runs in two passes (`CaseSensitiveRules`,
`TextRules.IsOrder` + `FilterPlan`):

1. **Native pass on a widened tree.** The tree is rewritten so the native filter
   returns a superset of the exact answer:
   - a positive leaf (equals, contains, begins, ends) keeps its
     case-insensitive rule, which matches more;
   - any other case-sensitive leaf (not equals, not contains, comparisons)
     becomes *defined*, and a text comparison compiles to its guard only. With
     no NOT in the tree, every leaf is in a positive position.
2. **Exact pass** on that superset only: for each element, read the value (the
   instance first, then its type) and run the whole original tree with ordinal
   comparison (`TextRules.Matches`, ignoring case unless Aa is on). Non-text
   leaves reuse their native filter per element.

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
- An inverted `ElementParameterFilter` reads with De Morgan: each rule gets its
  negated operator and the rules join with OR. If one rule has no negated
  operator, the whole filter reads as one `UnsupportedRule`.
- "In view" reads the active view's filters; for a link on Revit 2024 and later
  it reads the filters of the link's linked view.

## Live checks

Revit 2026, Snowdon Towers HVAC with the Architectural link, Doors (142
elements), driven through the panel with FlaUI and compared with direct API
counts. All matched.

| Rule | Count |
|------|-------|
| Swing Angle not empty | 131 |
| Swing Angle undefined | 11 |
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
OR over ids, the Defined guard and *undefined*.
