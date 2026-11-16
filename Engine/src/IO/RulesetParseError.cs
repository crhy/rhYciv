using System;
using System.IO;

namespace RhyCiv.Engine.IO
{
    /// <summary>
    /// A ruleset file that a mod author edited into a shape the parser cannot read.
    /// <para>
    /// <c>RULES.txt</c>, <c>CITY.txt</c> and <c>Game.txt</c> are user-editable, so
    /// truncation, reordering and typos are ordinary events rather than exceptional
    /// ones. Before this type existed, a short line or a stray letter surfaced as an
    /// <c>IndexOutOfRangeException</c> or a <c>FormatException</c> - usually well after
    /// startup, with no mention of the file that caused it, and no way for the author
    /// to know which line to fix. This error names the file, the section, the 1-based
    /// line and the offending text.
    /// </para>
    /// </summary>
    public class RulesetParseError : Exception
    {
        /// Full path of the ruleset file that failed to parse.
        public string FilePath { get; }

        /// Section header, without the leading '@', that the bad line sits in.
        public string Section { get; }

        /// <summary>
        /// 1-based line number in the file. When the problem is with a section as a
        /// whole (too few lines, say) this is the line the section starts on, and the
        /// message says so.
        /// </summary>
        public int Line { get; }

        /// The offending line, or null when the problem is with the section as a whole.
        public string? LineText { get; }

        public RulesetParseError(string filePath, string section, int line, string detail,
            string? lineText = null, Exception? inner = null)
            : base(BuildMessage(filePath, section, line, detail, lineText), inner)
        {
            FilePath = filePath;
            Section = section;
            Line = line;
            LineText = lineText;
        }

        /// <summary>
        /// A handler that died on an index, a range, a cast or a number format. The
        /// line that caused it is not recoverable from the exception, so the section's
        /// own start line is reported and the message says which that is.
        /// </summary>
        public static RulesetParseError FromHandlerFailure(string filePath, string section, int sectionStartLine,
            Exception failure)
        {
            return new RulesetParseError(filePath, section, sectionStartLine,
                $"{failure.GetType().Name}: {failure.Message} while reading this section, " +
                $"reported at the line the section starts on",
                inner: failure);
        }

        private static string BuildMessage(string filePath, string section, int line, string detail, string? lineText)
        {
            var message = $"{Path.GetFileName(filePath)} section {section}, line {line}: {detail}";
            if (!string.IsNullOrEmpty(lineText))
            {
                message += $" (\"{lineText}\")";
            }

            return message;
        }
    }
}
