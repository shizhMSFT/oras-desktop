using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace OrasProject.OrasDesktop.Views
{
    public partial class CopyManifestDialog : UserControl
    {
        public string SourceTag { get; set; } = string.Empty;
        public string DestinationTag { get; set; } = string.Empty;
        public bool Result { get; private set; }

        public CopyManifestDialog()
        {
            InitializeComponent();

            CancelButton.Click += (s, e) => 
            {
                Result = false;
                CloseDialog();
            };

            CopyButton.Click += (s, e) => 
            {
                DestinationTag = DestinationTagTextBox.Text ?? string.Empty;
                Result = true;
                CloseDialog();
            };
        }

        public void Initialize(string sourceTag)
        {
            SourceTag = sourceTag;
            SourceTagTextBlock.Text = sourceTag;
            DestinationTagTextBox.Text = $"{sourceTag}-copy";
        }

        private void CloseDialog()
        {
            if (Parent is Window window)
            {
                window.Close();
            }
        }

        public static async Task<(bool Result, string DestinationTag)> ShowDialog(Window parent, string sourceTag)
        {
            var dialog = new Window
            {
                Title = "Copy Manifest",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                SizeToContent = SizeToContent.Height
            };

            var content = new CopyManifestDialog();
            content.Initialize(sourceTag);
            dialog.Content = content;

            await dialog.ShowDialog(parent);

            return (content.Result, content.DestinationTag);
        }
    }
}