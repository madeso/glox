using System.Collections.Immutable;

namespace Glox.Registry;

// Data model for the Khronos OpenGL API Registry Schema

public enum EnumKind
{
    Default,
    Bitmask
}

public sealed record Registry(
    ImmutableArray<TypeDef> Types,
    ImmutableArray<KindDef> Kinds,
    ImmutableArray<GroupDef> Groups,
    ImmutableArray<EnumType> Enums, // changed from EnumType
    ImmutableArray<CommandDef> Commands,
    ImmutableArray<FeatureDef> Features,
    ImmutableArray<ExtensionDef> Extensions,
    ImmutableArray<string> Comments
);

public sealed record TypeDef(
    string Name,
    string? Requires,
    string? Comment,
    string? ApiEntry,
    string CodeBlock
);

public sealed record KindDef(
    string Name,
    string? Desc
);

public sealed record GroupDef(
    string Name,
    ImmutableArray<GroupEnumRef> Enums
);

public sealed record GroupEnumRef(
    string Name
);

public sealed record EnumType(
    int Index,
    string? Namespace,
    EnumKind Type,
    string? Vendor,
    string? Comment,
    string? Start,
    string? End,
    string? Group,
    ImmutableArray<EnumValue> Enums,
    ImmutableArray<UnusedDef> Unused);

public sealed record EnumValue(
    string Name,
    string? Value,
    string? Api,
    string? Type,
    string? Group,
    string? Alias,
    string? Comment // Added comment attribute
);

public sealed record UnusedDef(
    string? Start,
    string? End,
    string? Vendor,
    string? Comment
);

public sealed record CommandDef(
    ProtoDef Proto,
    ImmutableArray<ParamDef> Params,
    string? Alias,
    string? VecEquiv,
    GlxDef? Glx,
    string? Comment,
    string? Namespace // Added namespace attribute from <commands>
);

public sealed record ProtoDef(
    string? Group,
    string? Kind,
    string? Ptype,
    string? ApiEntry,
    string? Class, // Added class attribute
    string Name,
    ImmutableArray<string> Body
);

public sealed record ParamDef(
    string? Group,
    string? Kind,
    string? Len,
    string? Class,
    string? Ptype,
    string? ApiEntry,
    string Name,
    ImmutableArray<string> Body
);

public sealed record GlxDef(
    string? Type,
    string? Opcode
);

public sealed record FeatureDef(
    string Api,
    string Name,
    string? Protect,
    string Number,
    string? Comment,
    ImmutableArray<InterfaceDef> Require,
    ImmutableArray<InterfaceDef> Remove
);

public sealed record ExtensionDef(
    string Name,
    string Supported,
    string? Protect,
    string? Comment,
    ImmutableArray<InterfaceDef> Require,
    ImmutableArray<InterfaceDef> Remove
);

public sealed record InterfaceDef(
    string? Profile,
    string? Api,
    string? Comment,
    ImmutableArray<RequireEnum> Enums,
    ImmutableArray<RequireCommand> Commands,
    ImmutableArray<RequireType> Types
);

public sealed record RequireEnum(string Name, string? Comment);
public sealed record RequireCommand(string Name, string? Comment);
public sealed record RequireType(string Name, string? Comment);

public static class Parser
{
    private static TypeDef ParseTypeDef(El el)
    {
        var name = el.ElementsNamed("name").Select(n => string.Join("", n.ReadInnerText())).FirstOrDefault() ?? el.ReadAttribute("name");
        var count = el.ElementsNamed("apientry").Count();
        var requires = el.ReadAttribute("requires");
        var api = el.ReadAttribute("api");
        var comment = el.ReadAttribute("comment");
        var body = string.Join("", el.ReadInnerText());
        if (name == null)
        {
            el.ReportError("Missing name for type");
            name = "missing";
        }
        return new TypeDef(name, requires, api, comment, body);
    }

    private static KindDef ParseKindDef(El el)
    {
        var name = el.ReadAttribute("name") ?? "";
        var desc = el.ReadAttribute("desc");
        return new KindDef(name, desc);
    }

