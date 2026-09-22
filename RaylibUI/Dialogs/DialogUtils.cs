using Model;
using Raylib_CSharp.Fonts;

namespace RaylibUI;

public static class DialogUtils
{
    /// <summary>
    /// Returns a list of strings for wrapped text
    /// </summary>
    public static List<string> GetWrappedTexts(string text, int maxWidth, Font font, int fontSize)
    {
        var wrappedLines = new List<string>();
        maxWidth = Math.Max(1, maxWidth);

        foreach (var sourceLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            var words = sourceLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                wrappedLines.Add(string.Empty);
                continue;
            }

            var currentLine = string.Empty;
            foreach (var word in words)
            {
                var candidate = currentLine.Length == 0 ? word : $"{currentLine} {word}";
                if (currentLine.Length > 0 && TextRendering.Measure(font, candidate, fontSize, 0f).X > maxWidth)
                {
                    wrappedLines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = candidate;
                }
            }

            wrappedLines.Add(currentLine);
        }

        return wrappedLines;
    }

    /// <summary>
    /// Find occurences of %STRING and %NUMBER in text and replace it with other strings/numbers.
    /// </summary>
    /// <param name="text">Text where replacement takes place.</param>
    /// <param name="replacementStrings">A list of strings to replace %STRING0, %STRING1, %STRING2, etc.</param>
    /// <param name="replacementNumbers">A list of integers to replace %NUMBER0, %NUMBER1, %NUMBER2, etc.</param>
    public static string ReplacePlaceholders(string text, IList<string>? replacementStrings, IList<int>? replacementNumbers)
    {

        if (replacementStrings != null)
        {
            var index = text.IndexOf("%STRING", StringComparison.Ordinal);
            while (index != -1)
            {
                // A token at the very end of the text has no digit after it, and
                // a non-digit index would index the list at -1. Either took the
                // game down while trying to fill in a message.
                if (index + 7 >= text.Length)
                {
                    break;
                }

                var numericChar = text[index + 7];
                var slot = (int)char.GetNumericValue(numericChar);
                if (slot < 0 || slot >= replacementStrings.Count)
                {
                    // Leave the malformed token as literal text and move past it.
                    var next = text.IndexOf("%STRING", index + 7, StringComparison.Ordinal);
                    if (next == index)
                    {
                        next = text.IndexOf("%STRING", index + 1, StringComparison.Ordinal);
                    }
                    index = next;
                    continue;
                }

                text = text.Replace("%STRING" + numericChar, replacementStrings[slot]);
                index = text.IndexOf("%STRING", StringComparison.Ordinal);
            }
        }

        if (replacementNumbers != null)
        {
            var index = text.IndexOf("%NUMBER", StringComparison.Ordinal);
            while (index != -1)
            {
                if (index + 7 >= text.Length)
                {
                    break;
                }

                var numericChar = text[index + 7];
                var slot = (int)char.GetNumericValue(numericChar);
                if (slot < 0 || slot >= replacementNumbers.Count)
                {
                    var next = text.IndexOf("%NUMBER", index + 7, StringComparison.Ordinal);
                    if (next == index)
                    {
                        next = text.IndexOf("%NUMBER", index + 1, StringComparison.Ordinal);
                    }
                    index = next;
                    continue;
                }

                text = text.Replace("%NUMBER" + numericChar, replacementNumbers[slot].ToString());
                index = text.IndexOf("%NUMBER", StringComparison.Ordinal);
            }
        }

        return text;
    }
}
