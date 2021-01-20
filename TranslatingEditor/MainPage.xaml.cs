using System;
using System.Diagnostics;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TranslatingEditor {
    public sealed partial class MainPage : Page {
        private readonly ViewMedel _view = new ViewMedel() {
            Source = "选取源文件",
            Target = "选取目标文件",
        };

        public MainPage() => InitializeComponent();

        private void Source_Click(object sender, RoutedEventArgs e) {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".json");
            picker.FileTypeFilter.Add(".txt");
            picker
                .PickSingleFileAsync()
                .AsTask()
                .ContinueWith(async future => {
                    var file = future.Result;
                    if (file == null) return;
                    _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => _view.Source = file.Name);
                    var text = await Windows.Storage.FileIO.ReadTextAsync(file);
                    Debug.WriteLine($"Read {text.Length} characters from source file.");
                    ParseMapSource(text.AsSpan().Trim());
                });
        }

        private void Target_Click(object sender, RoutedEventArgs e) {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".json");
            picker.FileTypeFilter.Add(".txt");
            picker
                .PickSingleFileAsync()
                .AsTask()
                .ContinueWith(async future => {
                    var file = future.Result;
                    if (file == null) return;
                    _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => _view.Target = file.Name);
                    var text = await Windows.Storage.FileIO.ReadTextAsync(file);
                    Debug.WriteLine($"Read {text.Length} characters from target file.");
                });
        }

        private void ParseMapSource(ReadOnlySpan<char> text) {
            if (text[0] != '{' || text[text.Length - 1] != '}') {
                Debug.WriteLine("Parse error(1).");
                return;
            }
            text = text.Slice(1, text.Length - 2).Trim();

            var i = text.IndexOf('\n');
            var line = text.Slice(0, i).TrimEnd();

            const string LABEL = "\"label\": \"";
            if (!line.StartsWith(LABEL.AsSpan()) || !line.EndsWith("\",".AsSpan())) {
                Debug.WriteLine("Parse error(2).");
                return;
            }
            var title = line.Slice(LABEL.Length, line.Length - LABEL.Length - 2).ToString();
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => _view.Title = title);

            const string ENTRIES = "\"entries\": [";
            var others = text.Slice(i + 1).TrimStart();
            if (!others.StartsWith(ENTRIES.AsSpan()) || !others.EndsWith("]".AsSpan())) {
                Debug.WriteLine("Parse error(3).");
                return;
            }

            Debug.WriteLine("Done.");
        }

        private class ViewMedel : Bindable {
            private string _title;
            private string _source;
            private string _target;

            public string Title {
                get => _title;
                set => SetProperty(ref _title, value);
            }

            public string Source {
                get => _source;
                set => SetProperty(ref _source, value);
            }

            public string Target {
                get => _target;
                set => SetProperty(ref _target, value);
            }
        }
    }
}
