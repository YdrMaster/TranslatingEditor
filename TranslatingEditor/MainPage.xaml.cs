using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TranslatingEditor {
    public class SourceItem {
        public string Id;
        public string Name;
        public string Description;
    }

    public sealed partial class MainPage : Page {
        private readonly ViewMedel _view = new ViewMedel() {
            Source = "选取源文件",
            Target = "选取目标文件",
        };

        private readonly ObservableCollection<SourceItem> _sourceItems
            = new ObservableCollection<SourceItem>();

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
            text = SliceContent(text, "{", "}");

            var line = SplitHead(ref text);
            var title = SliceContent(line, "\"label\": \"", "\",").ToString();
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => _view.Title = title);

            text = SliceContent(text, "\"entries\": [", "]");
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.High, () => _sourceItems.Clear());

            while (text.Length > 5) {
                _ = SplitHead(ref text);
                var item = new SourceItem {
                    Id = SliceContent(SplitHead(ref text), "\"id\": \"", "\",").ToString(),
                    Name = SliceContent(SplitHead(ref text), "\"name\": \"", "\",").ToString(),
                    Description = SliceContent(SplitHead(ref text), "\"description\": \"", "\"").ToString(),
                };
                _ = SplitHead(ref text);
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => _sourceItems.Add(item));
            }

            Debug.WriteLine(text.ToString());
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count != 1 || !(e.AddedItems.First() is SourceItem item))
                return;
            NameBox.Text = item.Name;
            DescriptionBox.Text = item.Description;
        }

        private static ReadOnlySpan<char> SplitHead(ref ReadOnlySpan<char> source, char splitter = '\n') {
            var i = source.IndexOf(splitter);
            var head = source.Slice(0, i).TrimEnd();
            source = source.Slice(i + 1).TrimStart();
            return head;
        }

        private static ReadOnlySpan<char> SliceContent(ReadOnlySpan<char> source, string head, string tail) {
            Debug.Assert(source.StartsWith(head.AsSpan()));
            Debug.Assert(source.EndsWith(tail.AsSpan()));
            return source.Slice(head.Length, source.Length - head.Length - tail.Length).Trim();
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