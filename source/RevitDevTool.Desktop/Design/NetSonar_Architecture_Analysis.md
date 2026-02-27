# NetSonar Architecture Analysis - Lessons for RevitDevTool.Processor

This document analyzes the architecture of the NetSonar Avalonia application and provides insights for improving the RevitDevTool.Processor application.

---

## 1. Dependencies & Packages

### 1.1 NetSonar Package Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Avalonia | 11.3.x | Core UI framework |
| SukiUI | 6.0.4-nightly20260124 | UI Theme & Components |
| Material.Icons.Avalonia | 3.0.0-preview6 | Icon library |
| CommunityToolkit.Mvvm | 8.4.0 | MVVM framework |
| Microsoft.Extensions.DependencyInjection | 10.0.2 | DI container |
| LiveChartsCore.SkiaSharpView.Avalonia | 2.0.0-rc6.1 | Charts |
| MintPlayer.ObservableCollection | 10.0.0 | Enhanced collections |
| ObservableCollections | 3.3.4 | Observable collection extensions |
| ZLogger | 2.5.10 | Structured logging |
| ProcessX | 1.5.6 | Process management |

### 1.2 RevitDevTool.Processor Current Dependencies

| Package | Version | Status |
|---------|---------|--------|
| Avalonia | 11.3.x | ✅ Match |
| SukiUI | 6.0.4-nightly20260219 | ✅ Similar |
| IconPacks.Avalonia | 1.3.1 | ❌ Should migrate to Material.Icons |
| CommunityToolkit.Mvvm | 8.x | ✅ Match |
| Serilog | - | Different from ZLogger |

### 1.3 Key Insights - Dependencies

1. **Material.Icons.Avalonia over IconPacks**: NetSonar uses Material.Icons which integrates better with SukiUI and provides cleaner XAML syntax (`{icon:MaterialIconExt Kind=...}` vs `{iconPacks:PackIconCodicons Kind=...}`)

2. **Observable Collections**: Uses `MintPlayer.ObservableCollection` and `ObservableCollections` for enhanced collection operations with UI synchronization

3. **Logging**: ZLogger provides structured logging with good performance

---

## 2. UI/UX Design Patterns

### 2.1 SukiUI Component Usage

NetSonar demonstrates extensive SukiUI component usage:

```xaml
<!-- GlassCard - Main container with glass effect -->
<suki:GlassCard Margin="15,15,15,10">
    
    <!-- GroupBox - Section with header -->
    <suki:GroupBox>
        <suki:GroupBox.Header>
            <!-- Header content -->
        </suki:GroupBox.Header>
        
        <!-- Content -->
    </suki:GroupBox>
    
</suki:GlassCard>
```

### 2.2 Key SukiUI Components Used in NetSonar

| Component | Usage |
|-----------|-------|
| `GlassCard` | Main content containers with blur/glass effect |
| `GroupBox` | Section containers with headers |
| `CircleProgressBar` | Progress indication |
| `RadialGauge` | Speed/bandwidth visualization |
| `SukiSideMenu` | Navigation menu |
| `SukiWindow` | Main window base |
| `SukiToastHost` | Toast notifications |
| `SukiDialogHost` | Dialog management |
| `SukiMessageBoxHost` | Message boxes |
| `TextBoxExtensions.AddDeleteButton` | Input with clear button |
| `NumericUpDownExtensions.Unit` | Unit display for numeric inputs |

### 2.3 Navigation Pattern - SukiSideMenu

NetSonar uses `SukiSideMenu` for navigation between pages:

```xaml
<suki:SukiSideMenu>
    <suki:SukiSideMenuItem Content="Speed Test" 
                          Icon="{icon:MaterialIconExt Kind=Radar}"
                          TargetPage="{Binding SpeedTestPage}" />
    <suki:SukiSideMenuItem Content="Services" 
                          Icon="{icon:MaterialIconExt Kind=Web}"
                          TargetPage="{Binding PingableServicesPage}" />
</suki:SukiSideMenu>
```

### 2.4 Theme Configuration

NetSonar configures SukiUI theme in App.axaml:

```xml
<Application>
    <Application.Styles>
        <suki:SukiTheme ThemeColor="Blue" />
        <materialIcons:MaterialIconStyles />
        <StyleInclude Source="avares://NetSonar.Avalonia/Assets/Styles/AppStyles.axaml" />
    </Application.Styles>
</Application>
```

### 2.5 Custom Styling Approach

