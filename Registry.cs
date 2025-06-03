using System.Collections.Immutable;
using System.Xml.Linq;
using Spectre.Console;

namespace Glox.Registry;

// Data model for the Khronos OpenGL API Registry Schema

internal enum Level { ResolveGroupRefs,
    ResolveInterface
}

internal enum EnumKind
{
    Default,
    Bitmask
}

internal sealed class Registry(
    ImmutableArray<TypeDef> types,
    ImmutableArray<KindDef> kinds,
    ImmutableArray<GroupDef> groups,
    ImmutableArray<EnumBlock> enumBlocks,
    ImmutableArray<CommandDef> commands,
    ImmutableArray<FeatureDef> features,
    ImmutableArray<ExtensionDef> extensions,
    ImmutableArray<string> comments)
{
    internal ImmutableDictionary<string, TypeDef> Types { get; } = types.ToImmutableDictionary(x => x.Name, x=>x);
    internal ImmutableArray<KindDef> Kinds { get; } = kinds;
    internal Dictionary<string, GroupDef> GroupFromName { get; } = groups.ToDictionary(x => x.Name, x=>x);
    internal ImmutableArray<EnumBlock> EnumBlocks { get; } = enumBlocks;
    internal ImmutableDictionary<string, CommandDef> CommandFromName { get; } = commands.ToImmutableDictionary(x => x.Proto.Name, x => x);
    internal ImmutableArray<FeatureDef> Features { get; } = features;
    internal ImmutableArray<ExtensionDef> Extensions { get; } = extensions;
    internal ImmutableArray<string> Comments { get; } = comments;
    internal ImmutableDictionary<string, ImmutableArray<EnumValue>> EnumValuesFromName { get; } =
        enumBlocks
            .SelectMany(b => b.Enums)
            .GroupBy(x => x.Name)
            .ToImmutableDictionary(
                g => g.Key,
                g => g.ToImmutableArray()
            );

    internal void Resolve(Level level)
    {
        foreach (var t in Types.Values) t.Resolve(this, level);
        foreach (var k in Kinds) k.Resolve(this, level);
        foreach (var g in GroupFromName.Values) g.Resolve(this, level);
        foreach (var e in EnumBlocks) e.Resolve(this, level);
        foreach (var c in CommandFromName.Values) c.Resolve(this, level);
        foreach (var f in Features) f.Resolve(this, level);
        foreach (var e in Extensions) e.Resolve(this, level);
        // Comments are strings, nothing to resolve
    }

    internal GroupDef GetGroup(string name)
    {
        if (GroupFromName.TryGetValue(name, out var def)) return def;

        def = new GroupDef(name, []);
        GroupFromName.Add(name, def);
        return def;
    }

    internal ImmutableArray<EnumValue> FindEnumValue(string name) => CollectionExtensions.GetValueOrDefault(EnumValuesFromName, name);

    internal CommandDef? FindCommand(string name) => CollectionExtensions.GetValueOrDefault(CommandFromName, name);

    internal TypeDef? FindType(string name) => CollectionExtensions.GetValueOrDefault(Types, name);
}

internal sealed class TypeDef
{
    internal string Name { get; }
    internal string? Requires { get; }
    internal string? Comment { get; }
    internal string? ApiEntry { get; }
    internal string CodeBlock { get; }

    internal TypeDef(string name, string? requires, string? comment, string? apiEntry, string codeBlock)
    {
        Name = name;
        Requires = requires;
        Comment = comment;
        ApiEntry = apiEntry;
        CodeBlock = codeBlock;
    }

    internal void Resolve(Registry registry, Level level)
    {
        // Nothing to resolve
    }

    internal static TypeDef Null()
    {
        return new TypeDef("<null>", null, null, null, "<null>");
    }
}

internal sealed class KindDef(string name, string? desc)
{
    internal string Name { get; } = name;
    internal string? Desc { get; } = desc;

    internal void Resolve(Registry registry, Level level)
    {
        // Nothing to resolve
    }
}

