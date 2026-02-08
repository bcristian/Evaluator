using System.ComponentModel;

namespace Evaluator.ViewModels;

public class AnswerOptionViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public string Text { get; set; } = string.Empty;
    public int Index { get; set; }
    public bool IsCorrect { get; set; }
    /// <summary>When true (review mode), checkbox is read-only.</summary>
    public bool IsReadOnly { get; set; }
    public bool IsEditing => !IsReadOnly;

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
