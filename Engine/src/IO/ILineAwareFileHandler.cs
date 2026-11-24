namespace RhyCiv.Engine;

/// <summary>
/// Opt-in line numbers for an <see cref="IFileHandler"/>.
/// <para>
/// <see cref="IFileHandler.ProcessSection"/> deliberately keeps its
/// <c>(string, List&lt;string&gt;)</c> signature, because eight loaders implement it
/// and most do not care where a line came from. A handler that wants to report where
/// it went wrong implements this interface instead: <c>TextFileParser.ParseFile</c>
/// sets <see cref="SectionStartLine"/> to the 1-based line on which the current
/// section's content begins, immediately before each <c>ProcessSection</c> call.
/// </para>
/// <para>
/// A section's content lines are contiguous in the file - a blank line, a ';' line or
/// a new '@' header ends the section - so a handler can turn the index of one of its
/// content lines into an absolute line number by adding it to this value.
/// </para>
/// </summary>
public interface ILineAwareFileHandler
{
    int SectionStartLine { get; set; }
}