    private static GroupDef ParseGroupDef(El el)
    {
        var name = el.ReadAttribute("name") ?? "";
        var enums = el.ElementsNamed("enum")
            .Select(e => new GroupEnumRef(e.ReadAttribute("name") ?? "")).ToImmutableArray();
        return new GroupDef(name, enums);
    }

    private static EnumType ParseEnumsType(El el, int index)
    {
        var ns = el.ReadAttribute("namespace");
        var typeStr = el.ReadAttribute("type");
        var type = typeStr switch
        {
            "bitmask" => EnumKind.Bitmask,
            null => EnumKind.Default,
            _ => InvalidTypeStr(el, typeStr)
        };
        var vendor = el.ReadAttribute("vendor");
        var comment = el.ReadAttribute("comment");
        var start = el.ReadAttribute("start");
        var end = el.ReadAttribute("end");
        var group = el.ReadAttribute("group");
        var enums = el.ElementsNamed("enum").Select(ParseEnumValue).ToImmutableArray();
        var unused = el.ElementsNamed("unused").Select(ParseUnusedDef).ToImmutableArray();
        return new EnumType(index, ns, type, vendor, comment, start, end, group, enums, unused);

        static EnumKind InvalidTypeStr(El el, string typeStr)
        {
            el.ReportError("Invalid type", $"Got type {typeStr}");
            return EnumKind.Default;
        }
    }

    private static EnumValue ParseEnumValue(El el)
    {
        var name = el.ReadAttribute("name") ?? "";
        var value = el.ReadAttribute("value");
        var api = el.ReadAttribute("api");
        var type = el.ReadAttribute("type");
        var group = el.ReadAttribute("group");
        var alias = el.ReadAttribute("alias");
        var comment = el.ReadAttribute("comment"); // Added comment attribute
        return new EnumValue(name, value, api, type, group, alias, comment);
    }

    private static UnusedDef ParseUnusedDef(El el)
    {
        var start = el.ReadAttribute("start");
        var end = el.ReadAttribute("end");
        var vendor = el.ReadAttribute("vendor");
        var comment = el.ReadAttribute("comment");
        return new UnusedDef(start, end, vendor, comment);
    }

    private static CommandDef ParseCommandDef(El el, string? commandsNamespace)
    {
        var proto = el.ElementsNamed("proto").Select(ParseProtoDef).First();
        var @params = el.ElementsNamed("param").Select(ParseParamDef).ToImmutableArray();
        var alias = el.ElementsNamed("alias").Select(a => a.ReadAttribute("name")).FirstOrDefault();
        var vecequiv = el.ElementsNamed("vecequiv").Select(a => a.ReadAttribute("name")).FirstOrDefault();
        var glx = el.ElementsNamed("glx").Select(ParseGlxDef).FirstOrDefault();
        var comment = el.ReadAttribute("comment");
        return new CommandDef(proto, @params, alias, vecequiv, glx, comment, commandsNamespace);
    }

    private static ProtoDef ParseProtoDef(El el)
    {
        var group = el.ReadAttribute("group");
        var kind = el.ReadAttribute("kind");
        var @class = el.ReadAttribute("class"); // Added class attribute
        var apientry = el.ElementsNamed("apientry").Select(a => a.ReadInnerText().FirstOrDefault()).FirstOrDefault();
        var ptype = el.ElementsNamed("ptype").Select(p => p.ReadInnerText().FirstOrDefault()).FirstOrDefault();
        var name = el.ElementsNamed("name").Select(n => n.ReadInnerText().FirstOrDefault()).FirstOrDefault() ?? "";
        var body = el.ReadInnerText();
        return new ProtoDef(group, kind, ptype, apientry, @class, name, body);
    }

