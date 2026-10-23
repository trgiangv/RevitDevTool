using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace DevTools.Mcp.Client.Tests;

[TestClass]
public sealed class ProcessCatalogsTests
{
    [TestMethod]
    public void ReplaceRemoveClear_ManageCatalogs()
    {
        var catalogs = new ProcessCatalogs();
        var catalog = CreateCatalog(100, "ping", "sample://demo/status");

        catalogs.Replace(catalog);
        Assert.HasCount(1, catalogs.List());

        Assert.IsTrue(catalogs.Remove(100));
        Assert.IsEmpty(catalogs.List());

        catalogs.Replace(catalog);
        catalogs.Clear();
        Assert.IsEmpty(catalogs.List());
    }

    [TestMethod]
    public void Search_WithQuery_RanksATokenHitAndDropsUnknownTokens()
    {
        var catalog = new ProcessCatalogs();
        catalog.Replace(CreateCatalog(100, "read_file_info", "sample://a", description: "metadata reader"));
        catalog.Replace(CreateCatalog(200, "launch_host", "sample://b", description: "start host"));

        var strong = catalog.Search("read_file_info");
        Assert.HasCount(1, strong);
        Assert.AreEqual("read_file_info", strong[0].Item.Target);

        var weak = catalog.Search("zzz aaa");
        Assert.IsEmpty(weak);
    }

    [TestMethod]
    public void Search_UsesInputSchemaDescriptionsToFindCapability()
    {
        var catalog = new ProcessCatalogs();
        catalog.Replace(CreateCatalogWithSchema(
            100,
            "inspect_door",
            "Inspect model data",
            new { fireResistance = new { type = "string", description = "Fire resistance rating for a door" } }));
        catalog.Replace(CreateCatalogWithSchema(
            200,
            "inspect_wall",
            "Inspect model data",
            new { finish = new { type = "string", description = "Wall surface finish" } }));

        var matches = catalog.Search("fire resistance", [CatalogType.Tool], limit: 8);

        Assert.IsNotEmpty(matches);
        Assert.AreEqual("inspect_door", matches[0].Item.Target);
    }

