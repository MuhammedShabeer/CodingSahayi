using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Windows.Storage.Pickers;

namespace CodingSahayi;

public class ChatMessageTemplateSelector : DataTemplateSelector
{
    public DataTemplate TextMessageTemplate { get; set; } = null!;
    public DataTemplate ToolMessageTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item)
    {
        if (item is ToolMessageModel) return ToolMessageTemplate;
        return TextMessageTemplate;
    }
}

public sealed partial class MainWindow : Window
{
    public ObservableCollection<MessageModelBase> ChatHistory { get; } = new();
    private readonly AgentContextManager _agentManager = new();
    public string AppVersion => $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
    public static Visibility ShowCopyButton(string role) => role == "Agent" ? Visibility.Visible : Visibility.Collapsed;

    private CancellationTokenSource? _cts;
    private System.Diagnostics.Stopwatch _elapsedStopwatch = new();
    private DispatcherQueueTimer? _elapsedTimer;

    private async void CopyMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is TextMessageModel message)
        {
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(message.Content);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            StatusText.Text = "Copied to clipboard.";
        }
    }

    private SolidColorBrush UserBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 215));
    private SolidColorBrush AgentBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 60, 60, 60));
    private SolidColorBrush ToolBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 40, 40, 40));

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        
        WorkspacePathText.Text = _agentManager.WorkspaceDirectory;
    }

    private async void SelectWorkspaceButton_Click(object sender, RoutedEventArgs e)
    {
        var folderPicker = new FolderPicker();
        folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
        folderPicker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            _agentManager.WorkspaceDirectory = folder.Path;
            WorkspacePathText.Text = folder.Path;
        }
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog();
        dialog.XamlRoot = this.Content.XamlRoot;
        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
        {
            _agentManager.ReinitializeClient();
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        // If a generation is in progress, Stop it instead of sending new text.
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(InputTextBox.Text)) return;

        string userText = InputTextBox.Text;
        InputTextBox.Text = string.Empty;
        InputTextBox.IsEnabled = false;

        ChatHistory.Add(new TextMessageModel { Role = "User", Content = userText, Alignment = HorizontalAlignment.Right, BackgroundBrush = UserBrush });
        
        _cts = new CancellationTokenSource();

        // Start the elapsed-time timer that renders "Elapsed: mm:ss".
        _elapsedStopwatch.Restart();
        _elapsedTimer = DispatcherQueue.CreateTimer();
        _elapsedTimer.Interval = TimeSpan.FromSeconds(1);
        _elapsedTimer.Tick += (s, ev) =>
        {
            TimeSpan ts = _elapsedStopwatch.Elapsed;
            SendButton.Content = $"Stop ({ts.Minutes:D2}:{ts.Seconds:D2})";
        };
        _elapsedTimer.Start();

        // Change the button to act as a Stop control.
        SendButton.Content = "Stop (00:00)";

        StatusText.Text = "Thinking...";

        var token = _cts.Token;
        string finalResponse = await _agentManager.ProcessMessageAsync(
            userText, 
            (status) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    StatusText.Text = status;
                });
            },
            (toolCallId, toolName, args) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    var toolModel = new ToolMessageModel 
                    { 
                        ToolCallId = toolCallId,
                        Role = "Tool",
                        ToolName = toolName, 
                        Arguments = args, 
                        Status = "Running...",
                        Alignment = HorizontalAlignment.Left,
                        BackgroundBrush = ToolBrush 
                    };
                    ChatHistory.Add(toolModel);
                    ScrollToBottom();
                });
            },
            (toolCallId, output, success) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    var toolModel = ChatHistory.OfType<ToolMessageModel>().LastOrDefault(m => m.ToolCallId == toolCallId);
                    if (toolModel != null)
                    {
                        toolModel.Output = output;
                        toolModel.Status = success ? "Success" : "Failed";
                    }
                });
            },
            cancellationToken: token
        );

        DispatcherQueue.TryEnqueue(() =>
        {
            _elapsedTimer?.Stop();
            _elapsedStopwatch.Stop();

            if (!string.IsNullOrWhiteSpace(finalResponse))
            {
                ChatHistory.Add(new TextMessageModel { Role = "Agent", Content = finalResponse, Alignment = HorizontalAlignment.Left, BackgroundBrush = AgentBrush });
            }

            if (token.IsCancellationRequested)
            {
                StatusText.Text = "Interrupted.";
            }
            else
            {
                StatusText.Text = "Idle";
            }
            ScrollToBottom();

            InputTextBox.IsEnabled = true;
            SendButton.Content = "Send";
            _cts?.Dispose();
            _cts = null;
        });
    }

    private void ClearChat_Click(object sender, RoutedEventArgs e) 
    { 
        ChatHistory.Clear(); 
        ChatHistory.Add(new TextMessageModel { Role = "Agent", Content = "Hello! I am an AI coding assistant. Type a message to ask me a question or request a coding action. Use the \"Select Workspace\" button above to choose a folder containing your code project.\r\n\r\nWhat can I help you with today?", Alignment = HorizontalAlignment.Left, BackgroundBrush = AgentBrush }); 
    }

    private void ScrollToBottom()
    {
        ChatListView.UpdateLayout();
        if (ChatHistory.Count > 0)
        {
            ChatListView.ScrollIntoView(ChatHistory.Last());
        }
    }
}
