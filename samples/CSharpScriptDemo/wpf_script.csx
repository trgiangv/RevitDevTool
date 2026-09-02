#r "C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/8.0.27/ref/net8.0/System.Runtime.dll"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPI.dll"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPIUI.dll"
#load "./Helpers/DocumentHelper.csx"

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using UIFramework;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfDataGrid = System.Windows.Controls.DataGrid;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfTextBox = System.Windows.Controls.TextBox;

public record Employee(int Id, string Name, string Department, int Salary);

public sealed class FlaUIPlaygroundWindow : Window
{
    private readonly UIApplication _uiApp;
    private readonly ObservableCollection<string> _fruits =
    [
        "Apple",
        "Banana",
        "Cherry",
        "Dragon Fruit",
        "Elderberry"
    ];

    private WpfTextBox _nameTextBox = null!;
    private WpfTextBox _notesTextBox = null!;
    private WpfCheckBox _agreeCheckBox = null!;
    private WpfComboBox _cityComboBox = null!;
    private WpfTextBlock _selectedCityText = null!;
    private WpfListBox _fruitListBox = null!;
    private WpfDataGrid _employeeDataGrid = null!;
    private WpfTextBlock _outputTextBlock = null!;
    private WpfTextBlock _statusTextBlock = null!;

    public FlaUIPlaygroundWindow(UIApplication uiApp)
    {
        _uiApp = uiApp;
        Title = "UI Automation Test (Modeless)";
        Width = 960;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = MainWindow.getMainWnd();

        Content = BuildRoot();
        Loaded += OnLoaded;
    }

