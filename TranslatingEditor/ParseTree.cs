using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TranslatingEditor {
    internal interface IParseTree { }

    internal interface IBranch<T> : IParseTree where T : IParseTree {
        IList<T> Branches { get; }
    }

    internal interface IHtmlTree : IParseTree {
        string ToHtml();

        string ToMarkdown();
    }

    internal class HtmlLabel : IHtmlTree {
        public string Label;
        public string Attributes;

        public string ToHtml() => $"{Environment.NewLine}<{Label} {Attributes}/>{Environment.NewLine}";

        public string ToMarkdown() {
            switch (Label) {
                case "br":
                    return $"{Environment.NewLine}";
                case "hr":
                    return $"{Environment.NewLine}---{Environment.NewLine}";
                default:
                    return "";
            }
        }
    }

    internal class HtmlText : IHtmlTree {
        public string Text;

        public string ToHtml() => Text;

        public string ToMarkdown() => Text;
    }

    internal class HtmlBranch : IHtmlTree, IBranch<IHtmlTree> {
        private static readonly HashSet<string> HtmlFormatNewLine = new HashSet<string> { "body", "p", "h2", "hr", "br" };

        public HtmlLabel Label;

        public IList<IHtmlTree> Branches { get; set; }

        public string ToHtml() {
            var builder = new StringBuilder();

            var newLine = HtmlFormatNewLine.Contains(Label.Label);

            if (newLine)
                builder.AppendLine();

            builder.Append('<');
            builder.Append(Label.Label);

            if (!string.IsNullOrEmpty(Label.Attributes)) {
                builder.Append(' ');
                builder.Append(Label.Attributes);
            }

            if (Branches != null && Branches.Any()) {
                builder.Append('>');
                if (newLine)
                    builder.AppendLine();
                foreach (var branch in Branches)
                    builder.Append(branch.ToHtml());
                if (newLine)
                    builder.AppendLine();
                builder.Append("</");
                builder.Append(Label.Label);
                builder.Append('>');
            } else
                builder.Append($"></{Label.Label}>");

            if (newLine)
                builder.AppendLine();

            return builder.ToString();
        }

        public string ToMarkdown() {
            var builder = new StringBuilder();
            var newLine = HtmlFormatNewLine.Contains(Label.Label);
            string prefix, suffix;
            switch (Label.Label) {
                case "body":
                case "p":
                    prefix = suffix = "";
                    break;
                case "h2":
                    prefix = "## ";
                    suffix = "";
                    break;
                case "strong":
                case "em":
                    prefix = suffix = "**";
                    break;
                default:
                    prefix = "[";
                    suffix = $"]({Label.Label})";
                    break;
            }
            if (newLine)
                builder.AppendLine();
            builder.Append(prefix);
            foreach (var p in Branches)
                builder.Append(p.ToMarkdown());
            builder.Append(suffix);
            if (newLine)
                builder.AppendLine();
            return builder.ToString();
        }
    }

    internal static class HtmlParser {
        private static ReadOnlySpan<T> SliceByIndex<T>(this ReadOnlySpan<T> self, int begin, int end) {
            switch (end) {
                case -1:
                    return self.Slice(begin);
                case 0:
                    return ReadOnlySpan<T>.Empty;
                default:
                    if (end <= begin)
                        return ReadOnlySpan<T>.Empty;
                    return self.Slice(begin, end - begin);
            }
        }

        private static List<IHtmlTree> Parse(ReadOnlySpan<char> span) {
            var branches = new List<IHtmlTree>();

            while (!span.IsEmpty) {
                var begin = span.IndexOf('<');
                switch (begin) {
                    case -1: {
                            var text = span.ToString();
                            if (text != "\\n")
                                branches.Add(new HtmlText { Text = text });
                            span = Span<char>.Empty;
                        }
                        break;
                    case 0: {
                            var end = span.IndexOf('>');
                            if (end == -1)
                                throw new ApplicationException("html with '<' but without '>'");
                            if (end < 2)
                                throw new ApplicationException("blank html label");

                            var content = span.Slice(1, end - 1).Trim();
                            var splitter = content.IndexOf(' ');
                            span = span.Slice(end + 1).TrimStart();

                            if (content[content.Length - 1] == '/')
                                if (splitter == -1)
                                    branches.Add(new HtmlLabel {
                                        Label = content.Slice(0, end - 1).ToString(),
                                        Attributes = ""
                                    });
                                else
                                    branches.Add(new HtmlLabel {
                                        Label = content.Slice(0, splitter).ToString(),
                                        Attributes = content.SliceByIndex(splitter + 1, end - 2).Trim().ToString()
                                    });
                            else {
                                ReadOnlySpan<char> label, attributes;
                                if (splitter == -1) {
                                    label = content;
                                    attributes = Span<char>.Empty;
                                } else {
                                    label = content.Slice(0, splitter);
                                    attributes = content.Slice(splitter + 1).TrimStart();
                                }

                                if (label.SequenceEqual("hr".AsSpan()) || label.SequenceEqual("br".AsSpan())) {
                                    branches.Add(new HtmlLabel {
                                        Label = label.ToString(),
                                        Attributes = attributes.ToString()
                                    });
                                } else {
                                    var slice = span;
                                    while (true) {
                                        begin = slice.IndexOf('<');
                                        end = slice.IndexOf('>');
                                        if (begin == -1 || begin > end)
                                            throw new ApplicationException("html with label header but without tail");
                                        if (slice[begin + 1] != '/' || !slice.SliceByIndex(begin + 2, end).Trim().SequenceEqual(label))
                                            slice = slice.Slice(end + 1);
                                        else {
                                            var sliced = span.Length - slice.Length;
                                            begin += sliced;
                                            end += sliced;
                                            break;
                                        }
                                    }

                                    var inner = span.Slice(0, begin).TrimEnd();
                                    branches.Add(new HtmlBranch {
                                        Label = new HtmlLabel {
                                            Label = label.ToString(),
                                            Attributes = attributes.ToString()
                                        },
                                        Branches = Parse(inner)
                                    });
                                    span = span.Slice(end + 1).TrimStart();
                                }
                            }
                        }
                        break;
                    default: {
                            var text = span.Slice(0, begin).ToString();
                            if (text != "\\n")
                                branches.Add(new HtmlText { Text = text });
                            span = span.Slice(begin);
                        }
                        break;
                }
            }

            return branches;
        }

        public static HtmlBranch Parse(string text) =>
            new HtmlBranch {
                Label = new HtmlLabel { Label = "body" },
                Branches = Parse(text.AsSpan().Trim())
            };
    }
}
