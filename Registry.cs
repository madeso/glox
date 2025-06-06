using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Dataflow;
using System.Xml;
using System.Xml.Linq;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Glox.Registry;

// Data model for the Khronos OpenGL API Registry Schema

internal enum Level
{
    ResolveGroupAndKlassRefs,
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
    internal ImmutableDictionary<string, TypeDef> Types { get; } = types.ToImmutableDictionary(x => x.Name, x => x);
    internal ImmutableArray<KindDef> Kinds { get; } = kinds;
    internal Dictionary<string, GroupDef> GroupFromName { get; } = groups.ToDictionary(x => x.Name, x => x);
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
    internal Dictionary<string, Klass> KlassFromName { get; } = new();

    internal void Resolve(Level level)
    {
        foreach (var t in Types.Values) t.Resolve(this, level);
        foreach (var k in Kinds) k.Resolve(this, level);
        foreach (var g in GroupFromName.Values) g.Resolve(this, level);
        foreach (var e in EnumBlocks) e.Resolve(this, level);
        foreach (var c in CommandFromName.Values) c.Resolve(this, level);
        foreach (var f in Features) f.Resolve(this, level);
        foreach (var e in Extensions) e.Resolve(this, level);
        foreach (var c in KlassFromName.Values) c.Resolve(this, level);
        // Comments are strings, nothing to resolve
    }

    internal GroupDef GetGroup(string name)
    {
        if (GroupFromName.TryGetValue(name, out var def)) return def;

        def = new GroupDef(name, []);
        GroupFromName.Add(name, def);
        return def;
    }

    internal Klass GetKlass(string name)
    {
        if (KlassFromName.TryGetValue(name, out var def)) return def;

        def = new Klass(name);
        KlassFromName.Add(name, def);
        return def;
    }

    internal ImmutableArray<EnumValue> FindEnumValue(string name) => CollectionExtensions.GetValueOrDefault(EnumValuesFromName, name);

    internal CommandDef? FindCommand(string name) => CollectionExtensions.GetValueOrDefault(CommandFromName, name);

    internal TypeDef? FindType(string name) => CollectionExtensions.GetValueOrDefault(Types, name);
}

internal sealed class Klass(string name)
{
    internal string Name { get; } = name;
    public List<ParamDef> Params { get; } = new();

    public void Resolve(Registry registry, Level level)
    {
        // todo(Gustav): implement types
    }
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
    int index, string? ns, EnumKind type, string? vendor, string? comment, string? start, string? end, string? groupRef, ImmutableArray<EnumValue> enums, ImmutableArray<UnusedDef> unused)
{
    internal int Index { get; } = index;
    internal string? Namespace { get; } = ns;
    internal EnumKind Type { get; } = type;
    internal string? Vendor { get; } = vendor;
    internal string? Comment { get; } = comment;
    internal string? Start { get; } = start;
    internal string? End { get; } = end;
    internal string? GroupRef { get; } = groupRef;
    internal GroupDef? Group { get; private set; } = null;
    internal ImmutableArray<EnumValue> Enums { get; } = enums;
    internal ImmutableArray<UnusedDef> Unused { get; } = unused;

    internal void Resolve(Registry registry, Level level)
    {
        foreach (var e in Enums) e.Resolve(registry, level);
        foreach (var u in Unused) u.Resolve(registry, level);

        if (level == Level.ResolveGroupAndKlassRefs)
        {
            if (GroupRef != null)
            {
                Group = registry.GetGroup(GroupRef);
            }
        }
    }
}

internal sealed class EnumValue(
    string name, string? value, NamedApi? api, string? type, ImmutableArray<string> groupRefs, string? alias, string? comment)
{
    internal string Name { get; } = name;
    internal string? Value { get; } = value;
    internal NamedApi? Api { get; } = api;
    internal string? Type { get; } = type;
    internal ImmutableArray<string> GroupRefs { get; } = groupRefs;
    internal ImmutableArray<GroupDef> Groups { get; private set; } = [];
    internal string? Alias { get; } = alias;
    internal string? Comment { get; } = comment;

    internal void Resolve(Registry registry, Level level)
    {
        if (level == Level.ResolveGroupAndKlassRefs)
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
    internal ImmutableArray<ParamDef> Params { get; set; } = @params;
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
        return new CommandDef(new ProtoDef(null, null, "<missing>", []), [], null, null, null, null, null);
    }
}