internal sealed class GroupDef(string name, ImmutableArray<string> enumsRefs)
{
    internal string Name { get; } = name;
    internal ImmutableArray<string> EnumsRefs { get; } = enumsRefs;
    internal List<EnumValue> Enums { get; } = new();

    internal void Resolve(Registry registry, Level level)
    {
        // Nothing to resolve
    }
}

internal sealed class EnumBlock(
    int index, string? ns, EnumKind type, string? vendor, string? comment, string? start, string? end, string? group, ImmutableArray<EnumValue> enums, ImmutableArray<UnusedDef> unused)
{
    internal int Index { get; } = index;
    internal string? Namespace { get; } = ns;
    internal EnumKind Type { get; } = type;
    internal string? Vendor { get; } = vendor;
    internal string? Comment { get; } = comment;
    internal string? Start { get; } = start;
    internal string? End { get; } = end;
    internal string? Group { get; } = group;
    internal ImmutableArray<EnumValue> Enums { get; } = enums;
    internal ImmutableArray<UnusedDef> Unused { get; } = unused;

    internal void Resolve(Registry registry, Level level)
    {
        foreach (var e in Enums) e.Resolve(registry, level);
        foreach (var u in Unused) u.Resolve(registry, level);
    }
}

internal sealed class EnumValue(
    string name, string? value, string? api, string? type, ImmutableArray<string> groupRefs, string? alias, string? comment)
{
    internal string Name { get; } = name;
    internal string? Value { get; } = value;
    internal string? Api { get; } = api;
    internal string? Type { get; } = type;
    internal ImmutableArray<string> GroupRefs { get; } = groupRefs;
    internal ImmutableArray<GroupDef> Groups { get; private set; } = [];
    internal string? Alias { get; } = alias;
    internal string? Comment { get; } = comment;

    internal void Resolve(Registry registry, Level level)
    {
        if(level == Level.ResolveGroupRefs)
        {
            Groups = GroupRefs.Select(registry.GetGroup).ToImmutableArray();
            foreach (var g in Groups)
            {
                g.Enums.Add(this);
            }
        }
    }
}

internal sealed class UnusedDef(string? start, string? end, string? vendor, string? comment)
{
    internal string? Start { get; } = start;
    internal string? End { get; } = end;
    internal string? Vendor { get; } = vendor;
    internal string? Comment { get; } = comment;

    internal void Resolve(Registry registry, Level level)
    {
        // Nothing to resolve
    }
}

internal sealed class CommandDef(
    ProtoDef proto, ImmutableArray<ParamDef> @params, string? alias, string? vecequiv, GlxDef? glx, string? comment, string? ns)
{
    internal ProtoDef Proto { get; } = proto;
    internal ImmutableArray<ParamDef> Params { get; } = @params;
    internal string? Alias { get; } = alias;
    internal string? VecEquiv { get; } = vecequiv;
    internal GlxDef? Glx { get; } = glx;
    internal string? Comment { get; } = comment;
    internal string? Namespace { get; } = ns;

    internal void Resolve(Registry registry, Level level)
    {
        Proto.Resolve(registry, level);
        foreach (var p in Params) p.Resolve(registry, level);
        Glx?.Resolve(registry, level);
    }

    internal static CommandDef Null()
    {
        return new CommandDef(new ProtoDef(null, null, null, null, null, "<null>", []), [], null, null, null, null, null);
    }
}

internal sealed class ProtoDef(
    string? group, string? kind, string? ptype, string? apiEntry, string? @class, string name, ImmutableArray<string> body)
{
    internal string? Group { get; } = group;
    internal string? Kind { get; } = kind;
    internal string? Ptype { get; } = ptype;
    internal string? ApiEntry { get; } = apiEntry;
    internal string? Class { get; } = @class;
    internal string Name { get; } = name;
    internal ImmutableArray<string> Body { get; } = body;

    internal void Resolve(Registry registry, Level level)
    {
        // Nothing to resolve
    }
}