    private UIElement BuildRoot()
    {
        const string xaml = """
            <DockPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Menu DockPanel.Dock="Top">
                    <MenuItem x:Name="MenuFile" Header="_File">
                        <MenuItem x:Name="MenuNew" Header="_New" AutomationProperties.Name="MenuNew" />
                        <MenuItem x:Name="MenuSave" Header="_Save" AutomationProperties.Name="MenuSave" />
                        <Separator />
                        <MenuItem x:Name="MenuExit" Header="E_xit" AutomationProperties.Name="MenuExit" />
                    </MenuItem>
                    <MenuItem x:Name="MenuHelp" Header="_Help">
                        <MenuItem x:Name="MenuAbout" Header="_About" AutomationProperties.Name="MenuAbout" />
                    </MenuItem>
                </Menu>
                <StatusBar DockPanel.Dock="Bottom">
                    <StatusBarItem>
                        <TextBlock x:Name="StatusTextBlock" AutomationProperties.Name="StatusTextBlock" Text="Ready" />
                    </StatusBarItem>
                </StatusBar>
                <ScrollViewer VerticalScrollBarVisibility="Auto">
                    <StackPanel Margin="16">
                        <TextBlock FontSize="20" FontWeight="Bold" Text="Revit FlaUI MCP Control Playground" />
                        <GroupBox Margin="0,12,0,0" Header="Text Input">
                            <Grid Margin="8">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="140" />
                                    <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto" />
                                    <RowDefinition Height="Auto" />
                                    <RowDefinition Height="Auto" />
                                    <RowDefinition Height="Auto" />
                                </Grid.RowDefinitions>
                                <TextBlock Grid.Row="0" Grid.Column="0" VerticalAlignment="Center" Text="Full Name:" />
                                <TextBox x:Name="NameTextBox" Grid.Row="0" Grid.Column="1" Margin="0,4"
                                         AutomationProperties.Name="NameTextBox" Text="Initial value" />
                                <TextBlock Grid.Row="1" Grid.Column="0" VerticalAlignment="Center" Text="Notes (append):" />
                                <TextBox x:Name="NotesTextBox" Grid.Row="1" Grid.Column="1" Margin="0,4"
                                         AutomationProperties.Name="NotesTextBox" Text="Start typing here" />
                                <TextBlock Grid.Row="2" Grid.Column="0" VerticalAlignment="Center" Text="Password:" />
                                <PasswordBox x:Name="PasswordBox" Grid.Row="2" Grid.Column="1" Margin="0,4"
                                               AutomationProperties.Name="PasswordBox" />
                                <TextBlock Grid.Row="3" Grid.Column="0" VerticalAlignment="Center" Text="Read-only:" />
                                <TextBox x:Name="ReadOnlyTextBox" Grid.Row="3" Grid.Column="1" Margin="0,4"
                                         AutomationProperties.Name="ReadOnlyTextBox" IsReadOnly="True"
                                         Text="This text is read-only" />
                            </Grid>
                        </GroupBox>
                        <GroupBox Margin="0,12,0,0" Header="Buttons and Toggles">
                            <WrapPanel Margin="8">
                                <Button x:Name="PrimaryButton" Width="140" Margin="0,0,8,8"
                                        AutomationProperties.Name="PrimaryButton" Content="Click Me" />
                                <Button x:Name="SecondaryButton" Width="140" Margin="0,0,8,8"
                                        AutomationProperties.Name="SecondaryButton" Content="Secondary" />
                                <CheckBox x:Name="AgreeCheckBox" Margin="0,0,16,8"
                                          AutomationProperties.Name="AgreeCheckBox" Content="I agree" />
                                <RadioButton x:Name="OptionA" Margin="0,0,8,8"
                                             AutomationProperties.Name="OptionA" Content="Option A" GroupName="Options" />
                                <RadioButton x:Name="OptionB" Margin="0,0,8,8"
                                             AutomationProperties.Name="OptionB" Content="Option B" GroupName="Options" />
                            </WrapPanel>
                        </GroupBox>
                        <GroupBox Margin="0,12,0,0" Header="ComboBox">
                            <StackPanel Margin="8">
                                <ComboBox x:Name="CityComboBox" Width="240" HorizontalAlignment="Left"
                                          AutomationProperties.Name="CityComboBox">
                                    <ComboBoxItem Content="Hanoi" />
                                    <ComboBoxItem Content="Ho Chi Minh City" />
                                    <ComboBoxItem Content="Da Nang" />
                                    <ComboBoxItem Content="Can Tho" />
                                </ComboBox>
                                <TextBlock x:Name="SelectedCityText" Margin="0,8,0,0"
                                           AutomationProperties.Name="SelectedCityText" Text="Selected city: (none)" />
                            </StackPanel>
                        </GroupBox>
                        <GroupBox Margin="0,12,0,0" Header="ListBox">
                            <Grid Margin="8">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <ListBox x:Name="FruitListBox" Height="120" AutomationProperties.Name="FruitListBox" />
                                <StackPanel Grid.Column="1" Margin="12,0,0,0" VerticalAlignment="Top">
                                    <Button x:Name="AddFruitButton" Width="100" Margin="0,0,0,8"
                                            AutomationProperties.Name="AddFruitButton" Content="Add Fruit" />
                                    <Button x:Name="RemoveFruitButton" Width="100"
                                            AutomationProperties.Name="RemoveFruitButton" Content="Remove" />
                                </StackPanel>
                            </Grid>
                        </GroupBox>
                        <GroupBox Margin="0,12,0,0" Header="DataGrid">
                            <DataGrid x:Name="EmployeeDataGrid" Height="160" Margin="8" AutoGenerateColumns="False"
                                      AutomationProperties.Name="EmployeeDataGrid" CanUserAddRows="False" IsReadOnly="True">
                                <DataGrid.Columns>
                                    <DataGridTextColumn Binding="{Binding Id}" Header="ID" Width="50" />
                                    <DataGridTextColumn Binding="{Binding Name}" Header="Name" Width="*" />
                                    <DataGridTextColumn Binding="{Binding Department}" Header="Department" Width="120" />
                                    <DataGridTextColumn Binding="{Binding Salary}" Header="Salary" Width="90" />
                                </DataGrid.Columns>
                            </DataGrid>
                        </GroupBox>
                        <GroupBox Margin="0,12,0,0" Header="Output">
                            <TextBlock x:Name="OutputTextBlock" Margin="8" AutomationProperties.Name="OutputTextBlock"
                                       Text="Output will appear here after interactions." TextWrapping="Wrap" />
                        </GroupBox>
                    </StackPanel>
                </ScrollViewer>
            </DockPanel>
            """;

        return (UIElement)XamlReader.Parse(xaml);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var root = (FrameworkElement)Content!;

        _nameTextBox = Require<WpfTextBox>(root, "NameTextBox");
        _notesTextBox = Require<WpfTextBox>(root, "NotesTextBox");
        _agreeCheckBox = Require<WpfCheckBox>(root, "AgreeCheckBox");
        _cityComboBox = Require<WpfComboBox>(root, "CityComboBox");
        _selectedCityText = Require<WpfTextBlock>(root, "SelectedCityText");
        _fruitListBox = Require<WpfListBox>(root, "FruitListBox");
        _employeeDataGrid = Require<WpfDataGrid>(root, "EmployeeDataGrid");
        _outputTextBlock = Require<WpfTextBlock>(root, "OutputTextBlock");
        _statusTextBlock = Require<WpfTextBlock>(root, "StatusTextBlock");

        var readOnlyTextBox = Require<WpfTextBox>(root, "ReadOnlyTextBox");
        readOnlyTextBox.Text = $"Revit doc: {_uiApp.ActiveUIDocument?.Document?.Title ?? "(none)"}";

        _fruitListBox.ItemsSource = _fruits;
        _employeeDataGrid.ItemsSource = CreateEmployees();
        _cityComboBox.SelectedIndex = 0;

        Require<System.Windows.Controls.Button>(root, "PrimaryButton").Click += PrimaryButton_Click;
        Require<System.Windows.Controls.Button>(root, "SecondaryButton").Click += (_, _) => UpdateOutput("Secondary button clicked.");
        _agreeCheckBox.Checked += AgreeCheckBox_Changed;
        _agreeCheckBox.Unchecked += AgreeCheckBox_Changed;
        Require<WpfRadioButton>(root, "OptionA").Checked += Option_Changed;
        Require<WpfRadioButton>(root, "OptionB").Checked += Option_Changed;
        _cityComboBox.SelectionChanged += CityComboBox_SelectionChanged;
        _fruitListBox.SelectionChanged += FruitListBox_SelectionChanged;
        Require<System.Windows.Controls.Button>(root, "AddFruitButton").Click += AddFruitButton_Click;
        Require<System.Windows.Controls.Button>(root, "RemoveFruitButton").Click += RemoveFruitButton_Click;
        _employeeDataGrid.SelectionChanged += EmployeeDataGrid_SelectionChanged;
        Require<System.Windows.Controls.MenuItem>(root, "MenuNew").Click += (_, _) => UpdateOutput("Menu: New clicked.");
        Require<System.Windows.Controls.MenuItem>(root, "MenuSave").Click += (_, _) => UpdateOutput("Menu: Save clicked.");
        Require<System.Windows.Controls.MenuItem>(root, "MenuAbout").Click += (_, _) => UpdateOutput("Menu: About clicked.");
        Require<System.Windows.Controls.MenuItem>(root, "MenuExit").Click += (_, _) => Close();

        UpdateOutput("Modeless WPF window loaded from wpf_script.csx.");
    }

