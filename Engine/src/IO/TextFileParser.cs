
using System.Collections.Generic;
using System.IO;

namespace Civ2engine
{
    public static class TextFileParser
    {
        public static void ParseFile(string filePath, IFileHandler handler, bool allowBlanks = false)
        {
            if(string.IsNullOrWhiteSpace(filePath)) return;
            
            using var file = new StreamReader(filePath);
            
            string? line;
            string? section = null;
            List<string> contents = new();
            while ((line = file.ReadLine()) != null)
            {
                if (section != null)
                {
                    if (allowBlanks && string.IsNullOrWhiteSpace(line))
                    {
                        line = file.ReadLine();
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';'))
                        {
                            handler.ProcessSection(section, contents);
                            section = null;
                        }
                        else if (line.StartsWith('@'))
                        {
                            handler.ProcessSection(section, contents);
                            section = line[1..];
                            contents.Clear();
                        }
                        else
                        {
                            contents.Add("");
                            contents.Add(line);
                        }
                    }else if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';'))
                    {
                        handler.ProcessSection(section, contents);
                        section = null;
                    }
                    // A bare "@SECTION" header on its own line starts a new section, but only
                    // for files whose sections are not blank-line separated. The popup/intro
                    // format (allowBlanks) uses "@title=", "@width=", "@button=", "@options"
                    // etc. as section *content* that PopupBoxReader parses, so those "@" lines
                    // must not be mistaken for section breaks there - sections in that format
                    // are always terminated by the blank-line handling above.
                    else if (!allowBlanks && line.StartsWith('@'))
                    {
                        handler.ProcessSection(section, contents);
                        section = line[1..].TrimEnd();
                        contents = new List<string>();
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
                }
            }

            if (section != null && contents.Count > 0)
            {
                handler.ProcessSection(section, contents);
            }
        }
    }
}