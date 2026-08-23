using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Microsoft.EntityFrameworkCore;

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
    public ObservableCollection<CodingSahayi.Data.Project> ProjectsList { get; set; } = new();
    private readonly AgentContextManager _agentManager = new();
    public string AppVersion => $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
    public static Visibility ShowCopyButton(string role) => role == "Agent" ? Visibility.Visible : Visibility.Collapsed;

    private CancellationTokenSource? _cts;
    private System.Diagnostics.Stopwatch _elapsedStopwatch = new();
    private DispatcherQueueTimer? _elapsedTimer;
    
    public ObservableCollection<string> AttachedFiles { get; } = new();
    private bool _isMentioning = false;
    private int _mentionStartIndex = -1;
    private CancellationTokenSource? _searchCts;

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
        
        ModelSelector.ItemsSource = SettingsManager.AvailableModels;
        ModelSelector.SelectedItem = SettingsManager.ModelName;
        ModelSelector.SelectionChanged += ModelSelector_SelectionChanged;

        ContextChipsControl.ItemsSource = AttachedFiles;
        AttachedFiles.CollectionChanged += (s, e) => 
        {
            ContextChipsControl.Visibility = AttachedFiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        };

        using var db = new CodingSahayi.Data.AppDbContext();
        db.Database.EnsureCreated();
        
        LoadProjects();
    }

    private void LoadProjects()
    {
        using var db = new CodingSahayi.Data.AppDbContext();
        if (!db.Projects.Any())
        {
            db.Projects.Add(new CodingSahayi.Data.Project 
            { 
                Name = "Default Project", 
                WorkspacePath = _agentManager.WorkspaceDirectory ?? "", 
                CreatedAt = DateTime.UtcNow 
            });
            db.SaveChanges();
        }
        
        ProjectNav.MenuItems.Clear();
        var projects = db.Projects.Include(p => p.Conversations).ToList();
        
        foreach (var proj in projects)
        {
            var parentItem = new NavigationViewItem 
            { 
                Content = proj.Name, 
                Icon = new FontIcon { Glyph = "\uED43" },
                Tag = proj 
            };
            
            if (proj.Conversations != null)
            {
                foreach (var conv in proj.Conversations)
                {
                    var childItem = new NavigationViewItem
                    {
                        Content = conv.Title,
                        Icon = new SymbolIcon { Symbol = Symbol.Message },
                        Tag = conv.Id
                    };
                    parentItem.MenuItems.Add(childItem);
                }
            }
            ProjectNav.MenuItems.Add(parentItem);
        }
    }

    private void NewChat_Click(object sender, RoutedEventArgs e)
    {
        CodingSahayi.Data.Project? activeProject = null;
        
        if (ProjectNav.SelectedItem is NavigationViewItem navItem)
        {
            if (navItem.Tag is CodingSahayi.Data.Project p) 
                activeProject = p;
            else if (navItem.Tag is int convId)
            {
                // Find parent project if a conversation is selected
                using var db = new CodingSahayi.Data.AppDbContext();
                var conv = db.Conversations.FirstOrDefault(c => c.Id == convId);
                if (conv != null)
                {
                    activeProject = db.Projects.FirstOrDefault(p => p.Id == conv.ProjectId);
                }
            }
        }
        
        if (activeProject == null)
        {
            // Default to first project if nothing selected
            var firstItem = ProjectNav.MenuItems.FirstOrDefault() as NavigationViewItem;
            if (firstItem?.Tag is CodingSahayi.Data.Project p) activeProject = p;
        }

        if (activeProject != null)
        {
            using var db = new CodingSahayi.Data.AppDbContext();
            var newConv = new CodingSahayi.Data.Conversation
            {
                Title = "New Conversation",
                UpdatedAt = DateTime.Now,
                ProjectId = activeProject.Id
            };
            db.Conversations.Add(newConv);
            db.SaveChanges();
            
            LoadProjects();
            ClearChat_Click(this, new RoutedEventArgs());
        }
    }

    private void ModelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelSelector.SelectedItem is string selectedModel && !string.IsNullOrEmpty(selectedModel))
        {
            SettingsManager.ModelName = selectedModel;
            _agentManager.ReinitializeClient();
        }
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

    private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_cts != null) return;

        string text = InputTextBox.Text;
        int caret = InputTextBox.SelectionStart;

        // Mention Detection
        if (!_isMentioning)
        {
            if (caret > 0 && text.Length >= caret)
            {
                if (text[caret - 1] == '@' && (caret == 1 || char.IsWhiteSpace(text[caret - 2])))
                {
                    _isMentioning = true;
                    _mentionStartIndex = caret - 1;
                    UpdateMentionSearch("");
                }
            }
        }
        else
        {
            if (caret <= _mentionStartIndex || (caret > 0 && char.IsWhiteSpace(text[caret - 1])))
            {
                CancelMentionMode();
            }
            else
            {
                string query = text.Substring(_mentionStartIndex + 1, caret - (_mentionStartIndex + 1));
                UpdateMentionSearch(query);
            }
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            SendButton.Visibility = Visibility.Collapsed;
            MicButton.Visibility = Visibility.Visible;
        }
        else
        {
            SendButton.Visibility = Visibility.Visible;
            MicButton.Visibility = Visibility.Collapsed;
        }
    }

    private void CancelMentionMode()
    {
        _isMentioning = false;
        _mentionStartIndex = -1;
        MentionPopupBorder.Visibility = Visibility.Collapsed;
        _searchCts?.Cancel();
    }

    private async void UpdateMentionSearch(string query)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            await Task.Delay(150, token); // Debounce
            
            var files = await Task.Run(() => 
            {
                var dir = _agentManager.WorkspaceDirectory;
                if (string.IsNullOrEmpty(dir) || !System.IO.Directory.Exists(dir)) return new string[0];
                
                try {
                    return System.IO.Directory.EnumerateFiles(dir, $"*{query}*", System.IO.SearchOption.AllDirectories)
                        .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && !f.Contains("\\.git\\"))
                        .Take(20)
                        .Select(f => f.Substring(dir.Length).TrimStart('\\', '/'))
                        .ToArray();
                } catch { return new string[0]; }
            }, token);

            if (token.IsCancellationRequested) return;

            MentionListView.ItemsSource = files;
            MentionPopupBorder.Visibility = files.Any() ? Visibility.Visible : Visibility.Collapsed;
            if (files.Any()) MentionListView.SelectedIndex = 0;
        }
        catch (TaskCanceledException) { }
    }

    private void InputTextBox_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (_isMentioning && MentionPopupBorder.Visibility == Visibility.Visible)
        {
            if (e.Key == Windows.System.VirtualKey.Down)
            {
                if (MentionListView.SelectedIndex < MentionListView.Items.Count - 1) MentionListView.SelectedIndex++;
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Up)
            {
                if (MentionListView.SelectedIndex > 0) MentionListView.SelectedIndex--;
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Enter || e.Key == Windows.System.VirtualKey.Tab)
            {
                if (MentionListView.SelectedItem is string selected) ConfirmMention(selected);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                CancelMentionMode();
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Enter && !Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            SendButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void MentionListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is string selected) ConfirmMention(selected);
    }

    private void ConfirmMention(string filePath)
    {
        if (!_isMentioning) return;
        
        string text = InputTextBox.Text;
        int caret = InputTextBox.SelectionStart;
        
        string newText = text.Remove(_mentionStartIndex, caret - _mentionStartIndex);
        InputTextBox.Text = newText;
        InputTextBox.SelectionStart = _mentionStartIndex;
        
        if (!AttachedFiles.Contains(filePath)) AttachedFiles.Add(filePath);
        
        CancelMentionMode();
    }

    private void RemoveContextChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string file)
        {
            AttachedFiles.Remove(file);
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

        if (string.IsNullOrWhiteSpace(InputTextBox.Text) && AttachedFiles.Count == 0) return;

        string userText = InputTextBox.Text;
        
        _cts = new CancellationTokenSource();
        
        InputTextBox.Text = string.Empty;
        InputTextBox.IsEnabled = false;

        SendButton.Visibility = Visibility.Visible;
        MicButton.Visibility = Visibility.Collapsed;

        string fullMessageToAI = userText;
        if (AttachedFiles.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var file in AttachedFiles)
            {
                string path = System.IO.Path.Combine(_agentManager.WorkspaceDirectory, file);
                string content = NativeTools.ReadFile(path);
                sb.AppendLine($"<file path=\"{file}\">\n{content}\n</file>");
            }
            fullMessageToAI = $"{sb}\nUser Message:\n{userText}";
        }

        ChatHistory.Add(new TextMessageModel { Role = "User", Content = userText, Alignment = HorizontalAlignment.Right, BackgroundBrush = UserBrush });
        
        AttachedFiles.Clear();

        // Start the elapsed-time timer that renders "Elapsed: mm:ss".
        _elapsedStopwatch.Restart();
        _elapsedTimer = DispatcherQueue.CreateTimer();
        _elapsedTimer.Interval = TimeSpan.FromSeconds(1);
        _elapsedTimer.Tick += (s, ev) =>
        {
            TimeSpan ts = _elapsedStopwatch.Elapsed;
            ExecutionTimerText.Text = $"Time: {ts.Minutes:D2}:{ts.Seconds:D2}";
        };
        _elapsedTimer.Start();

        // Change the button to act as a Stop control.
        SendIcon.Symbol = Symbol.Stop;
        ExecutionTimerText.Text = "Time: 00:00";

        StatusText.Text = "Thinking...";

        var token = _cts.Token;
        string finalResponse = await _agentManager.ProcessMessageAsync(
            fullMessageToAI, 
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
                ExecutionTimerText.Text = ""; StatusText.Text = "Idle";
            }
            ScrollToBottom();

            InputTextBox.IsEnabled = true;
            SendIcon.Symbol = Symbol.Send;
            
            if (string.IsNullOrWhiteSpace(InputTextBox.Text))
            {
                SendButton.Visibility = Visibility.Collapsed;
                MicButton.Visibility = Visibility.Visible;
            }
            
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

    private async void NewProject_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        var nameTextBox = new TextBox { Header = "Project Name", PlaceholderText = "e.g. My Awesome App" };
        var folderTextBlock = new TextBlock { Text = "No folder selected", Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray), TextWrapping = TextWrapping.Wrap };
        var selectFolderBtn = new Button { Content = "Select Workspace Folder", Margin = new Thickness(0, 10, 0, 10) };
        string selectedPath = "";

        selectFolderBtn.Click += async (s, ev) =>
        {
            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                selectedPath = folder.Path;
                folderTextBlock.Text = folder.Path;
                folderTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
            }
        };

        var stackPanel = new StackPanel { Spacing = 10 };
        stackPanel.Children.Add(nameTextBox);
        stackPanel.Children.Add(selectFolderBtn);
        stackPanel.Children.Add(folderTextBlock);

        var dialog = new ContentDialog
        {
            Title = "Create New Project",
            Content = stackPanel,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            XamlRoot = this.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(nameTextBox.Text) || string.IsNullOrEmpty(selectedPath))
            {
                StatusText.Text = "Project creation cancelled: missing name or folder.";
                return;
            }

            var newProject = new CodingSahayi.Data.Project
            {
                Name = nameTextBox.Text.Trim(),
                WorkspacePath = selectedPath,
                CreatedAt = DateTime.UtcNow
            };

            using var db = new CodingSahayi.Data.AppDbContext();
            db.Projects.Add(newProject);
            await db.SaveChangesAsync();

            LoadProjects();
            _agentManager.WorkspaceDirectory = selectedPath;
            WorkspacePathText.Text = selectedPath;
            StatusText.Text = $"Project '{newProject.Name}' created.";
        }
    }
}
