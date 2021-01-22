using System;

namespace TranslatingEditor {
    internal static class Functions {
        public class ParseFaildException : Exception { }

        public static ReadOnlySpan<char> SplitHead(ref ReadOnlySpan<char> source, char splitter = '\n') {
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

        public static ReadOnlySpan<char> SliceContent(this ReadOnlySpan<char> source, string head, string tail) {
            if (source.StartsWith(head.AsSpan()) && source.EndsWith(tail.AsSpan()))
                return source.Slice(head.Length, source.Length - head.Length - tail.Length).Trim();
            throw new ParseFaildException();
        }
    }
}
