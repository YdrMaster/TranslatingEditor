using System;
using System.Diagnostics;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TranslatingEditor {
    public sealed partial class MainPage : Page {
        public MainPage() => InitializeComponent();

        private string text;

        private void Button_Click(object sender, RoutedEventArgs e) {
            _ = ((Button)sender).Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add(".json");
                var file = await picker.PickSingleFileAsync();
                if (file == null)
                    return;
                TitleBlock.Text = file.Name;
                _ = Windows.Storage.FileIO.ReadTextAsync(file).AsTask().ContinueWith(content => {
                    text = content.Result;
                    Debug.WriteLine($"Done. Read {text.Length} characters.");
                });
            });
        }
    }
}
