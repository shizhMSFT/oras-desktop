using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OrasProject.OrasDesktop.Models;

namespace OrasProject.OrasDesktop.Views
{
    public partial class LoginDialog : UserControl
    {
        public string RegistryUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public AuthenticationType AuthType { get; set; } = AuthenticationType.Basic;
        public bool Result { get; private set; }

        public LoginDialog()
        {
            InitializeComponent();

            CancelButton.Click += (s, e) => 
            {
                Result = false;
                CloseDialog();
            };

            LoginButton.Click += (s, e) => 
            {
                Username = UsernameTextBox.Text ?? string.Empty;
                Password = PasswordTextBox.Text ?? string.Empty;
                Token = TokenTextBox.Text ?? string.Empty;
                Result = true;
                CloseDialog();
            };

            AuthTypeComboBox.SelectedIndex = 0;
        }

        public void Initialize(string registryUrl)
        {
            RegistryUrl = registryUrl;
            RegistryTextBlock.Text = registryUrl;
        }

        private void AuthTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AuthTypeComboBox.SelectedIndex == 0)
            {
                AuthType = AuthenticationType.Basic;
                BasicAuthPanel.IsVisible = true;
                TokenAuthPanel.IsVisible = false;
            }
            else
            {
                AuthType = AuthenticationType.Token;
                BasicAuthPanel.IsVisible = false;
                TokenAuthPanel.IsVisible = true;
            }
        }

        private void CloseDialog()
        {
            if (Parent is Window window)
            {
                window.Close();
            }
        }

        public static async Task<(bool Result, AuthenticationType AuthType, string Username, string Password, string Token)> ShowDialog(Window parent, string registryUrl)
        {
            var dialog = new Window
            {
                Title = "Registry Authentication",
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                SizeToContent = SizeToContent.Height
            };

            var content = new LoginDialog();
            content.Initialize(registryUrl);
            dialog.Content = content;

            await dialog.ShowDialog(parent);

            return (content.Result, content.AuthType, content.Username, content.Password, content.Token);
        }
    }
}