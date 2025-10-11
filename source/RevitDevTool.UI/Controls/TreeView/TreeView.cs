// Copyright 2003-2024 by Lookup Foundation and Contributors
// 
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
// 
// THIS PROGRAM IS PROVIDED "AS IS" AND WITH ALL FAULTS.
// NO IMPLIED WARRANTY OF MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE IS PROVIDED.
// THERE IS NO GUARANTEE THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

public class TreeView : System.Windows.Controls.TreeView
{
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
    {
        base.OnItemsSourceChanged(oldValue, newValue);
        ItemsSourceChanged?.Invoke(this, newValue);
    }

    public event EventHandler<IEnumerable>? ItemsSourceChanged;
}