NetSonar uses theme-aware styling with `ResourceDictionary.ThemeDictionaries`:

```xml
<Styles.Resources>
    <ResourceDictionary>
        <ResourceDictionary.ThemeDictionaries>
            <ResourceDictionary x:Key="Light">
                <SolidColorBrush x:Key="AppSuccessColor" Color="DarkGreen" />
            </ResourceDictionary>
            <ResourceDictionary x:Key="Dark">
                <SolidColorBrush x:Key="AppSuccessColor" Color="Green" />
            </ResourceDictionary>
        </ResourceDictionary.ThemeDictionaries>
    </ResourceDictionary>
</Styles.Resources>
```

### 2.6 Icon Patterns

**Material.Icons Syntax** (NetSonar):
```xaml
<icon:MaterialIcon Kind="PlayCircleOutline" />
<icon:MaterialIconText Kind="Radar" Text="Ping" />
<Button Content="{icon:MaterialIconExt Kind=PlayCircleOutline}" />
```

---

## 3. Architecture & Patterns

### 3.1 MVVM Implementation

NetSonar uses a robust MVVM hierarchy:

```
ViewModelBase (with validation, toast/dialog support)
    ├── PageViewModelBase (with page metadata)
    │     └── SpeedTestPageModel
    └── DialogViewModelBase (for dialogs)
```

**ViewModelBase Features:**
- `ObservableValidatorExtended` - Enhanced validation
- Toast/Dialog management access
- Clipboard operations with toast
- URI launching
- Lifecycle hooks (`OnInitialized`, `OnLoaded`, `OnUnloaded`)

### 3.2 Custom Control Base Classes

**UserControlBase:**
```csharp
public partial class UserControlBase : UserControl
{
    protected override Type StyleKeyOverride => typeof(UserControl);
    
    protected override void OnInitialized() { }
    protected override void OnLoaded(RoutedEventArgs e) { }
    protected override void OnUnloaded(RoutedEventArgs e) { }
    
    [RelayCommand]
    public static Task CopyToClipboard(object? obj) { }
}
```

**SukiWindowExtended:**
```csharp
public class SukiWindowExtended : SukiWindow
{
    protected override Type StyleKeyOverride => typeof(SukiWindow);
    
    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is IDisposable disposable)
            disposable.Dispose();
    }
}
```

### 3.3 Service/DI Pattern

NetSonar uses Microsoft.Extensions.DependencyInjection:

```csharp
// In App.axaml.cs
var services = new ServiceCollection();
// Register services
services.AddSingleton<MainViewModel>();
services.AddSingleton<MainWindow>();
var provider = services.BuildServiceProvider();
```

### 3.4 Window Configuration

NetSonar demonstrates proper SukiWindow setup with Toast/Dialog hosts:

```xml
<suki:SukiWindow>
    <suki:SukiWindow.Hosts>
        <suki:SukiToastHost Manager="{Binding ToastManager}" />
        <suki:SukiDialogHost Manager="{Binding DialogManager}" />
    </suki:SukiWindow.Hosts>
</suki:SukiWindow>
```

---

## 4. Key Insights for RevitDevTool.Processor

### 4.1 Immediate Improvements

| Improvement | Priority | Impact |
|-------------|----------|--------|
| Migrate to Material.Icons | HIGH | Cleaner XAML, better SukiUI integration |
| Add Toast/Dialog hosts | HIGH | Better user feedback |
| Use GlassCard containers | MEDIUM | Modern look |
| Add GroupBox for sections | MEDIUM | Better organization |
| Create base ViewModel class | MEDIUM | Code reuse |

### 4.2 Architecture Patterns to Adopt

1. **ViewModelBase with lifecycle hooks**
2. **Custom UserControlBase**
3. **Dialog/Toast management in base class**
4. **Theme-aware custom styles**
5. **ObservableCollections for reactive UI**

### 4.3 Recommended Project Structure

```
RevitDevTool.Processor/
├── Controls/
│   ├── UserControlBase.cs
│   └── WindowBase.cs
├── ViewModels/
│   ├── ViewModelBase.cs       # New - base with toast/dialog
│   └── MainWindowViewModel.cs # Extend base
├── Views/
│   └── *.axaml               # Use GlassCard, GroupBox
├── Assets/
│   └── Styles/
│       └── AppStyles.axaml   # Custom styles
└── App.axaml                 # Configure SukiTheme + hosts
```

---