internal sealed class ParamDef(
    Location location, string? groupRef, string? kind, string? len, string? @class, string? ptypeRef, string? apiEntry, string name, ImmutableArray<string> body)
{
    private readonly Location _location = location;

    internal string? GroupRef { get; } = groupRef;
    internal GroupDef? Group { get; private set; } = null;
    internal string? Kind { get; } = kind;
    internal string? Len { get; } = len;
    internal string? Class { get; } = @class;
    internal string? PtypeRef { get; } = ptypeRef;
    internal TypeDef Type { get; private set; } = TypeDef.Null();
    internal string? ApiEntry { get; } = apiEntry;
    internal string Name { get; } = name;
    internal ImmutableArray<string> Body { get; } = body;

    internal void Resolve(Registry registry, Level level)
    {
        if(level == Level.ResolveGroupRefs)
        {
            if (GroupRef != null)
            {
                Group = registry.GetGroup(GroupRef);
            }

            if (PtypeRef != null)
            {
                var found = registry.FindType(PtypeRef);
                if (found != null)
                {
                    Type = found;
                }
                else
                {
                    _location.ReportError($"missing reference {PtypeRef}");
                }
            }
        }
    }
}

internal sealed class GlxDef(string? type, string? opcode)
{
    internal string? Type { get; } = type;
    internal string? Opcode { get; } = opcode;

    internal void Resolve(Registry registry, Level level)
    {
        // Nothing to resolve
    }
}

internal sealed class FeatureDef(
    string api, string name, string? protect, string number, string? comment, ImmutableArray<InterfaceDef> require, ImmutableArray<InterfaceDef> remove)
{
    internal string Api { get; } = api;
    internal string Name { get; } = name;
    internal string? Protect { get; } = protect;
    internal string Number { get; } = number;
    internal string? Comment { get; } = comment;
    internal ImmutableArray<InterfaceDef> Require { get; } = require;
    internal ImmutableArray<InterfaceDef> Remove { get; } = remove;

    internal void Resolve(Registry registry, Level level)
    {
        foreach (var r in Require) r.Resolve(registry, level);
        foreach (var r in Remove) r.Resolve(registry, level);
    }
}

internal sealed class ExtensionDef(
    Location location, string name, string supported, string? protect, string? comment, ImmutableArray<InterfaceDef> require, ImmutableArray<InterfaceDef> remove)
{
    private Location _location = location;
    internal string Name { get; } = name;
    internal string[] Supported { get; } = supported.Split("|", StringSplitOptions.TrimEntries);
    internal string? Protect { get; } = protect;
    internal string? Comment { get; } = comment;
    internal ImmutableArray<InterfaceDef> Require { get; } = require;
    internal ImmutableArray<InterfaceDef> Remove { get; } = remove;

    internal void Resolve(Registry registry, Level level)
    {
        if (Supported.Any(ContainsNonIdentifier))
        {
            _location.ReportError("Supported contains non id", string.Join(", ", Supported));
        }

        foreach (var r in Require) r.Resolve(registry, level);
        foreach (var r in Remove) r.Resolve(registry, level);
    }

    private static bool ContainsNonIdentifier(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return true;
        if (!(char.IsLetter(arg[0]) || arg[0] == '_'))
            return true;
        for (int i = 1; i < arg.Length; i++)
        {
            if (!(char.IsLetterOrDigit(arg[i]) || arg[i] == '_'))
                return true;
        }
        return false;
    }
}

internal sealed class InterfaceDef(
    string? profile, string? api, string? comment, ImmutableArray<InterfaceEnum> enums, ImmutableArray<InterfaceCommand> commands, ImmutableArray<InterfaceType> types)
{
    internal string? Profile { get; } = profile;
    internal string? Api { get; } = api;
    internal string? Comment { get; } = comment;
    internal ImmutableArray<InterfaceEnum> Enums { get; } = enums;
    internal ImmutableArray<InterfaceCommand> Commands { get; } = commands;
    internal ImmutableArray<InterfaceType> Types { get; } = types;

    internal void Resolve(Registry registry, Level level)
    {
        foreach (var e in Enums) e.Resolve(registry, level);
        foreach (var c in Commands) c.Resolve(registry, level);
        foreach (var t in Types) t.Resolve(registry, level);
    }
}

