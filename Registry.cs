using System.Collections.Immutable;

namespace glox_sharp;

// Data model for the Khronos OpenGL API Registry Schema

public sealed record Registry(
    ImmutableArray<TypeDef> Types,
    ImmutableArray<KindDef> Kinds,
    ImmutableArray<GroupDef> Groups,
    ImmutableArray<EnumsDef> Enums,
    ImmutableArray<CommandDef> Commands,
    ImmutableArray<FeatureDef> Features,
    ImmutableArray<ExtensionDef> Extensions,
    ImmutableArray<string> Comments
);

public sealed record TypeDef(
    string? Name,
    string? Requires,
    string? Api,
    string? Comment,
    string? InnerName,
    string? Apientry,
    ImmutableArray<string> Body
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

public sealed record EnumsDef(
    string? Namespace,
    string? Type,
    string? Vendor,
    string? Comment,
    string? Start,
    string? End,
    ImmutableArray<EnumDef> Enums,
    ImmutableArray<UnusedDef> Unused
);

public sealed record EnumDef(
    string Name,
    string? Value,
    string? Api,
    string? Type,
    string? Group,
    string? Alias
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
    string? Comment
);

public sealed record ProtoDef(
    string? Group,
    string? Kind,
    string? Ptype,
    string? Apientry,
    string Name,
    ImmutableArray<string> Body
);

public sealed record ParamDef(
    string? Group,
    string? Kind,
    string? Len,
    string? Class,
    string? Ptype,
    string? Apientry,
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
    ImmutableArray<RequireRemoveDef> Require,
    ImmutableArray<RequireRemoveDef> Remove
);

public sealed record ExtensionDef(
    string Name,
    string Supported,
    string? Protect,
    string? Comment,
    ImmutableArray<RequireRemoveDef> Require,
    ImmutableArray<RequireRemoveDef> Remove
);

public sealed record RequireRemoveDef(
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
            .Select(ParseEnumsDef).ToImmutableArray();

        var commands = root.ElementsNamed("commands")
            .SelectMany(c => c.ElementsNamed("command").Select(ParseCommandDef)).ToImmutableArray();

        var features = root.ElementsNamed("feature")
            .Select(ParseFeatureDef).ToImmutableArray();

        var extensions = root.ElementsNamed("extensions")
            .SelectMany(e => e.ElementsNamed("extension").Select(ParseExtensionDef)).ToImmutableArray();

        return new Registry(types, kinds, groups, enums, commands, features, extensions, comments);
    }

    private static TypeDef ParseTypeDef(El el)
    {
        string? innerName = null;
        string? apientry = null;
        foreach (var n in el.ElementsNamed("name"))
            innerName = n.ReadInnerText().FirstOrDefault();
        foreach (var a in el.ElementsNamed("apientry"))
            apientry = a.ReadInnerText().FirstOrDefault();
        var name = el.ReadAttribute("name");
        var requires = el.ReadAttribute("requires");
        var api = el.ReadAttribute("api");
        var comment = el.ReadAttribute("comment");
        var body = el.ReadInnerText();
        return new TypeDef(name, requires, api, comment, innerName, apientry, body);
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

    private static EnumsDef ParseEnumsDef(El el)
    {
        var ns = el.ReadAttribute("namespace");
        var type = el.ReadAttribute("type");
        var vendor = el.ReadAttribute("vendor");
        var comment = el.ReadAttribute("comment");
        var start = el.ReadAttribute("start");
        var end = el.ReadAttribute("end");
        var enums = el.ElementsNamed("enum").Select(ParseEnumDef).ToImmutableArray();
        var unused = el.ElementsNamed("unused").Select(ParseUnusedDef).ToImmutableArray();
        return new EnumsDef(ns, type, vendor, comment, start, end, enums, unused);
    }

    private static EnumDef ParseEnumDef(El el)
    {
        var name = el.ReadAttribute("name") ?? "";
        var value = el.ReadAttribute("value");
        var api = el.ReadAttribute("api");
        var type = el.ReadAttribute("type");
        var group = el.ReadAttribute("group");
        var alias = el.ReadAttribute("alias");
        return new EnumDef(name, value, api, type, group, alias);
    }

    private static UnusedDef ParseUnusedDef(El el)
    {
        var start = el.ReadAttribute("start");
        var end = el.ReadAttribute("end");
        var vendor = el.ReadAttribute("vendor");
        var comment = el.ReadAttribute("comment");
        return new UnusedDef(start, end, vendor, comment);
    }

    private static CommandDef ParseCommandDef(El el)
    {
        var proto = el.ElementsNamed("proto").Select(ParseProtoDef).First();
        var @params = el.ElementsNamed("param").Select(ParseParamDef).ToImmutableArray();
        var alias = el.ElementsNamed("alias").Select(a => a.ReadAttribute("name")).FirstOrDefault();
        var vecequiv = el.ElementsNamed("vecequiv").Select(a => a.ReadAttribute("name")).FirstOrDefault();
        var glx = el.ElementsNamed("glx").Select(ParseGlxDef).FirstOrDefault();
        var comment = el.ReadAttribute("comment");
        return new CommandDef(proto, @params, alias, vecequiv, glx, comment);
    }

    private static ProtoDef ParseProtoDef(El el)
    {
        var group = el.ReadAttribute("group");
        var kind = el.ReadAttribute("kind");
        var apientry = el.ElementsNamed("apientry").Select(a => a.ReadInnerText().FirstOrDefault()).FirstOrDefault();
        var ptype = el.ElementsNamed("ptype").Select(p => p.ReadInnerText().FirstOrDefault()).FirstOrDefault();
        var name = el.ElementsNamed("name").Select(n => n.ReadInnerText().FirstOrDefault()).FirstOrDefault() ?? "";
        var body = el.ReadInnerText();
        return new ProtoDef(group, kind, ptype, apientry, name, body);
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

    private static RequireRemoveDef ParseRequireRemoveDef(El el)
    {
        var profile = el.ReadAttribute("profile");
        var api = el.ReadAttribute("api");
        var comment = el.ReadAttribute("comment");
        var enums = el.ElementsNamed("enum").Select(e => new RequireEnum(e.ReadAttribute("name") ?? "", e.ReadAttribute("comment"))).ToImmutableArray();
        var commands = el.ElementsNamed("command").Select(e => new RequireCommand(e.ReadAttribute("name") ?? "", e.ReadAttribute("comment"))).ToImmutableArray();
        var types = el.ElementsNamed("type").Select(e => new RequireType(e.ReadAttribute("name") ?? "", e.ReadAttribute("comment"))).ToImmutableArray();
        return new RequireRemoveDef(profile, api, comment, enums, commands, types);
    }
}