## 5. Gap Analysis: Current vs Best Practices

### 5.1 Current Limitations

1. **UI Components**
   - Using raw Border instead of GlassCard
   - No GroupBox for section organization
   - IconPacks instead of Material.Icons
   - No CircleProgressBar with proper configuration
   - Missing Toast/Dialog infrastructure

2. **Architecture**
   - No base ViewModel class
   - No custom control base classes
   - No theme-aware custom styles
   - Limited MVVM pattern usage

3. **Missing Features**
   - No toast notifications
   - No dialog infrastructure
   - No clipboard helpers
   - No URI launching helpers

### 5.2 Refactoring Priorities

| Priority | Item | Effort | Files Affected |
|----------|------|--------|----------------|
| P0 | Migrate to Material.Icons | Low | All .axaml files |
| P0 | Add Toast/Dialog hosts | Medium | App.axaml, MainWindow.axaml |
| P1 | Create ViewModelBase | Medium | New file + ViewModels |
| P1 | Use GlassCard/GroupBox | Medium | Views |
| P2 | Custom AppStyles | Low | New AppStyles.axaml |
| P2 | Create UserControlBase | Low | New Controls/ |

---

## 6. Implementation Roadmap

### Phase 1: Quick Wins (Same Day)

1. ✅ Already done: Add Material.Icons.Avalonia package to csproj
2. ✅ Already done: Add MaterialIconsStyles to App.axaml
3. **Update all XAML files to use Material.Icons syntax** (IN PROGRESS)
4. Add ToastHost and DialogHost to MainWindow

### Phase 2: Foundation (1-2 Days)

5. Create `ViewModelBase.cs` with:
   - Toast/Dialog manager access
   - Lifecycle hooks
   - Clipboard helpers
   - URI launch helpers

6. Create `Controls/UserControlBase.cs`

### Phase 3: UI Polish (2-3 Days)

7. Replace Border containers with GlassCard
8. Add GroupBox for logical sections
9. Create `AppStyles.axaml` with theme-aware colors
10. Update MainWindow with proper structure

### Phase 4: Advanced (Optional)

11. Add SukiSideMenu navigation
12. Implement RadialGauge for metrics
13. Add ObservableCollections support

---

## 7. Detailed Refactoring Steps

### 7.1 Icon Migration (IconPacks → Material.Icons)

**Files to update:**
- `HeaderBarView.axaml`
- `SystemHealthPaneView.axaml`
- `ExecutionLogicPaneView.axaml`
- `TaskQueueView.axaml`
- `LiveConsoleView.axaml`
- `BottomActionBarView.axaml`

**Migration pattern:**

| IconPacks (OLD) | Material.Icons (NEW) |
|-----------------|---------------------|
| `iconPacks:PackIconCodicons Kind="Play"` | `icon:MaterialIconExt Kind=Play` |
| `iconPacks:PackIconCodicons Kind="Refresh"` | `icon:MaterialIconExt Kind=Refresh` |
| `iconPacks:PackIconCodicons Kind="File"` | `icon:MaterialIconExt Kind=FileOutline` |
| `iconPacks:PackIconCodicons Kind="Terminal"` | `icon:MaterialIconExt Kind=Console` |
| `iconPacks:PackIconCodicons Kind="Add"` | `icon:MaterialIconExt Kind=Plus` |
| `iconPacks:PackIconCodicons Kind="ListFlat"` | `icon:MaterialIconExt Kind=List` |
| `iconPacks:PackIconCodicons Kind="Gear"` | `icon:MaterialIconExt Kind=Cog` |
| `iconPacks:PackIconCodicons Kind="Error"` | `icon:MaterialIconExt Kind=AlertCircle` |
| `iconPacks:PackIconCodicons Kind="Warning"` | `icon:MaterialIconExt Kind=Alert` |
| `iconPacks:PackIconCodicons Kind="Check"` | `icon:MaterialIconExt Kind=Check` |

**XAML change example:**
```xml
<!-- OLD -->
<iconPacks:PackIconCodicons Kind="Play" Width="14" Height="14" />

<!-- NEW -->
<icon:MaterialIconExt Kind="Play" Width="14" Height="14" />
```

Also add namespace to each view:
```xml
xmlns:icon="clr-namespace:Material.Icons.Avalonia;assembly=Material.Icons.Avalonia"
```

### 7.2 Add Toast/Dialog Hosts to MainWindow

