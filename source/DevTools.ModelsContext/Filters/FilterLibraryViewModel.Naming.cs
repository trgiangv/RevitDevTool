using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace DevTools.ModelsContext.Filters;

public sealed partial class FilterLibraryViewModel
{
    [CustomValidation(typeof(FilterLibraryViewModel), nameof(CheckName))]
    public string EditName
    {
        get;
        set
        {
            if (SetProperty(ref field, value ?? string.Empty))
            {
                ValidateName();
            }
        }
    } = string.Empty;

    public string? NameError => GetErrors(nameof(EditName)).FirstOrDefault()?.ErrorMessage;

    [ObservableProperty]
    public partial bool IsEditingName { get; private set; }

    public static ValidationResult? CheckName(string name, ValidationContext context)
    {
        var error = FilterNames.Validate(name, ((FilterLibraryViewModel)context.ObjectInstance).OtherNames());
        return error is null ? ValidationResult.Success : new ValidationResult(error);
    }

    public void BeginRename()
    {
        if (Current is not { IsCustom: true })
        {
            return;
        }

        IsEditingName = true;
        EditName = Name;
        ValidateName();
    }

    public bool CommitRename()
    {
        if (!IsEditingName)
        {
            return true;
        }

        ValidateName();
        if (NameError is not null)
        {
            return false;
        }

        var trimmed = EditName.Trim();
        IsEditingName = false;
        EditName = trimmed;
        if (!string.Equals(trimmed, Name, StringComparison.Ordinal))
        {
            Name = trimmed;
            if (Current is { IsCustom: true } item)
            {
                item.Name = trimmed;
            }

            IsDirty = true;
        }

        return true;
    }

    public void CancelRename()
    {
        IsEditingName = false;
        EditName = Name;
        ClearErrors(nameof(EditName));
    }

    public void LeaveRename()
    {
        if (!CommitRename())
        {
            CancelRename();
        }
    }

    private void ValidateName()
    {
        if (IsEditingName)
        {
            ValidateProperty(EditName, nameof(EditName));
        }
        else
        {
            ClearErrors(nameof(EditName));
        }
    }

    private IEnumerable<string> OtherNames()
    {
        return Sources.Where(item => item.IsCustom && !ReferenceEquals(item, Current)).Select(item => item.Name);
    }

    private IEnumerable<string> CustomNames()
    {
        return Sources.Where(source => source.IsCustom).Select(source => source.Name);
    }
}
