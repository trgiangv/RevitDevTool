using DevTools.MetroFork.Tests.Support;

namespace DevTools.MetroFork.Tests;

[DoNotParallelize]
[TestClass]
public sealed class MultiSelectionComboBoxBindingTests
{
    private static readonly string[] MonitorAndFile = ["Monitor", "File"];
    private static readonly string[] MonitorOnly = ["Monitor"];
    private static readonly string[] FileOnly = ["File"];
    private static readonly string[] AllThree = ["Monitor", "File", "Http"];

    private static WpfStaSession Sta => WpfStaSessionHolder.Instance;

    [TestMethod]
    public void L1_Preloaded_Show_Pump_TwoUnique()
    {
        Sta.Invoke(() =>
        {
            using var fixture = MultiSelectionComboBoxFixture.CreatePreloaded();
            SelectionSurfaceAssertions.AssertAll(fixture, MonitorAndFile, "L1");
        });
    }

    [TestMethod]
    public void L2_DelayedLoad_TwoUnique_NoDuplicateChips()
    {
        Sta.Invoke(() =>
        {
            using var fixture = MultiSelectionComboBoxFixture.CreateShownEmpty();
            fixture.LoadFromSettings(fileEnabled: true, httpEnabled: false);
            SelectionSurfaceAssertions.AssertAll(fixture, MonitorAndFile, "L2");
        });
    }

    [TestMethod]
    public void L3_DelayedLoad_ThenReload_StillTwoUnique()
    {
        Sta.Invoke(() =>
        {
            using var fixture = MultiSelectionComboBoxFixture.CreateShownEmpty();
            fixture.LoadFromSettings(fileEnabled: true, httpEnabled: false);
            SelectionSurfaceAssertions.AssertAll(fixture, MonitorAndFile, "L3 after first load");

            fixture.LoadFromSettings(fileEnabled: true, httpEnabled: false);
            SelectionSurfaceAssertions.AssertAll(fixture, MonitorAndFile, "L3 after reload");
        });
    }

    [TestMethod]
    public void D1_Preloaded_OpenDropdown_MonitorAndFileChecked()
    {
        Sta.Invoke(() =>
        {
            using var fixture = MultiSelectionComboBoxFixture.CreatePreloaded();
            SelectionSurfaceAssertions.OpenDropdown(fixture);
            SelectionSurfaceAssertions.AssertAll(fixture, MonitorAndFile, "D1", dropdownOpen: true);
        });
    }

    [TestMethod]
    public void D2_DelayedLoad_OpenDropdown_MonitorAndFileChecked()
    {
        Sta.Invoke(() =>
        {
            using var fixture = MultiSelectionComboBoxFixture.CreateShownEmpty();
            fixture.LoadFromSettings(fileEnabled: true, httpEnabled: false);
            SelectionSurfaceAssertions.OpenDropdown(fixture);
            SelectionSurfaceAssertions.AssertAll(fixture, MonitorAndFile, "D2", dropdownOpen: true);
        });
    }

    [TestMethod]
    public void D3_OpenDropdown_ThenReload_NoDuplicateChecksOrChips()
    {
        Sta.Invoke(() =>
        {
            using var fixture = MultiSelectionComboBoxFixture.CreateShownEmpty();
            fixture.LoadFromSettings(fileEnabled: true, httpEnabled: false);
            SelectionSurfaceAssertions.OpenDropdown(fixture);
            fixture.LoadFromSettings(fileEnabled: true, httpEnabled: false);
            SelectionSurfaceAssertions.AssertAll(fixture, MonitorAndFile, "D3", dropdownOpen: true);
        });
    }

    [TestMethod]
    public void U1_OpenDropdown_UncheckFile_MonitorOnly()
    {
        Sta.Invoke(() =>
        {
            using var fixture = MultiSelectionComboBoxFixture.CreatePreloaded();
            SelectionSurfaceAssertions.OpenDropdown(fixture);
            SelectionSurfaceAssertions.SetPopupItemSelected(fixture, "File", selected: false);
            SelectionSurfaceAssertions.AssertAll(fixture, MonitorOnly, "U1", dropdownOpen: true);
        });
    }

    [TestMethod]
    public void U2_UncheckMonitor_KeepFile_FileOnly()
    {
        Sta.Invoke(() =>
        {
            using var fixture = MultiSelectionComboBoxFixture.CreatePreloaded();
            SelectionSurfaceAssertions.OpenDropdown(fixture);
            SelectionSurfaceAssertions.SetPopupItemSelected(fixture, "Monitor", selected: false);
            SelectionSurfaceAssertions.AssertAll(fixture, FileOnly, "U2", dropdownOpen: true);
        });
    }