    [TestMethod]
    public void Search_ResolvesNestedReferencedSchemasBeforeRanking()
    {
        const string schemaJson = """
            {
              "type": "object",
              "$defs": {
                "FilterSpec": {
                  "properties": {
                    "filters": {
                      "items": {
                        "oneOf": [
                          { "$ref": "#/$defs/CategoryFilter" },
                          { "$ref": "#/$defs/ClassFilter" }
                        ]
                      }
                    }
                  }
                },
                "CategoryFilter": {
                  "title": "CategoryFilter",
                  "description": "Filter elements by Revit category display names.",
                  "properties": {
                    "names": {
                      "description": "Category names, for example Walls, Doors, and Windows.",
                      "items": { "type": "string" }
                    }
                  }
                },
                "ClassFilter": {
                  "description": "Filter elements by Revit API class name such as Wall or Floor.",
                  "properties": {
                    "class_names": { "type": "array", "items": { "type": "string" } }
                  }
                }
              },
              "properties": {
                "filters": { "anyOf": [{ "$ref": "#/$defs/FilterSpec" }, { "type": "null" }] }
              }
            }
            """;
        using var schemaDocument = JsonDocument.Parse(schemaJson);
        var schema = schemaDocument.RootElement.Clone();
        var catalog = new ProcessCatalogs();
        catalog.Replace(new ProcessCatalog
        {
            ProcessId = 100,
            Instance = new InstanceInfo { HostApp = "Revit", ProcessId = 100, VersionNumber = "2025" },
            PipeName = HostPipeName.FormatMcp("Revit", "2025", 100),
            Tools =
            [
                CreateTool("revit_find_elements", "Structured element search with composable FilterSpec.", schema),
                CreateTool("revit_color_by_parameter", "Color splash by param value.", JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new { category_name = new { type = "string" }, parameter_name = new { type = "string" } },
                })),
            ],
            Resources = [],
            ResourceTemplates = [],
        });

        var matches = catalog.Search("find walls by category", [CatalogType.Tool]);

        Assert.IsNotEmpty(matches);
        Assert.AreEqual("revit_find_elements", matches[0].Item.Target);
    }

    [TestMethod]
    public void Search_UsesGenericParameterFilterForAttributeConstraints()
    {
        const string schemaJson = """
            {
              "type": "object",
              "$defs": {
                "CategoryFilter": {
                  "properties": {
                    "category_names": { "type": "array", "items": { "enum": ["Doors", "Walls"] } }
                  }
                },
                "ParameterFilter": {
                  "properties": {
                    "parameter_name": { "type": "string" },
                    "value": { "type": "string" }
                  }
                }
              },
              "properties": {
                "filters": {
                  "type": "array",
                  "items": { "oneOf": [
                    { "$ref": "#/$defs/CategoryFilter" },
                    { "$ref": "#/$defs/ParameterFilter" }
                  ] }
                }
              }
            }
            """;
        using var schemaDocument = JsonDocument.Parse(schemaJson);
        var schema = schemaDocument.RootElement.Clone();
        var catalog = new ProcessCatalogs();
        catalog.Replace(new ProcessCatalog
        {
            ProcessId = 100,
            Instance = new InstanceInfo { HostApp = "Revit", ProcessId = 100, VersionNumber = "2025" },
            PipeName = HostPipeName.FormatMcp("Revit", "2025", 100),
            Tools =
            [
                CreateTool("revit_find_elements", "Structured element search with composable filters.", schema),
                CreateTool("execute_csharp_code", "Execute a C# command"),
                CreateTool("execute_python_code", "Execute a Python script"),
            ],
            Resources = [],
            ResourceTemplates = [],
        });

        var matches = catalog.Search("show doors with fire rating", [CatalogType.Tool]);

        Assert.HasCount(1, matches);
        Assert.AreEqual("revit_find_elements", matches[0].Item.Target);
    }

    [TestMethod]
    public void Search_PrefersHigherQueryCoverageAndKeepsActionSpecificScheduleMatches()
    {
        var catalog = new ProcessCatalogs();
        catalog.Replace(new ProcessCatalog
        {
            ProcessId = 100,
            Instance = new InstanceInfo { HostApp = "Revit", ProcessId = 100, VersionNumber = "2025" },
            PipeName = HostPipeName.FormatMcp("Revit", "2025", 100),
            Tools =
            [
                CreateTool("revit_find_elements", "Find elements by category and filter."),
                CreateTool("revit_move_elements", "Translate elements by vector."),
                CreateTool("revit_preview_schedule", "Preview a schedule."),
                CreateTool("revit_export_schedule", "Export a schedule."),
                CreateTool("revit_create_schedule", "Create a schedule."),
            ],
            Resources = [],
            ResourceTemplates = [],
        });

        var filtered = catalog.Search("filter elements by category", [CatalogType.Tool]);
        Assert.AreEqual("revit_find_elements", filtered[0].Item.Target);

        var broadSchedule = catalog.Search("schedule", [CatalogType.Tool]);
        Assert.HasCount(3, broadSchedule);
        var preview = catalog.Search("preview schedule", [CatalogType.Tool]);
        var export = catalog.Search("export schedule", [CatalogType.Tool]);
        Assert.AreEqual("revit_preview_schedule", preview[0].Item.Target);
        Assert.AreEqual("revit_export_schedule", export[0].Item.Target);
    }

    [TestMethod]
    public void Search_PrefersSpecializedCapabilitiesAndFallsBackToCodeExecution()
    {
        var catalog = new ProcessCatalogs();
        catalog.Replace(new ProcessCatalog
        {
            ProcessId = 100,
            Instance = new InstanceInfo { HostApp = "Revit", ProcessId = 100, VersionNumber = "2025" },
            PipeName = HostPipeName.FormatMcp("Revit", "2025", 100),
            Tools =
            [
                CreateTool("revit_create_schedule", "Create a schedule"),
                CreateTool("revit_create_view", "Create a view"),
                CreateTool("execute_csharp_code", "Execute a C# command"),
                CreateTool("execute_python_code", "Execute a Python script"),
            ],
            Resources = [],
            ResourceTemplates = [],
        });

        var fallback = catalog.Search("create wall", [CatalogType.Tool]);
        Assert.HasCount(2, fallback);
        Assert.IsTrue(fallback.All(match => match.Item.Target.StartsWith("execute_", StringComparison.Ordinal)));

        var specialized = catalog.Search("schedule", [CatalogType.Tool]);
        Assert.AreEqual("revit_create_schedule", specialized[0].Item.Target);

        var explicitExecution = catalog.Search("execute python", [CatalogType.Tool]);
        Assert.AreEqual("execute_python_code", explicitExecution[0].Item.Target);
    }

    [TestMethod]
    public void Search_EmptyQuery_ReturnsNothing()
    {
        var catalog = new ProcessCatalogs();
        catalog.Replace(CreateCatalog(100, "ping", "sample://a"));

        Assert.IsEmpty(catalog.Search(null));
        Assert.IsEmpty(catalog.Search("  "));
    }

    [TestMethod]
    public void Search_FiltersByProcessId()
    {
        var catalog = new ProcessCatalogs();
        catalog.Replace(CreateCatalog(100, "ping", "sample://a"));
        catalog.Replace(CreateCatalog(200, "ping", "sample://b"));

        var matches = catalog.Search("ping", processId: 100);

        Assert.HasCount(1, matches);
        Assert.AreEqual(100, matches[0].ProcessId);
    }

    [TestMethod]
    public void Find_ReturnsTheItemForThatProcess()
    {
        var catalog = new ProcessCatalogs();
        catalog.Replace(CreateCatalog(100, "ping", "sample://a/ping"));
        catalog.Replace(CreateCatalog(200, "ping", "sample://b/ping"));

        var notFound = catalog.Find(CatalogType.Tool, "missing", 100);
        Assert.IsNull(notFound);

        var found = catalog.Find(CatalogType.Resource, "sample://a/ping", 100);
        Assert.IsNotNull(found);
        Assert.AreEqual("sample://a/ping", found.Target);

        var otherProcess = catalog.Find(CatalogType.Tool, "ping", 100);
        Assert.AreEqual(100, otherProcess!.ProcessId);
    }

    private static ProcessCatalog CreateCatalog(
        int processId,
        string toolName,
        string resourceUri,
        string? description = null) => new()
        {
            ProcessId = processId,
            Instance = new InstanceInfo { HostApp = "Revit", ProcessId = processId, VersionNumber = "2025" },
            PipeName = HostPipeName.FormatMcp("Revit", "2025", processId),
            Tools =
        [
            new Tool
            {
                Name = toolName,
                Description = description ?? $"{toolName} description",
                InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }),
            },
        ],
            Resources =
        [
            new Resource
            {
                Name = "demo_resource",
                Uri = resourceUri,
                Description = "Demo resource",
            },
        ],
            ResourceTemplates =
        [
            new ResourceTemplate
            {
                Name = "demo_template",
                UriTemplate = "sample://{id}",
                Description = "Demo template",
            },
        ],
        };

    private static Tool CreateTool(string name, string description, JsonElement? inputSchema = null) => new()
    {
        Name = name,
        Description = description,
        InputSchema = inputSchema ?? JsonSerializer.SerializeToElement(new { type = "object" }),
    };

    private static ProcessCatalog CreateCatalogWithSchema(
        int processId,
        string toolName,
        string description,
        object properties)
    {
        var catalog = CreateCatalog(processId, toolName, "sample://" + toolName, description);
        return new ProcessCatalog
        {
            ProcessId = catalog.ProcessId,
            Instance = catalog.Instance,
            PipeName = catalog.PipeName,
            Tools =
            [
                new Tool
                {
                    Name = toolName,
                    Description = description,
                    InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties }),
                },
            ],
            Resources = catalog.Resources,
            ResourceTemplates = catalog.ResourceTemplates,
        };
    }
}