    private static T Require<T>(FrameworkElement root, string name) where T : class
    {
        return (root.FindName(name) as T)
               ?? throw new System.Exception($"Could not find '{name}' ({typeof(T).Name}).");
    }

    private static List<Employee> CreateEmployees() =>
    [
        new(1, "Alice Nguyen", "Engineering", 85000),
        new(2, "Bob Tran", "Sales", 62000),
        new(3, "Carol Le", "Marketing", 71000),
        new(4, "David Pham", "Engineering", 92000),
        new(5, "Eva Hoang", "HR", 58000)
    ];

    private void UpdateOutput(string message)
    {
        _outputTextBlock.Text = message;
        _statusTextBlock.Text = message;
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _uiApp.ActiveUIDocument?.Document;
        var name = _nameTextBox.Text.Trim();
        var city = (_cityComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "(none)";
        var wallCount = doc == null ? 0 : DocumentHelper.CountElements<Wall>(doc);
        var project = doc == null ? "(no document)" : DocumentHelper.GetProjectInfo(doc);
        UpdateOutput($"Primary clicked. Name='{name}', City='{city}', Agree={_agreeCheckBox.IsChecked == true}, Walls={wallCount}\n{project}");
    }

    private void AgreeCheckBox_Changed(object sender, RoutedEventArgs e) =>
        UpdateOutput($"Agree checkbox is now: {_agreeCheckBox.IsChecked == true}");

    private void Option_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is WpfRadioButton radio)
            UpdateOutput($"Selected option: {radio.Content}");
    }

    private void CityComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var city = (_cityComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "(none)";
        _selectedCityText.Text = $"Selected city: {city}";
        UpdateOutput($"ComboBox changed to: {city}");
    }

    private void FruitListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var fruit = _fruitListBox.SelectedItem?.ToString() ?? "(none)";
        UpdateOutput($"ListBox selection: {fruit}");
    }

    private void AddFruitButton_Click(object sender, RoutedEventArgs e)
    {
        var newFruit = $"Fruit {_fruits.Count + 1}";
        _fruits.Add(newFruit);
        _fruitListBox.SelectedItem = newFruit;
        UpdateOutput($"Added fruit: {newFruit}");
    }

    private void RemoveFruitButton_Click(object sender, RoutedEventArgs e)
    {
        if (_fruitListBox.SelectedItem is string selected)
        {
            _fruits.Remove(selected);
            UpdateOutput($"Removed fruit: {selected}");
        }
        else
        {
            UpdateOutput("No fruit selected to remove.");
        }
    }

    private void EmployeeDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_employeeDataGrid.SelectedItem is Employee employee)
            UpdateOutput($"DataGrid row selected: {employee.Name} ({employee.Department})");
    }
}

[Transaction(TransactionMode.Manual)]
public class WpfScriptCmd : IExternalCommand
{
    private static FlaUIPlaygroundWindow? _window;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (_window != null)
        {
            if (_window.IsVisible)
            {
                _window.Activate();
                return Result.Succeeded;
            }

            _window = null;
        }

        _window = new FlaUIPlaygroundWindow(commandData.Application);
        _window.Closed += (_, _) => _window = null;
        _window.Show();

        return Result.Succeeded;
    }
}