interface IProtoMember
{
    void Resolve(Registry registry, Level level);
    void Visit(IProtoMemberVisitor vis);
}

internal static class IProtoMemberUtils
{
    public static T Visit<T>(this IEnumerable<IProtoMember> member, T visitor) where T: IProtoMemberVisitor
    {
        foreach (var m in member)
        {
            m.Visit(visitor);
        }
        return visitor;
    }
}

interface IProtoMemberVisitor
{
    void VisitText(ProtoTextMember member);
    void VisitName(ProtoNameMember member);
    void VisitPType(ProtoPTypeMember member);
}

internal sealed class ProtoTextMember(string value) : IProtoMember
{
    public string Value { get; } = value;
    public static ProtoTextMember? Parse(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        return new ProtoTextMember(s);
    }
    public void Resolve(Registry registry, Level level)
    {
        // Nothing to resolve
    }
    public void Visit(IProtoMemberVisitor vis)
    {
        vis.VisitText(this);
    }
}

internal sealed class ProtoNameMember(string name) : IProtoMember
{
    public string Name { get; } = name;
    public void Resolve(Registry registry, Level level)
    {
        // Nothing to resolve
    }
    public void Visit(IProtoMemberVisitor vis)
    {
        vis.VisitName(this);
    }
}

internal sealed class ProtoPTypeMember(Location location, string @ref) : IProtoMember
{
    public TypeDef? Type { get; private set; } = null;
    public string? Kind { get; set; } = null;
    public string? KlassRef { get; set; } = null;
    public Klass? Klass { get; private set; } = null;

    public void Resolve(Registry registry, Level level)
    {
        if (level != Level.ResolveGroupAndKlassRefs) return;

        var found = registry.FindType(@ref);
        if (found == null)
        {
            location.ReportError($"Missing ptype {@ref}");
            return;
        }

        Type = found;

        if (KlassRef != null)
        {
            Klass = registry.GetKlass(KlassRef);
        }
    }

    public void Visit(IProtoMemberVisitor vis)
    {
        vis.VisitPType(this);
    }
}

internal sealed class ProtoDef(string? group, ProtoPTypeMember? ptype, string name, ImmutableArray<IProtoMember> body)
{
    internal string Name { get; } = name;
    internal string? Group { get; } = group;

    internal ProtoPTypeMember? Ptype { get; } = ptype;

    internal ImmutableArray<IProtoMember> Body { get; } = body;

    internal void Resolve(Registry registry, Level level)
    {
        foreach(var p in Body)
        {
            p.Resolve(registry, level);
        }
    }
}

internal sealed class ParamDef(Location location, CommandDef command, string? groupRef, string? kind, string? len, string? klassRef, string? typeRef, string? apiEntry, string name, ImmutableArray<string> body)
{
    private readonly Location _location = location;

    internal string? GroupRef { get; } = groupRef;
    internal GroupDef? Group { get; private set; } = null;
    internal string? Kind { get; } = kind;
    internal string? Len { get; } = len;
    internal string? KlassRef { get; } = klassRef;
    internal Klass? Klass { get; private set; } = null;
    internal string? TypeRef { get; } = typeRef;
    internal TypeDef Type { get; private set; } = TypeDef.Null();
    internal string? ApiEntry { get; } = apiEntry;
    internal string Name { get; } = name;
    internal ImmutableArray<string> Body { get; } = body;

    internal CommandDef OwnerCommand { get; } = command;