In `MainWindow.axaml`, add hosts inside SukiWindow:

```xml
<suki:SukiWindow ...>
    <suki:SukiWindow.Hosts>
        <suki:SukiToastHost Manager="{Binding ToastManager}" />
        <suki:SukiDialogHost Manager="{Binding DialogManager}" />
    </suki:SukiWindow.Hosts>
    <!-- rest of content -->
</suki:SukiWindow>
```

### 7.3 Create ViewModelBase

Create `ViewModels/ViewModelBase.cs`:

```csharp
using System;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace RevitDevTool.Processor.ViewModels;

public partial class ViewModelBase : ObservableObject
{
    public static SukiDialogManager DialogManager => App.DialogManager;
    public static SukiToastManager ToastManager => App.ToastManager;

    protected internal virtual void OnInitialized() { }
    protected internal virtual void OnLoaded() { }
    protected internal virtual void OnUnloaded() { }

    [RelayCommand]
    public static Task CopyToClipboardWithToast(object? text)
    {
        // Implementation
    }

    public static SukiDialogBuilder CreateMessageBox(
        NotificationType type, string? title = null, object? content = null)
    {
        var dialog = DialogManager.CreateDialog();
        if (title is not null) dialog.SetTitle(title);
        if (content is not null) dialog.SetContent(content);
        return dialog.OfType(type);
    }
}
```

### 7.4 Replace Border with GlassCard

Replace in views:
```xml
<!-- OLD -->
<Border BorderThickness="1" Padding="12">
    <!-- content -->
</Border>

<!-- NEW -->
<suki:GlassCard Padding="12" Margin="10">
    <!-- content -->
</suki:GlassCard>
```

### 7.5 Add GroupBox for Sections

```xml
<suki:GroupBox Margin="0,10">
    <suki:GroupBox.Header>
        <TextBlock Text="Section Title" FontWeight="SemiBold" />
    </suki:GroupBox.Header>
    <!-- content -->
</suki:GroupBox>
```

---

## 8. Design Document Analysis (code.html)

The `code.html` file contains a mature production UI design for the "RBP Next" application. This section analyzes the design patterns and maps them to SukiUI/Avalonia implementations.

### 8.1 Layout Structure

The design follows a classic dashboard layout:

```
┌─────────────────────────────────────────────────────────────────┐
│  HEADER: Logo + Title + Run Pre-Check + Load/Save Profile      │
├──────────┬──────────────────────────────────────────────────────┤
│          │  EXECUTION LOGIC (Left Panel)                       │
│  SYSTEM  │  - Target Version (dropdown)                         │
│  HEALTH  │  - Audit File (toggle)                               │
│  PANEL   │  - Headless Mode (toggle)                            │
│          │  - Dialog Suppression (toggle)                      │
│  - CPU   │  - Auto-Abort (toggle)                              │
│  - RAM   ├──────────────────────────────────────────────────────┤
│  - Limit │  TASK QUEUE TABLE                                    │
│          │  - Checkbox | Source | File | Size | Ver | Status   │
│  Orches- │  - Progress bars for active tasks                   │
│  trator  │  - Status pills (Completed, Processing, Error)     │
├──────────┴──────────────────────────────────────────────────────┤
│  LIVE CONSOLE OUTPUT                                           │
│  - Terminal-style colored output                              │
│  - Auto-scroll toggle | Wrap text toggle                       │
├─────────────────────────────────────────────────────────────────┤
│  FOOTER: Overall Progress | Dry Run | Start Processing         │
└─────────────────────────────────────────────────────────────────┘
```

### 8.2 UI Components Mapping

| Design Component | SukiUI/Avalonia Implementation |
|-----------------|-------------------------------|
| Sidebar (w-64) | `SukiSideMenu` with 250-270px width |
| Progress Circle | `suki:CircleProgressBar` or custom SVG |
| Toggle Switch | `ToggleSwitch` with custom styling |
| Status Pill | Custom Border with rounded corners + Background |
| Card Container | `suki:GlassCard` or `Border` with shadow |
| Header Bar | Custom `StackPanel` with buttons |
| Table | `DataGrid` with custom cell templates |
| Console | `TextBox` with colored `Runs` or custom control |

### 8.3 Color Scheme

