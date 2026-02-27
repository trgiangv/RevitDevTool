using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace RevitDevTool.Desktop.ViewModels;

/// <summary>
/// Base class for all ViewModels providing common functionality like dialog and toast management.
/// </summary>
public partial class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Gets the main window's TopLevel for navigation and dialog operations.
    /// </summary>
    protected static TopLevel? TopLevel => 
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
            ? desktop.MainWindow 
            : null;

    /// <summary>
    /// Shows a toast notification with the given title and message.
    /// </summary>
    public static void ShowToast(string title, string message, NotificationType type = NotificationType.Information)
    {
        App.ToastManager.CreateToast()
            .WithTitle(title)
            .WithContent(message)
            .OfType(type)
            .Queue();
    }

    /// <summary>
    /// Shows a success toast notification.
    /// </summary>
    public static void ShowSuccess(string title, string message) => 
        ShowToast(title, message, NotificationType.Success);

    /// <summary>
    /// Shows an error toast notification.
    /// </summary>
    public static void ShowError(string title, string message) => 
        ShowToast(title, message, NotificationType.Error);

    /// <summary>
    /// Shows a warning toast notification.
    /// </summary>
    public static void ShowWarning(string title, string message) => 
        ShowToast(title, message, NotificationType.Warning);

    /// <summary>
    /// Creates a simple message dialog.
    /// </summary>
    public static SukiDialogBuilder CreateMessageBox(string? title = null, object? content = null)
    {
        var dialog = App.DialogManager.CreateDialog();
        if (title is not null)
            dialog.SetTitle(title);
        if (content is not null)
            dialog.SetContent(content);
        return dialog;
    }

    /// <summary>
    /// Creates a message dialog with the specified notification type styling.
    /// </summary>
    public static SukiDialogBuilder CreateMessageBox(NotificationType type, string? title = null, object? content = null)
    {
        return CreateMessageBox(title, content).OfType(type);
    }

    /// <summary>
    /// Shows a simple message dialog and returns immediately.
    /// </summary>
    public static void ShowMessage(string title, string message, NotificationType type = NotificationType.Information)
    {
        CreateMessageBox(type, title, message).TryShow();
    }

    /// <summary>
    /// Copies text to clipboard and shows a toast confirmation.
    /// </summary>
    public static async Task CopyToClipboardAsync(string? text, bool showToast = true)
    {
        if (string.IsNullOrEmpty(text)) return;

        var topLevel = TopLevel;
        if (topLevel?.Clipboard is null) return;

        await topLevel.Clipboard.SetTextAsync(text);

        if (showToast)
            ShowSuccess("Copied", "Text copied to clipboard");
    }

    /// <summary>
    /// Called when the view finishes initialization.
    /// </summary>
    protected internal virtual void OnInitialized() { }

    /// <summary>
    /// Called when the view finishes loading.
    /// </summary>
    protected internal virtual void OnLoaded() { }

    /// <summary>
    /// Called when the view finishes unloading.
    /// </summary>
    protected internal virtual void OnUnloaded() { }
}

