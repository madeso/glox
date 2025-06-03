using System.Collections.Immutable;
using System.Xml;
using Spectre.Console;

namespace glox_sharp;

public static class Parser
{
    internal static void Parse(El root)
    {
        var comments = root.ElementsNamed("comment").Select(ParseComment).ToImmutableArray();
        var types = root.ElementsNamed("types").SelectMany(ParseTypes).ToImmutableArray();
        var kinds = root.ElementsNamed("kinds").SelectMany(ParseKinds).ToImmutableArray();
        var enums = root.ElementsNamed("enums").Select(ParseEnum).ToImmutableArray();

        AnsiConsole.WriteLine("done parsing");
    }

    private static IEnumerable<Kind> ParseKinds(El root)
    {
        foreach (var el in root.ElementsNamed("kind"))
        {
            var name = el.ReadAttribute("name");
            var description = el.ReadAttribute("desc");
            if (name == null)
            {
                el.ReportError("Missing name");
                continue;
            }
            if (description == null)
            {
                el.ReportError("Missing description", name);
                continue;
            }
            yield return new Kind(name, description);
        }
    }

    private static IEnumerable<Type> ParseTypes(El root)
    {
        foreach(var el in root.ElementsNamed("type"))
        {
            var nameFromElem = el.ElementsNamed("name").SelectMany(r => r.ReadInnerText()).FirstOrDefault();
            var code = string.Join("", el.ReadInnerText());
            var apis = el.ElementsNamed("apientry").Count();
            var name = nameFromElem ?? el.ReadAttribute("name");
            var comment = el.ReadAttribute("comment");
            var requires = el.ReadAttribute("requires");
            if (name == null)
            {
                el.ReportError("Missing name");
                continue;
            }
            yield return new Type(name, code, comment, requires);
        }
    }

    private static string ParseComment(El el)
    {
        var r = el.ReadInnerText();
        if (r.Length != 0) return string.Join("", r);
        el.ReportError("Missing documentation");
        return string.Empty;
    }

    private static EnumType ParseEnum(El el)
    {
        var ns = el.ReadAttribute("namespace");
        if (ns == null)
        {
            el.ReportError("Missing namespace attribute");
            ns = "missing";
        }

        var group = el.ReadAttribute("group");
        var type = el.ReadAttribute("type");
        var comment = el.ReadAttribute("comment");
        var vendor = el.ReadAttribute("vendor");
        var start = el.ReadAttribute("start");
        var end = el.ReadAttribute("end");
        return new EnumType();
    }
}

// todo(Gustav): resolve the Requires field
internal record Type(string Name, string Code, string? Comment, string? Requires);

internal record Kind(string Name, string Description);

internal class EnumType
{
    // ERROR : /registry/enums[1]: Unused elements: enum, unused
}