internal sealed class InterfaceEnum(Location location, string enumValueRef, string? comment)
{
    private Location _location = location;
    internal string EnumValueRef { get; } = enumValueRef;
    internal ImmutableArray<EnumValue> Value { get; private set; } = [];
    internal string? Comment { get; } = comment;

    internal void Resolve(Registry registry, Level level)
    {
        if (level == Level.ResolveInterface)
        {
            var found = registry.FindEnumValue(EnumValueRef);
            if (found.Length > 0)
            {
                Value = found;
            }
            else
            {
                _location.ReportError($"Missing enum value {EnumValueRef}");
            }
        }
    }
}

internal sealed class InterfaceCommand(Location location, string commandRef, string? comment)
{
    private Location _location = location;
    internal string CommandRef { get; } = commandRef;
    internal CommandDef Command { get; private set; } = CommandDef.Null();
    internal string? Comment { get; } = comment;

    internal void Resolve(Registry registry, Level level)
    {
        if (level == Level.ResolveInterface)
        {
            var found = registry.FindCommand(CommandRef);
            if(found != null)
            {
                Command = found;
            }
            else
            {
                _location.ReportError($"Missing command {CommandRef}");
            }
        }
    }
}

internal sealed class InterfaceType(Location location, string typeRef, string? comment)
{
    private Location _location = location;
    internal string TypeRef { get; } = typeRef;
    internal TypeDef Type { get; private set; } = TypeDef.Null();
    internal string? Comment { get; } = comment;

    internal void Resolve(Registry registry, Level level)
    {
        if (level == Level.ResolveInterface)
        {
            var found = registry.FindType(TypeRef);
            if (found != null)
            {
                Type = found;
            }
            else
            {
                _location.ReportError($"Missing type {TypeRef}");
            }
        }
    }
}

