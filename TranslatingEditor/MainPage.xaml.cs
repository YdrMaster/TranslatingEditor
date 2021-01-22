using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
        private readonly ViewModel _view = new ViewModel();

        private readonly ObservableCollection<SpellItem> _sourceItems
            = new ObservableCollection<SpellItem>();

        private Dictionary<string, SpellItem> _targetItems
            = new Dictionary<string, SpellItem>();

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
            var i = 0;
            try {
                text = text.SliceContent("{", "}");

                var line = Functions.SplitHead(ref text);
                var title = line.SliceContent("\"label\": \"", "\",").ToString();
                _ = ModifyUI(() => _view.Title = title);

                text = text.SliceContent("\"entries\": [", "]");
                while (text.Length > 5) {
                    _ = Functions.SplitHead(ref text);
                    var item = new SpellItem {
                        Id = Functions.SplitHead(ref text).SliceContent("\"id\": \"", "\",").ToString(),
                        Name = Functions.SplitHead(ref text).SliceContent("\"name\": \"", "\",").ToString(),
                        Description = Functions.SplitHead(ref text).SliceContent("\"description\": \"", "\"").ToString(),
                    };
                    _ = Functions.SplitHead(ref text);
                    _ = ModifyUI(() => _sourceItems.Add(item));
                    ++i;
                }
            } catch (Functions.ParseFaildException) {
                return false;
            }
            Debug.WriteLine($"Parsed {i} items from the source file.");

            return true;
        }

        private bool ParseTarget(ReadOnlySpan<char> text) {
            try {
                text = text.SliceContent("{", "}");

                var line = Functions.SplitHead(ref text);
                var title = line.SliceContent("\"label\": \"", "\",").ToString();
                if (title != _view.Title) return false;

                text = text.SliceContent("\"entries\": [", "]");
                while (text.Length > 5) {
                    _ = Functions.SplitHead(ref text);
                    var item = new SpellItem {
                        Id = Functions.SplitHead(ref text).SliceContent("\"id\": \"", "\",").ToString(),
                        Name = Functions.SplitHead(ref text).SliceContent("\"name\": \"", "\",").ToString(),
                        Description = Functions.SplitHead(ref text).SliceContent("\"description\": \"", "\"").ToString(),
                    };
                    _ = Functions.SplitHead(ref text);
                    _targetItems[item.Id] = item;
                }
            } catch (Functions.ParseFaildException) {
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
            if (e.AddedItems.Count == 1 && e.AddedItems.First() is SpellItem item)
                _view.SetFocus(_view.IdNotMatch ? item : _targetItems[item.Id]);
        }

        private IAsyncAction ModifyUI(DispatchedHandler task)
            => Dispatcher.RunAsync(CoreDispatcherPriority.Normal, task);

        private class ViewModel : Bindable {
            private string _title;
            private bool _sourceLoaded = false;
            private bool _idNotMatch = true;
            private string _sourceFileName = "选择原文文件";
            private string _targetFileName = "选择译文文件";

            private FocusCache _cache = null;

            public string Title {
                get => _title;
                set => SetProperty(ref _title, value);
            }

            public bool SourceLoaded => _sourceLoaded;

            public string SourceFileName => _sourceFileName;

            public bool IdNotMatch {
                get => _idNotMatch;
                set {
                    if (SetProperty(ref _idNotMatch, value) && !value)
                        SetProperty(ref _targetFileName, "选择译文文件", nameof(SourceFileName));
                }
            }

            public string TargetFileName {
                get => _targetFileName;
                set => SetProperty(ref _targetFileName, value);
            }

            public void LoadSource(string name) {
                SetProperty(ref _sourceFileName, name, nameof(SourceFileName));
                SetProperty(ref _sourceLoaded, true, nameof(SourceLoaded));
            }

            public string FocusName {
                get => _cache?.Name ?? "";
                set {
                    if (_cache == null || _cache.Name == value)
                        return;
                    _cache.Name = value;
                    Notify(nameof(FocusName));
                }
            }

            public string FocusDescription {
                get => _cache?.Description ?? "";
                set {
                    if (_cache == null || _cache.Description == value)
                        return;
                    _cache.Description = value;
                    Notify(nameof(FocusDescription));
                }
            }

            public void SetFocus(SpellItem item) {
                _cache = new FocusCache(item);
                Notify(nameof(FocusName));
                Notify(nameof(FocusDescription));
            }
        }

        private class FocusCache {
            private readonly string _id;
            private string _description;
            private Task<string> _format;
            private List<(string, string)> _specialLabel = new List<(string, string)>();

            public FocusCache(SpellItem item) {
                _id = item.Id;
                Name = item.Name;
                _description = item.Description;
                Debug.Assert(_description.StartsWith("<p>"));
                _format = Task.Run(() => {
                    var builder = new StringBuilder();
                    var text = _description.AsSpan(3);
                    var labels = new Stack<(string, bool)>();
                    labels.Push(("p", false));

                    var jump = false;
                    var nol = 0;
                    while (!text.IsEmpty) {
                        var l_ = text.IndexOf('@');
                        var lb = text.IndexOf('<');
                        var le = text.IndexOf('>');
                        ReadOnlySpan<char> label, content;

                        if (0 <= l_ && l_ < lb) {
                            content = text.Slice(0, l_).TrimEnd();
                            if (!content.IsEmpty && !content.SequenceEqual("\\n".AsSpan()) && !content.SequenceEqual("\\r".AsSpan())) {
                                jump = true;
                                builder.Append(content.ToString());
                            }
                            lb = text.IndexOf('[');
                            le = text.IndexOf(']');
                            label = text.Slice(lb + 1, le - lb - 1).Trim();
                            _specialLabel.Add(("@", label.ToString()));
                            lb = text.IndexOf('{');
                            le = text.IndexOf('}');
                            content = text.Slice(lb + 1, le - lb - 1).Trim();
                            var temp = $"[{content.ToString()}](@) ";
                            builder.Append(jump ? $" {temp}" : temp);
                            text = text.Slice(le + 1).TrimStart();
                            continue;
                        }

                        if (lb < 0 || le < 0 || lb == text.Length - 1) {
                            Debug.WriteLine($"!!! {text.ToString()}");
                            throw new Functions.ParseFaildException();
                        }

                        content = text.Slice(0, lb).TrimEnd();
                        if (!content.IsEmpty && !content.SequenceEqual("\\n".AsSpan())) {
                            jump = true;
                            builder.Append(content.ToString());
                        }

                        label = text.Slice(lb + 1, le - lb - 1).Trim();
                        text = text.Slice(le + 1).TrimStart();

                        l_ = label.IndexOf(' ');
                        if (0 < l_ && !label.EndsWith(" /".AsSpan())) {
                            var actual = label.Slice(0, l_).ToString();
                            labels.Push((actual, true));
                            _specialLabel.Add((actual, label.Slice(l_ + 1).Trim().ToString()));
                            builder.Append(jump ? " [" : "[");
                        } else if (label[0] != '/') {
                            var l = label.ToString();
                            switch (l) {
                                case "p":
                                    // 段落标记
                                    labels.Push((l, false));
                                    jump = false;
                                    break;
                                case "strong":
                                case "b":
                                    // 粗体
                                    labels.Push((l, false));
                                    builder.Append(jump ? " **" : "**");
                                    break;
                                case "em":
                                case "i":
                                    // 斜体
                                    labels.Push((l, false));
                                    builder.Append(jump ? " *" : "*");
                                    break;
                                case "h2":
                                    labels.Push((l, false));
                                    builder.Append("## ");
                                    jump = false;
                                    break;
                                case "hr":
                                case "hr/":
                                case "hr /":
                                    // 分隔线
                                    builder.AppendLine("---");
                                    builder.AppendLine();
                                    jump = false;
                                    break;
                                case "br":
                                case "br/":
                                case "br /":
                                    // 换行符
                                    builder.AppendLine();
                                    jump = false;
                                    break;
                                case "ul":
                                    // 无序列表标记，没什么用，丢弃
                                    labels.Push((l, false));
                                    break;
                                case "ol":
                                    labels.Push((l, false));
                                    nol = 1;
                                    break;
                                case "li":
                                    labels.Push((l, false));
                                    builder.Append(nol > 0 ? $"{nol++} " : "- ");
                                    jump = false;
                                    break;
                                case "span":
                                    labels.Push((l, false));
                                    break;
                                default:
                                    Debug.WriteLine($"<{label.ToString()}>");
                                    builder.Append($"<{label.ToString()}>");
                                    break;
                            }
                        } else {
                            if (labels.Any()) {
                                var (temp, special) = labels.Pop();
                                Debug.Assert(label.Slice(1).SequenceEqual(temp.AsSpan()));
                                if (special)
                                    builder.Append($"]({temp}) ");
                                else
                                    switch (temp) {
                                        case "a":
                                            builder.Append("](a) ");
                                            break;
                                        case "p":
                                            builder.AppendLine();
                                            builder.AppendLine();
                                            break;
                                        case "strong":
                                        case "b":
                                            builder.Append("** ");
                                            break;
                                        case "em":
                                        case "i":
                                            builder.Append("* ");
                                            break;
                                        case "h2":
                                            builder.AppendLine();
                                            builder.AppendLine();
                                            break;
                                        case "ul":
                                            builder.AppendLine();
                                            break;
                                        case "ol":
                                            builder.AppendLine();
                                            nol = 0;
                                            break;
                                        case "li":
                                            builder.AppendLine();
                                            break;
                                        case "span":
                                            break;
                                        default:
                                            Debug.WriteLine($"??? <{label.ToString()}>");
                                            builder.Append($"<{label.ToString()}>");
                                            break;
                                    }
                            } else {
                                Debug.WriteLine($"!!! <{label.ToString()}>");
                                builder.Append($"<{label.ToString()}>");
                            }
                        }
                    }

                    return builder.ToString();
                });
            }

            public string Name { get; set; }

            public string Description {
                get => _format.Result;
                set { }
            }
        }
    }
}