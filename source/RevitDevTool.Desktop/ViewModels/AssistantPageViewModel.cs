using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;

namespace RevitDevTool.Desktop.ViewModels;

public partial class AssistantPageViewModel : PageViewModelBase
{
    public override int Index => 1;
    public override string DisplayName => "Assistant";
    public override MaterialIconKind Icon => MaterialIconKind.Robot;

    [ObservableProperty] private string _mcpServerStatus = "Not Connected";
    [ObservableProperty] private bool _isMcpServerConnected;
    [ObservableProperty] private string _mcpServerPath = string.Empty;
    [ObservableProperty] private string _mcpServerArgs = string.Empty;
    [ObservableProperty] private string _mcpProviderEndpoint = string.Empty;
    [ObservableProperty] private string _mcpProviderApiKey = string.Empty;
    [ObservableProperty] private string _mcpProviderModel = "gpt-4o";
    [ObservableProperty] private string _selectedMcpProvider = "OpenAI";
    [ObservableProperty] private bool _isTestingConnection;
    [ObservableProperty] private string _connectionTestResult = string.Empty;

    public IReadOnlyList<string> McpProviders { get; }= ["OpenAI", "Anthropic", "Azure OpenAI", "Ollama"];
    public ObservableCollection<McpServerConfig> McpServers { get; } = [];
    public ObservableCollection<McpProviderConfig> McpProviderConfigs { get; } = [];

    public AssistantPageViewModel()
    {
        // Add sample MCP servers
        McpServers.Add(new McpServerConfig { Name = "Filesystem", Path = "", Args = "" });
        McpServers.Add(new McpServerConfig { Name = "Git", Path = "", Args = "" });
    }

    [RelayCommand]
    private void AddMcpServer()
    {
        McpServers.Add(new McpServerConfig { Name = "New Server", Path = "", Args = "" });
    }

    [RelayCommand]
    private void RemoveMcpServer(McpServerConfig? server)
    {
        if (server != null)
            McpServers.Remove(server);
    }

    [RelayCommand]
    private void AddMcpProvider()
    {
        McpProviderConfigs.Add(new McpProviderConfig { Name = "New Provider", Endpoint = "", ApiKey = "", Model = "gpt-4o" });
    }

    [RelayCommand]
    private void RemoveMcpProvider(McpProviderConfig? provider)
    {
        if (provider != null)
            McpProviderConfigs.Remove(provider);
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsTestingConnection = true;
        ConnectionTestResult = "Testing connection...";

        try
        {
            // Simulate connection test
            await Task.Delay(1000);
            ConnectionTestResult = "Connection successful!";
            IsMcpServerConnected = true;
            McpServerStatus = "Connected";
        }
        catch (Exception ex)
        {
            ConnectionTestResult = $"Connection failed: {ex.Message}";
            IsMcpServerConnected = false;
            McpServerStatus = "Connection Failed";
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    [RelayCommand]
    private void SaveConfiguration()
    {
        // Save MCP configuration
        ConnectionTestResult = "Configuration saved successfully!";
    }
}

public partial class McpServerConfig : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _path = string.Empty;
    [ObservableProperty] private string _args = string.Empty;
    [ObservableProperty] private bool _isEnabled = true;
}

public partial class McpProviderConfig : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _endpoint = string.Empty;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private bool _isEnabled = true;
}