internal static class Parser
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
            el.Location.ReportError("Missing name for type");
            name = "missing";
        }
        return new TypeDef(
            name: name,
            requires: requires,
            comment: api,
            apiEntry: comment,
            codeBlock: body
        );
    }

    private static KindDef ParseKindDef(El el)
    {
        var name = el.ReadAttribute("name") ?? "";
        var desc = el.ReadAttribute("desc");
        return new KindDef(
            name: name,
            desc: desc
        );
    }

    private static GroupDef ParseGroupDef(El el)
    {
        var name = el.ReadAttribute("name") ?? "";
        var enums = el.ElementsNamed("enum")
            .Select(e => e.ReadAttribute("name") ?? "").ToImmutableArray();
        return new GroupDef(
            name: name,
            enumsRefs: enums
        );
    }

    private static EnumBlock ParseEnumsType(El el, int index)
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
        return new EnumBlock(
            index: index,
            ns: ns,
            type: type,
            vendor: vendor,
            comment: comment,
            start: start,
            end: end,
            group: group,
            enums: enums,
            unused: unused
        );

        static EnumKind InvalidTypeStr(El el, string typeStr)
        {
            el.Location.ReportError("Invalid type", $"Got type {typeStr}");
            return EnumKind.Default;
        }
    }

    private static EnumValue ParseEnumValue(El el)
    {
        var name = el.ReadAttribute("name") ?? "";
        var value = el.ReadAttribute("value");
        var api = el.ReadAttribute("api");
        var type = el.ReadAttribute("type");
        var groupStr = el.ReadAttribute("group");
        var group = groupStr?.Split(",", StringSplitOptions.TrimEntries).ToImmutableArray() ?? [];
        var alias = el.ReadAttribute("alias");
        var comment = el.ReadAttribute("comment"); // Added comment attribute
        return new EnumValue(
            name: name,
            value: value,
            api: api,
            type: type,
            groupRefs: group,
            alias: alias,
            comment: comment
        );
    }

    private static UnusedDef ParseUnusedDef(El el)
    {
        var start = el.ReadAttribute("start");
        var end = el.ReadAttribute("end");
        var vendor = el.ReadAttribute("vendor");
        var comment = el.ReadAttribute("comment");
        return new UnusedDef(
            start: start,
            end: end,
            vendor: vendor,
            comment: comment
        );
    }

    private static CommandDef ParseCommandDef(El el, string? commandsNamespace)
    {
        var proto = el.ElementsNamed("proto").Select(ParseProtoDef).First();
        var @params = el.ElementsNamed("param").Select(ParseParamDef).ToImmutableArray();
        var alias = el.ElementsNamed("alias").Select(a => a.ReadAttribute("name")).FirstOrDefault();
        var vecequiv = el.ElementsNamed("vecequiv").Select(a => a.ReadAttribute("name")).FirstOrDefault();
        var glx = el.ElementsNamed("glx").Select(ParseGlxDef).FirstOrDefault();
        var comment = el.ReadAttribute("comment");
        return new CommandDef(
            proto: proto,
            @params: @params,
            alias: alias,
            vecequiv: vecequiv,
            glx: glx,
            comment: comment,
            ns: commandsNamespace
        );
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
        return new ProtoDef(
            group: group,
            kind: kind,
            ptype: ptype,
            apiEntry: apientry,
            @class: @class,
            name: name,
            body: body
        );
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
        return new ParamDef(el.Location,
            groupRef: group,
            kind: kind,
            len: len,
            @class: @class,
            ptypeRef: ptype,
            apiEntry: apientry,
            name: name,
            body: body
        );
    }

    private static GlxDef ParseGlxDef(El el)
    {
        var type = el.ReadAttribute("type");
        var opcode = el.ReadAttribute("opcode");
        return new GlxDef(
            type: type,
            opcode: opcode
        );
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
        return new FeatureDef(
            api: api,
            name: name,
            protect: protect,
            number: number,
            comment: comment,
            require: require,
            remove: remove
        );
    }

    private static ExtensionDef ParseExtensionDef(El el)
    {
        var name = el.ReadAttribute("name") ?? "";
        var supported = el.ReadAttribute("supported") ?? "";
        var protect = el.ReadAttribute("protect");
        var comment = el.ReadAttribute("comment");
        var require = el.ElementsNamed("require").Select(ParseRequireRemoveDef).ToImmutableArray();
        var remove = el.ElementsNamed("remove").Select(ParseRequireRemoveDef).ToImmutableArray();
        return new ExtensionDef(el.Location,
            name: name,
            supported: supported,
            protect: protect,
            comment: comment,
            require: require,
            remove: remove
        );
    }

    private static InterfaceDef ParseRequireRemoveDef(El el)
    {
        var profile = el.ReadAttribute("profile");
        var api = el.ReadAttribute("api");
        var comment = el.ReadAttribute("comment");
        var enums = el.ElementsNamed("enum").Select(e => new InterfaceEnum(e.Location,
            enumValueRef: e.ReadAttribute("name") ?? "",
            comment: e.ReadAttribute("comment")
        )).ToImmutableArray();
        var commands = el.ElementsNamed("command").Select(e => new InterfaceCommand(e.Location,
            commandRef: e.ReadAttribute("name") ?? "",
            comment: e.ReadAttribute("comment")
        )).ToImmutableArray();
        var types = el.ElementsNamed("type").Select(e => new InterfaceType(e.Location,
            typeRef: e.ReadAttribute("name") ?? "",
            comment: e.ReadAttribute("comment")
        )).ToImmutableArray();
        return new InterfaceDef(
            profile: profile,
            api: api,
            comment: comment,
            enums: enums,
            commands: commands,
            types: types
        );
    }

    internal static Registry Parse(El root)
    {
        // Parse <comment> elements at the root (registry-level comments)
        AnsiConsole.WriteLine("Parsing XML...");
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

        var reg = new Registry(
            types: types,
            kinds: kinds,
            groups: groups,
            enumBlocks: enums,
            commands: commands,
            features: features,
            extensions: extensions,
            comments: comments
        );

        AnsiConsole.WriteLine("Resolving references...");
        reg.Resolve(Level.ResolveGroupRefs);
        reg.Resolve(Level.ResolveInterface);

        return reg;
    }
}
