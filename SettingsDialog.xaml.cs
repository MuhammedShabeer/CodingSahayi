using Microsoft.UI.Xaml.Controls;

namespace CodingSahayi;

public sealed partial class SettingsDialog : ContentDialog
{
    public SettingsDialog()
    {
        this.InitializeComponent();
        
        ApiKeyBox.Password = SettingsManager.SecureApiKey;
        EndpointBox.Text = SettingsManager.ApiEndpoint;
        
        // Populate model ComboBox with saved models, select the active one
        ModelNameBox.ItemsSource = SettingsManager.AvailableModels;
        ModelNameBox.SelectedItem = SettingsManager.ModelName;
        
        SystemPromptBox.Text = SettingsManager.SystemPrompt;
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        SettingsManager.SecureApiKey = ApiKeyBox.Password;
        SettingsManager.ApiEndpoint = EndpointBox.Text;
        
        string selectedModel = ModelNameBox.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(selectedModel))
        {
            SettingsManager.ModelName = selectedModel;
            SettingsManager.EnsureModelInList(selectedModel);
        }
        
        SettingsManager.SystemPrompt = SystemPromptBox.Text;
    }
}
