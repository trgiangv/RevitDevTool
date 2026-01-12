using Microsoft.Extensions.Logging;
using RevitDevTool.RichTextBox.Colored.Themes;
using System.Diagnostics;
using System.Globalization;

namespace RevitDevTool.RichTextBox.Colored.Demo;

public partial class Form1 : Form
{
    private ZLoggerRichTextBoxOptions? _options;
    private ZLoggerRichTextBoxLoggerProvider? _provider;
    private ILogger? _logger;
    private Microsoft.Extensions.Logging.ILoggerFactory? _loggerFactory;
    private bool _toolbarsVisible = true;

    public Form1()
    {
        InitializeComponent();
    }

    private void Initialize()
    {
        // Dispose previous provider if exists
        _provider?.Dispose();
        _loggerFactory?.Dispose();

        _options = new ZLoggerRichTextBoxOptions
        {
            Theme = ThemePresets.Literate,
            OutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message}{NewLine}{Exception}",
            FormatProvider = new CultureInfo("en-US"),
            AutoScroll = true,
            MaxLogLines = 1000
        };

        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddZLoggerRichTextBox(richTextBox1, out _provider, options =>
            {
                options.Theme = _options.Theme;
                options.OutputTemplate = _options.OutputTemplate;
                options.FormatProvider = _options.FormatProvider;
                options.AutoScroll = _options.AutoScroll;
                options.MaxLogLines = _options.MaxLogLines;
            });
        });

        _logger = _loggerFactory.CreateLogger("Demo");

        _logger.LogDebug("Started logger.");
        btnDispose.Enabled = true;
    }

    private void Form1_Load(object sender, EventArgs e)
    {
        Initialize();

        _logger?.LogInformation("Application started. Environment: {Environment}, Version: {Version}",
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
            typeof(Form1).Assembly.GetName().Version);

        var apiRequest = new
        {
            Method = "POST",
            Endpoint = "/api/users",
            RequestId = Guid.NewGuid()
        };
        _logger?.LogInformation("API Request: {Request}", apiRequest);

        try
        {
            SimulateDatabaseOperation();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Database operation failed. Connection string: {ConnectionString}",
                "Server=localhost;Trusted_Connection=True;");
        }
    }

    private void CloseAndFlush()
    {
        _logger?.LogDebug("Dispose requested.");
        _provider?.Dispose();
        _loggerFactory?.Dispose();
        _provider = null;
        _loggerFactory = null;
        _logger = null;
    }

    private void btnDebug_Click(object sender, EventArgs e)
    {
        var query = new
        {
            Sql = "SELECT * FROM Users WHERE Status = @status",
            Parameters = new { status = "Active" },
            ExecutionTime = 150 // ms
        };
        _logger?.LogDebug("Database query executed: {Query}", query);
    }

    private void btnError_Click(object sender, EventArgs e)
    {
        try
        {
            throw new InvalidOperationException("Failed to process payment",
                new Exception("Gateway timeout"));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Payment processing failed for OrderId: {OrderId}", Guid.NewGuid());
        }
    }

    private void btnFatal_Click(object sender, EventArgs e)
    {
        try
        {
            throw new OutOfMemoryException("Application memory limit exceeded");
        }
        catch (Exception ex)
        {
            _logger?.LogCritical(ex, "Critical system failure. Memory usage: {MemoryUsage}MB",
                Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024);
        }
    }

    private void btnInformation_Click(object sender, EventArgs e)
    {
        var userAction = new
        {
            UserId = "user123",
            Action = "Login",
            IP = "192.168.1.1",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = DateTime.UtcNow
        };
        _logger?.LogInformation("User activity: {UserAction}", userAction);
    }

    private void btnParallelFor_Click(object sender, EventArgs e)
    {
        for (var stepNumber = 1; stepNumber <= 1000; stepNumber++)
        {
            _logger?.LogTrace("Processing batch item Step {StepNumber:000} - Status: {Status}", stepNumber, "InProgress");
            _logger?.LogDebug("Batch processing metrics for Step {StepNumber:000} - Duration: {Duration}ms", stepNumber, 150);
            _logger?.LogInformation("Completed processing Step {StepNumber:000} - Items processed: {Count}", stepNumber, 1000);
            _logger?.LogWarning("Performance warning for Step {StepNumber:000} - Response time: {ResponseTime}ms", stepNumber, 500);
            _logger?.LogError("Failed to process Step {StepNumber:000} - Error code: {ErrorCode}", stepNumber, "E1001");
            _logger?.LogCritical("Critical failure in Step {StepNumber:000} - System state: {State}", stepNumber, "Unrecoverable");
        }
    }

    private async void btnTaskRun_Click(object sender, EventArgs e)
    {
        var tasks = new List<Task>();

        for (var i = 1; i <= 1000; i++)
        {
            var stepNumber = i;
            var task = Task.Run(() =>
            {
                _logger?.LogTrace("Background task Step {StepNumber:000} - Status: {Status}", stepNumber, "Started");
                _logger?.LogDebug("Background task Step {StepNumber:000} - Thread ID: {ThreadId}", stepNumber, Environment.CurrentManagedThreadId);
                _logger?.LogInformation("Background task Step {StepNumber:000} - Progress: {Progress}%", stepNumber, 75);
                _logger?.LogWarning("Background task Step {StepNumber:000} - Resource usage: {CpuUsage}%", stepNumber, 85);
                _logger?.LogError("Background task Step {StepNumber:000} - Failed with code: {ErrorCode}", stepNumber, "E2001");
                _logger?.LogCritical("Background task Step {StepNumber:000} - System impact: {Impact}", stepNumber, "Critical");
            });

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
    }

    private void btnVerbose_Click(object sender, EventArgs e)
    {
        _logger?.LogTrace("Processing batch item {ItemId} of {TotalItems}", 42, 100);
    }

    private void btnWarning_Click(object sender, EventArgs e)
    {
        _logger?.LogWarning("High memory usage detected: {MemoryUsage}MB (Threshold: {Threshold}MB)",
            Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024, 16);
    }

    private static void SimulateDatabaseOperation()
    {
        throw new Exception("Database connection timeout", new TimeoutException("Network connection lost"));
    }

    private void btnObject_Click(object sender, EventArgs e)
    {
        var systemMetrics = new
        {
            MemoryUsage = 1024.5,
            LastGcCollection = DateTime.UtcNow.AddMinutes(-5)
        };

        _logger?.LogInformation("System metrics: {Metrics}", systemMetrics);
    }

    private void btnScalar_Click(object sender, EventArgs e)
    {
        _logger?.LogInformation("Cache hit ratio: {HitRatio:P2}", 0.856);
        _logger?.LogInformation("Response received: {ResponseTime}", DateTime.Now);
        _logger?.LogInformation("Valid batch size: {IsValid}", true);
        _logger?.LogInformation("API version: {ApiVersion}", "2.1.0");
    }

    private void btnDictionary_Click(object sender, EventArgs e)
    {
        var config = new Dictionary<string, object>
        {
            ["ConnectionTimeout"] = 30,
            ["MaxRetries"] = 3,
            ["EnableCompression"] = true,
            ["AllowedOrigins"] = new[] { "https://api.example.com", "https://admin.example.com" }
        };

        _logger?.LogInformation("Configuration loaded: {Config}", config);
    }

    private void btnStructure_Click(object sender, EventArgs e)
    {
        var auditLog = new
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            User = new
            {
                Id = "user456",
                Role = "Administrator",
                Department = "IT"
            },
            Action = "ConfigurationUpdate"
        };

        _logger?.LogInformation("Audit log entry: {AuditLog}", auditLog);
    }

    private void btnComplex_Click(object sender, EventArgs e)
    {
        var deploymentInfo = new
        {
            DeploymentId = Guid.NewGuid(),
            Environment = "Production",
            Version = "2.1.0",
            Timestamp = DateTime.UtcNow,
            Services = new object[]
            {
                new
                {
                    Name = "API",
                    Status = "Healthy",
                    Metrics = new
                    {
                        ResponseTime = 45,
                        ErrorRate = 0.01,
                        RequestsPerSecond = 150
                    }
                },
                new
                {
                    Name = "Database",
                    Status = "Degraded",
                    Metrics = new
                    {
                        ConnectionCount = 85,
                        QueryTime = 120,
                        ReplicationLag = 5
                    }
                }
            }
        };

        _logger?.LogInformation("Deployment status: {DeploymentInfo}", deploymentInfo);
    }

    private void btnDispose_Click(object sender, EventArgs e)
    {
        CloseAndFlush();
        btnDispose.Enabled = false;
    }

    private void btnReset_Click(object sender, EventArgs e)
    {
        CloseAndFlush();
        Initialize();
    }

    private void btnAutoScroll_Click(object sender, EventArgs e)
    {
        if (_options == null)
        {
            return;
        }

        _options.AutoScroll = !_options.AutoScroll;
        btnAutoScroll.Text = _options.AutoScroll ? "Disable Auto Scroll" : "Enable Auto Scroll";
    }

    private void Form1_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.T)
        {
            _toolbarsVisible = !_toolbarsVisible;
            toolStrip1.Visible = _toolbarsVisible;
            toolStrip2.Visible = _toolbarsVisible;
        }
    }

    private void btnClear_Click(object sender, EventArgs e)
    {
        _provider?.Processor.Clear();
    }

    private void btnRestore_Click(object sender, EventArgs e)
    {
        _provider?.Processor.Restore();
    }
}
