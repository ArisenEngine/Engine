using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ArisenEditor.Models;
using ArisenEngine;

namespace ArisenEditor.ViewModels;

interface IHierarchyViewModel<T> where T : TreeNodeBase
{
    public ObservableCollection<T> Source { get; }

    public T[] Roots { get; }
    
    public string Header { get; }

}