    [TestMethod]
    public void C1_AfterU1_CheckFileAgain_MonitorAndFile()
    {
        Sta.Invoke(() =>
        {
            using var fixture = MultiSelectionComboBoxFixture.CreatePreloaded();
            SelectionSurfaceAssertions.OpenDropdown(fixture);
            SelectionSurfaceAssertions.SetPopupItemSelected(fixture, "File", selected: false);
            SelectionSurfaceAssertions.AssertAll(fixture, MonitorOnly, "C1 after uncheck File");

            SelectionSurfaceAssertions.SetPopupItemSelected(fixture, "File", selected: true);
            SelectionSurfaceAssertions.AssertAll(fixture, MonitorAndFile, "C1 after re-check File", dropdownOpen: true);
        });
    }

    [TestMethod]
    public void C2_CheckHttp_ThreeUnique()
    {
        Sta.Invoke(() =>
        {
            using var fixture = MultiSelectionComboBoxFixture.CreatePreloaded();
            SelectionSurfaceAssertions.OpenDropdown(fixture);
            SelectionSurfaceAssertions.SetPopupItemSelected(fixture, "Http", selected: true);
            SelectionSurfaceAssertions.AssertAll(fixture, AllThree, "C2", dropdownOpen: true);
        });
    }

    [TestMethod]
    public void C3_UncheckAll_ThenCheckMonitorAndFile_TwoUnique()
    {
        Sta.Invoke(() =>
        {
            using var fixture = MultiSelectionComboBoxFixture.CreatePreloaded();
            SelectionSurfaceAssertions.OpenDropdown(fixture);
            SelectionSurfaceAssertions.SetPopupItemSelected(fixture, "Monitor", selected: false);
            SelectionSurfaceAssertions.SetPopupItemSelected(fixture, "File", selected: false);
            SelectionSurfaceAssertions.SetPopupItemSelected(fixture, "Http", selected: false);
            SelectionSurfaceAssertions.AssertAll(fixture, [], "C3 after uncheck all", dropdownOpen: true);

            SelectionSurfaceAssertions.SetPopupItemSelected(fixture, "Monitor", selected: true);
            SelectionSurfaceAssertions.SetPopupItemSelected(fixture, "File", selected: true);
            SelectionSurfaceAssertions.AssertAll(fixture, MonitorAndFile, "C3 after re-check Monitor+File", dropdownOpen: true);
        });
    }

    [TestMethod]
    public void R1_AfterU1_ReloadSettings_BackToMonitorAndFile()
    {
        Sta.Invoke(() =>
        {
            using var fixture = MultiSelectionComboBoxFixture.CreatePreloaded();
            SelectionSurfaceAssertions.OpenDropdown(fixture);
            SelectionSurfaceAssertions.SetPopupItemSelected(fixture, "File", selected: false);
            SelectionSurfaceAssertions.AssertAll(fixture, MonitorOnly, "R1 after uncheck File", dropdownOpen: true);

            fixture.ComboBox.IsDropDownOpen = false;
            MultiSelectionComboBoxFixture.Pump(fixture.Window.Dispatcher);
            fixture.LoadFromSettings(fileEnabled: true, httpEnabled: false);
            SelectionSurfaceAssertions.AssertAll(fixture, MonitorAndFile, "R1 after reload");
        });
    }

    [TestMethod]
    public void B1_UserAddsDuplicate_BagIsMirroredOnChips()
    {
        Sta.Invoke(() =>
        {
            using var fixture = MultiSelectionComboBoxFixture.CreateShownEmpty();
            fixture.LoadFromSettings(fileEnabled: true, httpEnabled: false);
            SelectionSurfaceAssertions.AssertAll(fixture, MonitorAndFile, "B1 after load");

            fixture.ViewModel.SelectedLogTargets.Add("Monitor");
            MultiSelectionComboBoxFixture.Pump(fixture.Window.Dispatcher);

            SelectionSurfaceAssertions.AssertUserBagMirrored(
                fixture,
                ["Monitor", "File", "Monitor"],
                "B1 after user Add(Monitor)");
        });
    }

    [TestMethod]
    public void B2_UserBag_OpenDropdown_DoesNotInventFourthChip()
    {
        Sta.Invoke(() =>
        {
            using var fixture = MultiSelectionComboBoxFixture.CreateShownEmpty();
            fixture.LoadFromSettings(fileEnabled: true, httpEnabled: false);
            fixture.ViewModel.SelectedLogTargets.Add("Monitor");
            MultiSelectionComboBoxFixture.Pump(fixture.Window.Dispatcher);

            SelectionSurfaceAssertions.OpenDropdown(fixture);
            SelectionSurfaceAssertions.AssertUserBagMirrored(
                fixture,
                ["Monitor", "File", "Monitor"],
                "B2 after dropdown open",
                dropdownOpen: true);
        });
    }
}
