using System;

namespace ArisenEditor.Core.Services;

public interface ISelectionService
{
    event Action<object?> SelectionChanged;
    object? CurrentSelection { get; set; }
}

public class SelectionService : ISelectionService
{
    private object? _currentSelection;
    public event Action<object?>? SelectionChanged;

    public object? CurrentSelection
    {
        get => _currentSelection;
        set
        {
            if (_currentSelection == value) return;
            _currentSelection = value;
            SelectionChanged?.Invoke(_currentSelection);
        }
    }
}