```css
/* Light Theme */
primary: #0064C8 (Blue)
background: #F4F5F7
surface: #FFFFFF
text: #1C1F23
border: #E1E3E6

/* Dark Theme */
background: #16161A
surface: #232329
text: #F4F5F7
border: #3A3A42

/* Status Colors */
success: Green (#10B981)
warning: Amber (#F59E0B)
error: Red (#EF4444)
processing: Blue (#0064C8)
```

### 8.4 Key Design Features

1. **Status Pills**: Rounded badges with icons and colors
   ```xml
   <Border Background="#ECFDF5" CornerRadius="9999" Padding="8,2">
       <StackPanel Orientation="Horizontal" Spacing="4">
           <Icon Kind="CheckCircle" Foreground="#059669" />
           <TextBlock Text="Completed" Foreground="#059669" />
       </StackPanel>
   </Border>
   ```

2. **Circular Progress Gauges**: CSS conic-gradient for progress visualization
   ```xml
   <!-- SukiUI provides CircleProgressBar -->
   <suki:CircleProgressBar Value="45" Maximum="100" />
   ```

3. **Console Output**: Terminal-style with colored text per log level

---

## 9. Migration Plan for RevitDevTool.Processor

### Phase 1: Foundation (Completed ✅)

| Task | Status | Notes |
|------|--------|-------|
| Add Material.Icons.Avalonia package | ✅ DONE | In csproj |
| Add MaterialIconsStyles | ✅ DONE | In App.axaml |
| Migrate HeaderBarView.axaml | ✅ DONE | Using Material.Icons |
| Migrate SystemHealthPaneView.axaml | ✅ DONE | Using Material.Icons |
| Migrate ExecutionLogicPaneView.axaml | ✅ DONE | Using Material.Icons |
| Migrate TaskQueueView.axaml | ✅ DONE | Using Material.Icons |
| Migrate LiveConsoleView.axaml | ✅ DONE | Already had Material.Icons |
| Migrate BottomActionBarView.axaml | ✅ DONE | Using Material.Icons |
| Add Toast/Dialog hosts | ✅ DONE | In MainWindow.axaml |
| Create ViewModelBase | ✅ DONE | With toast/dialog helpers |

### Phase 2: Layout Redesign (Recommended)

Based on the design document, recommend these layout changes:

#### 2.1 MainWindow.axaml Structure
```xml
<suki:SukiWindow>
    <suki:SukiWindow.Hosts>
        <suki:SukiToastHost />
        <suki:SukiDialogHost />
    </suki:SukiWindow.Hosts>

    <Grid RowDefinitions="Auto,*">
        <!-- Header Bar -->
        <views:HeaderBarView />

        <!-- Main Content -->
        <Grid Grid.Row="1" ColumnDefinitions="Auto,*">
            <!-- Left Sidebar - System Health -->
            <suki:GlassCard Width="260" Margin="0,0,10,0">
                <views:SystemHealthPaneView />
            </suki:GlassCard>

            <!-- Right Content Area -->
            <Grid Grid.Column="1" RowDefinitions="Auto,*,Auto,Auto">
                <!-- Crash Risk Warning (conditional) -->
                <Border IsVisible="{Binding HasCrashRisk}" ... />

                <!-- Execution Logic + Task Queue -->
                <Grid Grid.Row="1" ColumnDefinitions="280,*">
                    <suki:GlassCard>
                        <views:ExecutionLogicPaneView />
                    </suki:GlassCard>
                    <suki:GlassCard Grid.Column="1">
                        <views:TaskQueueView />
                    </suki:GlassCard>
                </Grid>

                <!-- Live Console -->
                <suki:GlassCard Grid.Row="2" Height="200">
                    <views:LiveConsoleView />
                </suki:GlassCard>

                <!-- Bottom Action Bar -->
                <views:BottomActionBarView Grid.Row="3" />
            </Grid>
        </Grid>
    </Grid>
</suki:SukiWindow>
```

#### 2.2 System Health Panel Enhancement
Add circular progress gauges for CPU/RAM:
```xml
<suki:GlassCard>
    <StackPanel>
        <!-- CPU Gauge -->
        <suki:CircleProgressBar Value="{Binding CpuUsage}"
                                  Maximum="100"
                                  ShowText="True"
                                  Unit="%"/>

        <!-- RAM Gauge -->
        <suki:CircleProgressBar Value="{Binding RamUsage}"
                                  Maximum="100"
                                  ShowText="True"
                                  Unit="%"/>
    </StackPanel>
</suki:GlassCard>
```

