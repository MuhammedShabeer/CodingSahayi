using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CodingSahayi;

public abstract class MessageModelBase : INotifyPropertyChanged
{
    private string _role = string.Empty;
    public string Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    public HorizontalAlignment Alignment { get; set; }
    public SolidColorBrush BackgroundBrush { get; set; } = null!;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null!)
    {
        if (Equals(storage, value)) return false;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public class TextMessageModel : MessageModelBase
{
    private string _content = string.Empty;
    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }
}

public class ToolMessageModel : MessageModelBase
{
    public string ToolCallId { get; set; } = string.Empty;

    private string _toolName = string.Empty;
    public string ToolName
    {
        get => _toolName;
        set => SetProperty(ref _toolName, value);
    }

    private string _arguments = string.Empty;
    public string Arguments
    {
        get => _arguments;
        set => SetProperty(ref _arguments, value);
    }

    private string _output = string.Empty;
    public string Output
    {
        get => _output;
        set => SetProperty(ref _output, value);
    }

    private string _status = "Running";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }
}
