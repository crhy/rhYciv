
using System.Collections.Generic;
using System.IO;

namespace RhyCiv.Engine
{
    public static class TextFileParser
    {
        /// <summary>
        /// A section starts at a bare <c>@NAME</c> line. A line like
        /// <c>@title=...</c> or <c>@width=320</c> is a keyed property of the
        /// current section and is passed through to the handler as content, not
        /// treated as the start of a new section.
        /// </summary>
        private static bool IsSectionHeader(string? line) =>
            line != null && line.StartsWith('@') && !line.Contains('=');

        public static void ParseFile(string filePath, IFileHandler handler, bool allowBlanks = false)
        {
            if(string.IsNullOrWhiteSpace(filePath)) return;
            
            using var file = new StreamReader(filePath);

            var lineAware = handler is ILineAwareFileHandler aware ? aware : null;

            string? line;
            string? section = null;
            List<string> contents = new();
            // 1-based number of the line most recently read from the file.
            int lineRead = 0;
            // 1-based line on which the current section's content begins, i.e. the
            // line after its '@NAME' header. Handlers that implement ILineAwareFileHandler
            // are told this before each ProcessSection call.
            int contentStartLine = 0;

            string? ReadLine()
            {
                lineRead++;
                return file.ReadLine();
            }

            void ProcessSectionAt(string? name, List<string> body, int startLine)
            {
                if (lineAware != null) lineAware.SectionStartLine = startLine;
                handler.ProcessSection(name, body);
            }

            while ((line = ReadLine()) != null)
            {
                if (section != null)
                {
                    if (allowBlanks && string.IsNullOrWhiteSpace(line))
                    {
                        line = ReadLine();
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';'))
                        {
                            ProcessSectionAt(section, contents, contentStartLine);
                            section = null;
                        }
                        else if (IsSectionHeader(line))
                        {
                            ProcessSectionAt(section, contents, contentStartLine);
                            section = line![1..];
                            contents.Clear();
                            contentStartLine = lineRead + 1;
                        }
                        else
                        {
                            // The blank line and the line that follows it are both
                            // content, and they sit on consecutive lines, so the
                            // section's start line is unchanged.
                            contents.Add("");
                            contents.Add(line);
                        }
                    }else if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';'))
                    {
                        ProcessSectionAt(section, contents, contentStartLine);
                        section = null;
                    }
                    else if (IsSectionHeader(line))
                    {
                        ProcessSectionAt(section, contents, contentStartLine);
                        section = line[1..].TrimEnd();
                        contents = new List<string>();
                        contentStartLine = lineRead + 1;
                    }
                    else
                    {
                        contents.Add(line);
                    }
                }
                else
                {
                    if (!line.StartsWith('@')) continue;
                    section = line[1..].TrimEnd();
                    contents = new List<string>();
                    contentStartLine = lineRead + 1;
                }
            }

            if (section != null && contents.Count > 0)
            {
                ProcessSectionAt(section, contents, contentStartLine);
            }
        }
    }
}