using Microsoft.UI.Xaml.Controls;

namespace CodingSahayi;

public sealed partial class SettingsDialog : ContentDialog
{
    public SettingsDialog()
    {
        this.InitializeComponent();
        
        ApiKeyBox.Password = SettingsManager.SecureApiKey;
        EndpointBox.Text = SettingsManager.ApiEndpoint;
        ModelNameBox.Text = SettingsManager.ModelName;
        SystemPromptBox.Text = SettingsManager.SystemPrompt;
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        SettingsManager.SecureApiKey = ApiKeyBox.Password;
        SettingsManager.ApiEndpoint = EndpointBox.Text;
        SettingsManager.ModelName = ModelNameBox.Text;
        SettingsManager.SystemPrompt = SystemPromptBox.Text;
    }
}