#### 2.3 Task Queue Enhancement
Add status pills and source icons:
```xml
<DataGrid>
    <DataGrid.Columns>
        <DataGridTemplateColumn Header="Status">
            <DataGridTemplateColumn.CellTemplate>
                <DataTemplate>
                    <Border Classes="status-pill status-success">
                        <StackPanel Orientation="Horizontal">
                            <Icon Kind="CheckCircle" />
                            <TextBlock Text="Completed" />
                        </StackPanel>
                    </Border>
                </DataTemplate>
            </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>
    </DataGrid.Columns>
</DataGrid>
```

### Phase 3: Theme Support (Optional)

Add dynamic theme switching like NetSonar:

```csharp
// App.Theme.cs
public static void ChangeBaseTheme(ApplicationTheme theme)
{
    Theme.ChangeBaseTheme(theme switch {
        ApplicationTheme.Light => ThemeVariant.Light,
        ApplicationTheme.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default
    });
}
```

### Phase 4: Navigation (Future Enhancement)

If multiple pages are needed, add `SukiSideMenu`:

```xml
<suki:SukiSideMenu>
    <suki:SukiSideMenuItem Header="Orchestration"
                           Icon="{icon:MaterialIconExt Kind=Dashboard}"
                           IsSelected="True" />
    <suki:SukiSideMenuItem Header="Queue Manager"
                           Icon="{icon:MaterialIconExt Kind=QueuePlayNext}" />
    <suki:SukiSideMenuItem Header="Audit Logs"
                           Icon="{icon:MaterialIconExt Kind=History}" />
    <suki:SukiSideMenuItem Header="Settings"
                           Icon="{icon:MaterialIconExt Kind=Settings}" />
</suki:SukiSideMenu>
```

---

## 10. Best Practices Summary

### 10.1 From NetSonar

1. **MVVM Hierarchy**: Always create base classes (`ViewModelBase`, `PageViewModelBase`)
2. **Lifecycle Hooks**: Use `OnInitialized`, `OnLoaded`, `OnUnloaded` for setup/teardown
3. **Toast/Dialog Infrastructure**: Centralize user feedback through managers
4. **Theme-Aware Styling**: Use `ResourceDictionary.ThemeDictionaries` for light/dark
5. **DI Pattern**: Register services in `ServiceCollection` for testability

### 10.2 From Design Document

1. **Status Pills**: Use consistent colored badges for task states
2. **Progress Visualization**: Use `CircleProgressBar` for CPU/RAM metrics
3. **Console Styling**: Color-code log levels (INFO, WARN, ERROR)
4. **Layout Spacing**: Consistent margins (10-12px) and gaps
5. **Responsive Design**: Use Grid with flexible column/row definitions

### 10.3 Recommended File Structure

```
RevitDevTool.Processor/
├── Controls/
│   └── UserControlBase.cs       # Optional base with lifecycle
├── ViewModels/
│   ├── ViewModelBase.cs         # ✅ DONE - toast/dialog helpers
│   ├── MainWindowViewModel.cs   # Extend ViewModelBase
│   └── (future page VMs)
├── Views/
│   ├── MainWindow.axaml        # ✅ DONE - with hosts
│   ├── HeaderBarView.axaml     # ✅ DONE
│   ├── SystemHealthPaneView.axaml
│   ├── ExecutionLogicPaneView.axaml
│   ├── TaskQueueView.axaml
│   ├── LiveConsoleView.axaml
│   └── BottomActionBarView.axaml
├── Services/
│   └── (existing services)
├── Assets/
│   └── Styles/
│       └── AppStyles.axaml     # Optional theme colors
├── App.axaml                   # ✅ DONE - SukiTheme
└── App.axaml.cs                # Dialog/Toast managers
```

---

## 11. Conclusion

The RevitDevTool.Processor application has been successfully migrated to use:
- ✅ Material.Icons instead of IconPacks
- ✅ SukiUI Toast/Dialog infrastructure
- ✅ ViewModelBase with helper methods

The recommended next steps are:
1. **Optional**: Enhance layout with GlassCard containers
2. **Optional**: Add System Health circular gauges
3. **Optional**: Implement theme switching
4. **Future**: Consider SukiSideMenu if multi-page navigation is needed

The application now follows the same architectural patterns as NetSonar and is ready for future enhancements.

---

*Document generated based on NetSonar v6.0.4-nightly20260124 analysis*
*For RevitDevTool.Processor improvement planning*
*Design reference: code.html (RBP Next - Mature Production UI)*

