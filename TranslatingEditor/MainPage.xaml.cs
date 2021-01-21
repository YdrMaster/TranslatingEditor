using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TranslatingEditor {
    public class SpellItem {
        public string Id;
        public string Name;
        public string Description;
    }

    public sealed partial class MainPage : Page {
        private readonly ViewMedel _view = new ViewMedel();

        private readonly ObservableCollection<SpellItem> _sourceItems
            = new ObservableCollection<SpellItem>();

        private Dictionary<string, SpellItem> _targetItems
            = new Dictionary<string, SpellItem>();

        public MainPage() => InitializeComponent();

        private void Source_Click(object sender, RoutedEventArgs e) {
            var locationPromptDialog = new ContentDialog {
                Title = "1",
                Content = "2",
                CloseButtonText = "好的",
            };

            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".json");
            picker.FileTypeFilter.Add(".txt");
            picker
                .PickSingleFileAsync()
                .AsTask()
                .ContinueWith(async future => {
                    var file = future.Result;
                    if (file == null) return;
                    _ = ModifyUI(() => {
                        _view.LoadSource(file.Name);
                        _sourceItems.Clear();
                    });
                    var text = await FileIO.ReadTextAsync(file);
                    Debug.WriteLine($"Read {text.Length} characters from source file.");
                    if (!ParseSource(text.AsSpan().Trim()))
                        _ = ModifyUI(async () => {
                            await new ContentDialog {
                                Title = "源文件解析失败",
                                Content = $"源文件中含有不正确的 json 格式，导致解析失败。已解析出 {_sourceItems.Count} 个法术。尚未解析的将被丢弃。建议修复并重新加载源文件。",
                                CloseButtonText = "好的",
                            }.ShowAsync();
                            _view.SourceLoaded=false;
                        });
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
                    _ = ModifyUI(() => _view.TargetFileName = file.Name);
                    var text = await FileIO.ReadTextAsync(file);
                    Debug.WriteLine($"Read {text.Length} characters from target file.");
                    if (string.IsNullOrWhiteSpace(text))
                        _ = ModifyUI(async () => {
                            var result = await new ContentDialog {
                                Title = "目标文件是空文件",
                                Content = "目标文件是空文件，将与源文件同步。",
                                SecondaryButtonText = "好的"
                            }.ShowAsync();
                            _view.IdNotMatch = false;
                            Sync();
                        });
                    else if (!ParseTarget(text.AsSpan().Trim()))
                        _ = ModifyUI(async () => {
                            var result = await new ContentDialog {
                                Title = "目标文件与源文件不匹配",
                                Content = "目标文件与源文件不匹配，这可能是因为二者标题不同、法术数目不同或法术标识符不能一一对应。点击重新加载，可重选目标文件，点击同步将修改目标文件以与源文件匹配。",
                                CloseButtonText = "重新加载",
                                SecondaryButtonText = "同步"
                            }.ShowAsync();
                            if (result == ContentDialogResult.Secondary) {
                                _view.IdNotMatch = false;
                                Sync();
                            } else
                                _view.IdNotMatch = true;
                        });
                    else
                        _view.IdNotMatch = false;
                });
        }

        private bool ParseSource(ReadOnlySpan<char> text) {
            try {
                text = SliceContent(text, "{", "}");

                var line = SplitHead(ref text);
                var title = SliceContent(line, "\"label\": \"", "\",").ToString();
                _ = ModifyUI(() => _view.Title = title);

                text = SliceContent(text, "\"entries\": [", "]");
                while (text.Length > 5) {
                    _ = SplitHead(ref text);
                    var item = new SpellItem {
                        Id = SliceContent(SplitHead(ref text), "\"id\": \"", "\",").ToString(),
                        Name = SliceContent(SplitHead(ref text), "\"name\": \"", "\",").ToString(),
                        Description = SliceContent(SplitHead(ref text), "\"description\": \"", "\"").ToString(),
                    };
                    _ = SplitHead(ref text);
                    _ = ModifyUI(() => _sourceItems.Add(item));
                }
            } catch (ParseFaildException) {
                return false;
            }

            Thread.Sleep(10);
            Debug.WriteLine($"Parsed {_sourceItems.Count} items from the source file.");

            return true;
        }

        private bool ParseTarget(ReadOnlySpan<char> text) {
            try {
                text = SliceContent(text, "{", "}");

                var line = SplitHead(ref text);
                var title = SliceContent(line, "\"label\": \"", "\",").ToString();
                if (title != _view.Title)
                    return false;

                text = SliceContent(text, "\"entries\": [", "]");
                while (text.Length > 5) {
                    _ = SplitHead(ref text);
                    var item = new SpellItem {
                        Id = SliceContent(SplitHead(ref text), "\"id\": \"", "\",").ToString(),
                        Name = SliceContent(SplitHead(ref text), "\"name\": \"", "\",").ToString(),
                        Description = SliceContent(SplitHead(ref text), "\"description\": \"", "\"").ToString(),
                    };
                    _ = SplitHead(ref text);
                    _targetItems.Add(item.Id, item);
                }
            } catch (ParseFaildException) {
                return false;
            }

            return CheckMap();
        }

        private bool CheckMap() {
            if (_sourceItems.Count != _targetItems.Count)
                return false;
            foreach (var item in _sourceItems)
                if (!_targetItems.ContainsKey(item.Id))
                    return false;
            return true;
        }

        private void Sync() {
            var legacy = _targetItems;
            _targetItems = new Dictionary<string, SpellItem>();
            foreach (var item in _sourceItems)
                _targetItems.Add(item.Id, legacy.TryGetValue(item.Id, out var spell) ? spell : item);
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count == 1 && e.AddedItems.First() is SpellItem item) {
                if (_view.IdNotMatch) {
                    NameBox.Text = item.Name;
                    DescriptionBox.Text = item.Description;
                } else {
                    item = _targetItems[item.Id];
                    NameBox.Text = item.Name;
                    DescriptionBox.Text = item.Description;
                }
            }
        }

        private IAsyncAction ModifyUI(DispatchedHandler task)
            => Dispatcher.RunAsync(CoreDispatcherPriority.Normal, task);

        private static ReadOnlySpan<char> SplitHead(ref ReadOnlySpan<char> source, char splitter = '\n') {
            var i = source.IndexOf(splitter);
            if (i == source.Length - 1 || i < 0) {
                var head = source;
                source = ReadOnlySpan<char>.Empty;
                return head;
            } else {
                var head = source.Slice(0, i).TrimEnd();
                source = source.Slice(i + 1).TrimStart();
                return head;
            }
        }

        private static ReadOnlySpan<char> SliceContent(ReadOnlySpan<char> source, string head, string tail) {
            if (source.StartsWith(head.AsSpan()) && source.EndsWith(tail.AsSpan()))
                return source.Slice(head.Length, source.Length - head.Length - tail.Length).Trim();
            throw new ParseFaildException();
        }

        private class ParseFaildException : Exception { }

        private class ViewMedel : Bindable {
            private string _title;
            private bool _sourceLoaded = false;
            private bool _idNotMatch = false;
            private string _sourceFileName = "选取源文件";
            private string _targetFileName = "选取目标文件";

            public string Title {
                get => _title;
                set => SetProperty(ref _title, value);
            }

            public bool SourceLoaded {
                get => _sourceLoaded;
                set {
                    if (!value && SetProperty(ref _sourceLoaded, value))
                        SetProperty(ref _sourceFileName, "选取源文件", nameof(SourceFileName));
                }
            }

            public string SourceFileName => _sourceFileName;

            public bool IdNotMatch {
                get => _idNotMatch;
                set => SetProperty(ref _idNotMatch, value);
            }

            public string TargetFileName {
                get => _targetFileName;
                set => SetProperty(ref _targetFileName, value);
            }

            public void LoadSource(string name) {
                SetProperty(ref _sourceFileName, name, nameof(SourceFileName));
                SetProperty(ref _sourceLoaded, true, nameof(SourceLoaded));
            }
        }
    }
}