    internal void Resolve(Registry registry, Level level)
    {
        if (level != Level.ResolveGroupAndKlassRefs) return;

        if (GroupRef != null)
        {
            Group = registry.GetGroup(GroupRef);
        }

        if (TypeRef != null)
        {
            var found = registry.FindType(TypeRef);
            if (found != null)
            {
                Type = found;
            }
            else
            {
                _location.ReportError($"missing reference {TypeRef}");
            }
        }

        if (KlassRef != null)
        {
            Klass = registry.GetKlass(KlassRef);
            Klass.Params.Add(this);
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

internal enum NamedApi
{
    GL, GlEmbedded1, GlEmbedded2, GlSafety1, GlSafety2
}

internal enum Action
{
    Required, Removed
}

internal class ActionWith<T>(Action action, T what)
{
    public Action Action { get; } = action;
    public T What { get; } = what;
}

internal static class ActionExtensions
{
    internal static ActionWith<T> With<T>(this Action a, T what)
    {
        return new ActionWith<T>(a, what);
    }
}

internal sealed class FeatureDef(
    NamedApi? api, string name, string? protect, Version number, string? comment, ImmutableArray<InterfaceDef> require, ImmutableArray<InterfaceDef> remove)
{
    internal NamedApi? Api { get; } = api;
    internal string Name { get; } = name;
    internal string? Protect { get; } = protect;
    internal Version Number { get; } = number;
    internal string? Comment { get; } = comment;
    internal ImmutableArray<InterfaceDef> Require { get; } = require;
    internal ImmutableArray<InterfaceDef> Remove { get; } = remove;

    internal IEnumerable<ActionWith<CommandDef>> AllCommands =>
        Remove.SelectMany(r => r.Commands).Select(x => x.Command).Select(x => Action.Removed.With(x)).Concat(
            Require.SelectMany(r => r.Commands).Select(x => x.Command).Select(x => Action.Required.With(x)));

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

    internal IEnumerable<ActionWith<CommandDef>> AllCommands =>
        Remove.SelectMany(r => r.Commands).Select(x => x.Command).Select(x => Action.Removed.With(x)).Concat(
            Require.SelectMany(r => r.Commands).Select(x => x.Command).Select(x => Action.Required.With(x)));

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
    string? profile, NamedApi? api, string? comment, ImmutableArray<InterfaceEnum> enums, ImmutableArray<InterfaceCommand> commands, ImmutableArray<InterfaceType> types)
{
    internal string? Profile { get; } = profile;
    internal NamedApi? Api { get; } = api;
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
            if (found != null)
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
            groupRef: group,
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
        var api = ParseApi(el.Location, el.ReadAttribute("api"));
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

    private static NamedApi? ParseApi(Location location, string? attribute)
    {
        if (attribute == null) return null;
        return attribute switch
        {
            "gl" => NamedApi.GL,
            "gles1" => NamedApi.GlEmbedded1,
            "gles2" => NamedApi.GlEmbedded2,
            "glsc1" => NamedApi.GlSafety1,
            "glsc2" => NamedApi.GlSafety2,
            _ => ReportInvalidAttribute()
        };

        NamedApi? ReportInvalidAttribute()
        {
            location.ReportError("Invalid attribute detected", attribute);
            return null;
        }
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
        var alias = el.ElementsNamed("alias").Select(a => a.ReadAttribute("name")).FirstOrDefault();
        var vecequiv = el.ElementsNamed("vecequiv").Select(a => a.ReadAttribute("name")).FirstOrDefault();
        var glx = el.ElementsNamed("glx").Select(ParseGlxDef).FirstOrDefault();
        var comment = el.ReadAttribute("comment");
        var command = new CommandDef(
            proto: proto,
            @params: [],
            alias: alias,
            vecequiv: vecequiv,
            glx: glx,
            comment: comment,
            ns: commandsNamespace
        );

        var @params = el.ElementsNamed("param").Select(e => ParseParamDef(e, command)).ToImmutableArray();
        command.Params = @params;

        return command;
    }

    private class NameVisitor : IProtoMemberVisitor
    {
        public List<string> Names { get; } = [];

        public void VisitText(ProtoTextMember member)
        {
        }

        public void VisitName(ProtoNameMember member)
        {
            Names.Add(member.Name);
        }

        public void VisitPType(ProtoPTypeMember member)
        {
        }
    }

    private class PtypeVistor : IProtoMemberVisitor
    {
        public List<ProtoPTypeMember> Ptypes { get; } = [];

        public void VisitText(ProtoTextMember member)
        {
        }

        public void VisitName(ProtoNameMember member)
        {
        }

        public void VisitPType(ProtoPTypeMember member)
        {
            Ptypes.Add(member);
        }
    }

    private static ProtoDef ParseProtoDef(El el)
    {
        var group = el.ReadAttribute("group");
        var klassRef = el.ReadAttribute("class");
        var kind = el.ReadAttribute("kind");

        var children = el.ReadChildren().ToImmutableArray();
        var body = InsertSpace(ParseProtoChildren(el.Location, children)).ToImmutableArray();
        var name = body.Visit(new NameVisitor()).Names.FirstOrDefault();
        var ptypes = body.Visit(new PtypeVistor()).Ptypes;

        if (name == null)
        {
            el.Location.ReportError("Missing (or too many) names");
            name = "<missing>";
        }

        if (ptypes.Count > 1)
        {
            el.Location.ReportError("Too many ptypes");
        }

        var ptype = ptypes.FirstOrDefault();
        if (klassRef != null || kind != null)
        {
            if (ptype == null)
            {
                el.Location.ReportError("Has kind or class but no ptype");
            }
            else
            {
                ptype.Kind = kind;
                ptype.KlassRef = klassRef;
            }
        }

        return new ProtoDef(
            group: group,
            ptype: ptype,
            body: body,
            name: name
        );
    }

    private static IEnumerable<IProtoMember> InsertSpace(IEnumerable<IProtoMember> mems)
    {
        IProtoMember? last = null;
        foreach (var current in mems)
        {
            if (last != null)
            {
                if (IsBlock(last) && IsBlock(current))
                {
                    yield return new ProtoTextMember(" ");
                }
            }

            last = current;
            yield return current;
        }

        static bool IsBlock(IProtoMember m) => m is ProtoPTypeMember or ProtoNameMember;
    }

    private static IEnumerable<IProtoMember> ParseProtoChildren(Location root, IEnumerable<XmlNode> nodes)
    {
        int index = 0;
        foreach (var n in nodes)
        {
            switch (n)
            {
                case XmlComment:
                    continue;
                case XmlElement xmlElement:
                    {
                        var r = ParseProtoElement(xmlElement, root.Sub(xmlElement.Name, index));
                        if (r != null) yield return r;
                    }
                    break;
                case XmlSignificantWhitespace sw:
                    {
                        var r = ProtoTextMember.Parse(sw.Value);
                        if (r != null) yield return r;
                    }
                    break;
                case XmlText t:
                    {
                        var r = ProtoTextMember.Parse(t.Value);
                        if (r != null) yield return r;
                    }
                    break;
                case XmlWhitespace ws:
                    {
                        var r = ProtoTextMember.Parse(ws.Value);
                        if (r != null) yield return r;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(n));
            }

            index += 1;
        }
    }

    private static IProtoMember? ParseProtoElement(XmlElement elem, Location loc)
    {
        switch (elem.Name)
        {
            case "name":
                // <name> tag: function/type/param name
                return new ProtoNameMember(elem.InnerText);
            case "ptype":
                // <ptype> tag: type name
                return new ProtoPTypeMember(loc, elem.InnerText);
            default:
                return null;
        }
    }

    private static ParamDef ParseParamDef(El el, CommandDef command)
    {
        var group = el.ReadAttribute("group");
        var kind = el.ReadAttribute("kind");
        var len = el.ReadAttribute("len");
        var @class = el.ReadAttribute("class");
        var apientry = el.ElementsNamed("apientry").Select(a => a.ReadInnerText().FirstOrDefault()).FirstOrDefault();
        var ptype = el.ElementsNamed("ptype").Select(p => p.ReadInnerText().FirstOrDefault()).FirstOrDefault();
        var name = el.ElementsNamed("name").Select(n => n.ReadInnerText().FirstOrDefault()).FirstOrDefault() ?? "";
        var body = el.ReadInnerText();
        return new ParamDef(el.Location, command,
            groupRef: group,
            kind: kind,
            len: len,
            klassRef: @class,
            typeRef: ptype,
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
        var api = ParseApi(el.Location, el.ReadAttribute("api"));
        var name = el.ReadAttribute("name") ?? "";
        var protect = el.ReadAttribute("protect");
        var version = el.ReadAttribute("number");
        if (version == null)
        {
            el.Location.ReportError("Missing version information");
            version = "1337.42";
        }
        var number = Version.Parse(version);
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
        var api = ParseApi(el.Location, el.ReadAttribute("api"));
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
        reg.Resolve(Level.ResolveGroupAndKlassRefs);
        reg.Resolve(Level.ResolveInterface);

        return reg;
    }
}