    private static ParamDef ParseParamDef(El el)
    {
        var group = el.ReadAttribute("group");
        var kind = el.ReadAttribute("kind");
        var len = el.ReadAttribute("len");
        var @class = el.ReadAttribute("class");
        var apientry = el.ElementsNamed("apientry").Select(a => a.ReadInnerText().FirstOrDefault()).FirstOrDefault();
        var ptype = el.ElementsNamed("ptype").Select(p => p.ReadInnerText().FirstOrDefault()).FirstOrDefault();
        var name = el.ElementsNamed("name").Select(n => n.ReadInnerText().FirstOrDefault()).FirstOrDefault() ?? "";
        var body = el.ReadInnerText();
        return new ParamDef(group, kind, len, @class, ptype, apientry, name, body);
    }

    private static GlxDef ParseGlxDef(El el)
    {
        var type = el.ReadAttribute("type");
        var opcode = el.ReadAttribute("opcode");
        return new GlxDef(type, opcode);
    }

    private static FeatureDef ParseFeatureDef(El el)
    {
        var api = el.ReadAttribute("api") ?? "";
        var name = el.ReadAttribute("name") ?? "";
        var protect = el.ReadAttribute("protect");
        var number = el.ReadAttribute("number") ?? "";
        var comment = el.ReadAttribute("comment");
        var require = el.ElementsNamed("require").Select(ParseRequireRemoveDef).ToImmutableArray();
        var remove = el.ElementsNamed("remove").Select(ParseRequireRemoveDef).ToImmutableArray();
        return new FeatureDef(api, name, protect, number, comment, require, remove);
    }

    private static ExtensionDef ParseExtensionDef(El el)
    {
        var name = el.ReadAttribute("name") ?? "";
        var supported = el.ReadAttribute("supported") ?? "";
        var protect = el.ReadAttribute("protect");
        var comment = el.ReadAttribute("comment");
        var require = el.ElementsNamed("require").Select(ParseRequireRemoveDef).ToImmutableArray();
        var remove = el.ElementsNamed("remove").Select(ParseRequireRemoveDef).ToImmutableArray();
        return new ExtensionDef(name, supported, protect, comment, require, remove);
    }

    private static InterfaceDef ParseRequireRemoveDef(El el)
    {
        var profile = el.ReadAttribute("profile");
        var api = el.ReadAttribute("api");
        var comment = el.ReadAttribute("comment");
        var enums = el.ElementsNamed("enum").Select(e => new RequireEnum(e.ReadAttribute("name") ?? "", e.ReadAttribute("comment"))).ToImmutableArray();
        var commands = el.ElementsNamed("command").Select(e => new RequireCommand(e.ReadAttribute("name") ?? "", e.ReadAttribute("comment"))).ToImmutableArray();
        var types = el.ElementsNamed("type").Select(e => new RequireType(e.ReadAttribute("name") ?? "", e.ReadAttribute("comment"))).ToImmutableArray();
        return new InterfaceDef(profile, api, comment, enums, commands, types);
    }

    internal static Registry Parse(El root)
    {
        // Parse <comment> elements at the root (registry-level comments)
        var comments = root.ElementsNamed("comment")
            .SelectMany(e => e.ReadInnerText()).ToImmutableArray();

        var types = root.ElementsNamed("types")
            .SelectMany(t => t.ElementsNamed("type").Select(ParseTypeDef)).ToImmutableArray();

        var kinds = root.ElementsNamed("kinds")
            .SelectMany(k => k.ElementsNamed("kind").Select(ParseKindDef)).ToImmutableArray();

        var groups = root.ElementsNamed("groups")
            .SelectMany(g => g.ElementsNamed("group").Select(ParseGroupDef)).ToImmutableArray();

        var enums = root.ElementsNamed("enums")
            .Select(ParseEnumsType).ToImmutableArray();

        var commands = root.ElementsNamed("commands")
            .SelectMany(c =>
            {
                var ns = c.ReadAttribute("namespace");
                return c.ElementsNamed("command").Select(cmd => ParseCommandDef(cmd, ns));
            }).ToImmutableArray();

        var features = root.ElementsNamed("feature")
            .Select(ParseFeatureDef).ToImmutableArray();

        var extensions = root.ElementsNamed("extensions")
            .SelectMany(e => e.ElementsNamed("extension").Select(ParseExtensionDef)).ToImmutableArray();

        return new Registry(types, kinds, groups, enums, commands, features, extensions, comments);
    }
}
