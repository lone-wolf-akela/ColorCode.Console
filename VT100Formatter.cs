using ColorCode.Common;
using ColorCode.Parsing;
using ColorCode.Styling;
using System.Text;

namespace ColorCode.Console
{
    public class VT100Formatter : CodeColorizerBase
    {
        private const char Esc = (char)0x1B;

        /// <summary>
        /// Creates a <see cref="HtmlFormatter"/>, for creating HTML to display Syntax Highlighted code.
        /// </summary>
        /// <param name="Style">The Custom styles to Apply to the formatted Code.</param>
        /// <param name="languageParser">The language parser that the <see cref="HtmlFormatter"/> instance will use for its lifetime.</param>
        public VT100Formatter(StyleDictionary? Style = null, ILanguageParser? languageParser = null) : base(Style, languageParser)
        {
        }

        private TextWriter? Writer { get; set; }

        /// <summary>
        /// Creates the HTML Markup, which can be saved to a .html file.
        /// </summary>
        /// <param name="sourceCode">The source code to colorize.</param>
        /// <param name="language">The language to use to colorize the source code.</param>
        /// <returns>Colorised HTML Markup.</returns>
        public string GetVT100String(string sourceCode, ILanguage language)
        {
            var buffer = new StringBuilder(sourceCode.Length * 2);

            using (TextWriter writer = new StringWriter(buffer))
            {
                Writer = writer;
                languageParser.Parse(sourceCode, language, (parsedSourceCode, captures) => Write(parsedSourceCode, captures));
                writer.Flush();
            }

            return buffer.ToString();
        }

        protected override void Write(string parsedSourceCode, IList<Scope> scopes)
        {
            var styleInsertions = new List<TextInsertion>();

            foreach (Scope scope in scopes)
                GetStyleInsertionsForCapturedStyle(scope, styleInsertions);

            styleInsertions.SortStable((x, y) => x.Index.CompareTo(y.Index));

            int offset = 0;

            foreach (TextInsertion styleInsertion in styleInsertions)
            {
                var text = parsedSourceCode[offset..styleInsertion.Index];
                Writer?.Write(text);
                if (string.IsNullOrEmpty(styleInsertion.Text))
                    BuildSpanForCapturedStyle(styleInsertion.Scope);
                else
                    Writer?.Write(styleInsertion.Text);
                offset = styleInsertion.Index;
            }

            Writer?.Write(parsedSourceCode[offset..]);
        }

        private void GetStyleInsertionsForCapturedStyle(Scope scope, ICollection<TextInsertion> styleInsertions)
        {
            styleInsertions.Add(new TextInsertion
            {
                Index = scope.Index,
                Scope = scope
            });

            foreach (Scope childScope in scope.Children)
                GetStyleInsertionsForCapturedStyle(childScope, styleInsertions);

            styleInsertions.Add(new TextInsertion
            {
                Index = scope.Index + scope.Length,
                Text = String.Concat(Esc, "[0m")
            });
        }

        private void BuildSpanForCapturedStyle(Scope scope)
        {
            string foreground = string.Empty;
            string background = string.Empty;
            bool italic = false;
            bool bold = false;

            if (Styles.Contains(scope.Name))
            {
                Style style = Styles[scope.Name];

                foreground = style.Foreground;
                background = style.Background;
                italic = style.Italic;
                bold = style.Bold;
            }

            WriteElementStart(foreground, background, italic, bold);
        }
        public static string ToColorSeq(string? color)
        {
            if (color == null) return "0";

            var length = 6;
            var start = color.Length - length;
            var str_r = color.Substring(start, 2);
            var str_g = color.Substring(start + 2, 2);
            var str_b = color.Substring(start + 4, 2);
            var int_r = Convert.ToInt32(str_r, 16);
            var int_g = Convert.ToInt32(str_g, 16);
            var int_b = Convert.ToInt32(str_b, 16);
            return string.Concat(int_r, ";", int_g, ";", int_b);
        }
        private void WriteElementStart(string? foreground = null, string? background = null, bool italic = false, bool bold = false)
        {
            if (!string.IsNullOrWhiteSpace(foreground) || !string.IsNullOrWhiteSpace(background) || italic || bold)
            {
                if (!string.IsNullOrWhiteSpace(foreground))
                    Writer?.Write(string.Concat(Esc, "[38;2;", ToColorSeq(foreground), "m"));
                if (!string.IsNullOrWhiteSpace(background))
                    Writer?.Write(string.Concat(Esc, "[48;2;", ToColorSeq(background), "m"));

                if (italic)
                    Writer?.Write(string.Concat(Esc, "[1m"));

                if (bold)
                    Writer?.Write(string.Concat(Esc, "[3m"));
            }
        }
    }
}