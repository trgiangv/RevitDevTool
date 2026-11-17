using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace DevTools.UI.Behaviors;

public static class CheckedItems
{
    public static readonly DependencyProperty PathProperty = DependencyProperty.RegisterAttached(
        "Path",
        typeof(string),
        typeof(CheckedItems),
        new PropertyMetadata(null, OnPathChanged));

    private static readonly DependencyProperty SyncProperty = DependencyProperty.RegisterAttached(
        "Sync",
        typeof(Sync),
        typeof(CheckedItems));

    public static string? GetPath(DependencyObject element)
    {
        return (string?)element.GetValue(PathProperty);
    }

    public static void SetPath(DependencyObject element, string? value)
    {
        element.SetValue(PathProperty, value);
    }

    private static void OnPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox box)
        {
            return;
        }

        (box.GetValue(SyncProperty) as Sync)?.Detach();
        box.ClearValue(SyncProperty);
        if (e.NewValue is string { Length: > 0 } path)
        {
            box.SetValue(SyncProperty, new Sync(box, path));
        }
    }

    private sealed class Sync
    {
        private readonly ListBox _box;
        private readonly string _path;
        private readonly List<INotifyPropertyChanged> _watched = [];
        private bool _isPulling;
        private bool _isPushing;

        public Sync(ListBox box, string path)
        {
            _box = box;
            _path = path;
            box.Loaded += OnLoaded;
            box.Unloaded += OnUnloaded;
            Attach();
        }

        public void Detach()
        {
            _box.Loaded -= OnLoaded;
            _box.Unloaded -= OnUnloaded;
            Release();
        }

        private void Attach()
        {
            Release();
            ((INotifyCollectionChanged)_box.Items).CollectionChanged += OnItemsChanged;
            _box.SelectionChanged += OnSelectionChanged;
            Watch();
            Pull();
        }

        private void Release()
        {
            ((INotifyCollectionChanged)_box.Items).CollectionChanged -= OnItemsChanged;
            _box.SelectionChanged -= OnSelectionChanged;
            Unwatch();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Attach();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Release();
        }

        private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Watch();
            Pull();
        }

        private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == _path || string.IsNullOrEmpty(e.PropertyName))
            {
                Pull();
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isPulling || !ReferenceEquals(e.OriginalSource, _box))
            {
                return;
            }

            _isPushing = true;
            try
            {
                foreach (var item in e.AddedItems)
                {
                    Write(item, true);
                }

                foreach (var item in e.RemovedItems)
                {
                    Write(item, false);
                }
            }
            finally
            {
                _isPushing = false;
            }

            Pull();
        }

        private void Watch()
        {
            Unwatch();
            foreach (var item in _box.Items)
            {
                if (item is INotifyPropertyChanged notify)
                {
                    notify.PropertyChanged += OnItemChanged;
                    _watched.Add(notify);
                }
            }
        }

        private void Unwatch()
        {
            foreach (var notify in _watched)
            {
                notify.PropertyChanged -= OnItemChanged;
            }

            _watched.Clear();
        }

        private void Pull()
        {
            if (_isPushing || _isPulling)
            {
                return;
            }

            var wanted = _box.Items.Cast<object>().Where(Read).ToList();
            var current = _box.SelectedItems.Cast<object>().ToList();
            if (wanted.Count == current.Count && wanted.All(current.Contains))
            {
                return;
            }

            _isPulling = true;
            try
            {
                _box.SelectedItems.Clear();
                foreach (var item in wanted)
                {
                    _box.SelectedItems.Add(item);
                }
            }
            finally
            {
                _isPulling = false;
            }
        }

        private bool Read(object item)
        {
            return TypeDescriptor.GetProperties(item)[_path]?.GetValue(item) is true;
        }

        private void Write(object item, bool value)
        {
            if (_box.Items.Contains(item) && TypeDescriptor.GetProperties(item)[_path] is { IsReadOnly: false } property
                                           && property.GetValue(item) is bool current && current != value)
            {
                property.SetValue(item, value);
            }
        }
    }
}
