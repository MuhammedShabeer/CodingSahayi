using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;
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
        if (string.IsNullOrWhiteSpace(InputTextBox.Text)) return;

        string userText = InputTextBox.Text;
        InputTextBox.Text = string.Empty;
        InputTextBox.IsEnabled = false;
        SendButton.IsEnabled = false;

        ChatHistory.Add(new TextMessageModel { Role = "User", Content = userText, Alignment = HorizontalAlignment.Right, BackgroundBrush = UserBrush });
        
        StatusText.Text = "Thinking...";

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
            }
        );

        DispatcherQueue.TryEnqueue(() =>
        {
            if (!string.IsNullOrWhiteSpace(finalResponse))
            {
                ChatHistory.Add(new TextMessageModel { Role = "Agent", Content = finalResponse, Alignment = HorizontalAlignment.Left, BackgroundBrush = AgentBrush });
            }
            StatusText.Text = "Idle";
            ScrollToBottom();

            InputTextBox.IsEnabled = true;
            SendButton.IsEnabled = true;
        });
    }

    private void ClearChat_Click(object sender, RoutedEventArgs e) 
    { 
        ChatHistory.Clear(); 
        ChatHistory.Add(new TextMessageModel { Role = "Agent", Content = "Hello! I am an AI coding assistant. Type a message to ask me a question or request a coding action. Use the \"Select Workspace\" button above to choose a folder containing your code project.\r\n\r\nPlease note that this application can only interact with files in the selected workspace. If you need to access files outside of this workspace, you will need to create a new project or modify an existing one within the workspace.\r\n\r\nWhat can I help you with today?", Alignment = HorizontalAlignment.Left, BackgroundBrush = AgentBrush }); 
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
