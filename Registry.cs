using System.Collections.Immutable;
using System.Formats.Tar;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using System.Xml;
using System.Xml.Linq;
using Glox.Html;
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
    internal ImmutableDictionary<string, TypeDef> TypeFromName { get; } = types.ToImmutableDictionary(x => x.Name, x => x);
    internal Dictionary<string, KindDef> KindFromName { get; } = kinds.ToDictionary(x => x.Name, x => x);
    internal Dictionary<string, GroupDef> GroupFromName { get; } = groups.ToDictionary(x => x.Name, x => x);
    internal ImmutableArray<EnumBlock> EnumBlocks { get; } = enumBlocks;
    internal ImmutableDictionary<string, CommandDef> CommandFromName { get; } = commands.ToImmutableDictionary(x => x.Name, x => x);
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
        foreach (var t in TypeFromName.Values) t.Resolve(this, level);
        foreach (var k in KindFromName.Values) k.Resolve(this, level);
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

    internal TypeDef? FindType(string name) => CollectionExtensions.GetValueOrDefault(TypeFromName, name);

    public KindDef GetKind(string name)
    {
        if (KindFromName.TryGetValue(name, out var def)) return def;

        def = new KindDef(name, null);
        KindFromName.Add(name, def);
        return def;
    }
}

internal sealed class Klass(string name)
{
    internal string Name { get; } = name;
    public List<CommandDef> Commands { get; } = new();

    public void Resolve(Registry registry, Level level)
    {
        // todo(Gustav): implement types
    }
}

internal sealed class TypeDef(Location? location, int index, string name, string? requiresRef, string? comment, string? apiEntry, IEnumerable<ITypeCode> codeBlock, bool gotAttributeFromName)
{
    internal string Name { get; } = name;
    internal TypeDef? Requires { get; private set; } = null;
    internal string? Comment { get; } = comment;
    internal string? ApiEntry { get; } = apiEntry;
    internal IEnumerable<ITypeCode> CodeBlock { get; } = codeBlock;
    internal bool GotAttributeFromName { get; } = gotAttributeFromName;
    internal int DeclarationIndex { get; } = index;

    internal void Resolve(Registry registry, Level level)
    {
        if (level != Level.ResolveGroupAndKlassRefs) return;
        if (requiresRef != null)
        {
            Requires = registry.FindType(requiresRef);
            if (Requires == null)
            {
                location?.ReportError($"Type requires is not a valid type {requiresRef}");
            }
        }
    }

    internal static TypeDef Null()
    {
        return new TypeDef(null, -1, "<null>", null, null, null, [], false);
    }
}

internal sealed class KindDef(string name, string? desc)
{
    internal string Name { get; } = name;
    internal string? Desc { get; } = desc;
    public List<CommandDef> Commands { get; } = new();

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

internal sealed class CommandDef(ImmutableArray<ParamDef> @params, string? alias, string? vecequiv, GlxDef? glx, string? comment, string? ns)
{
    internal string Name { get; set; } = "<missing>";

    internal ProtoPType? ReturnValue { get; set; } = null;

    internal ImmutableArray<IProto> Prototype { get; set;  } = [];

    internal ImmutableArray<ParamDef> Params { get; set; } = @params;
    internal string? Alias { get; } = alias;
    internal string? VecEquiv { get; } = vecequiv;
    internal GlxDef? Glx { get; } = glx;
    internal string? Comment { get; } = comment;
    internal string? Namespace { get; } = ns;

    internal void Resolve(Registry registry, Level level)
    {
        foreach (var p in Prototype)
        {
            p.Resolve(registry, level);
        }

        foreach (var p in Params) p.Resolve(registry, level);
        Glx?.Resolve(registry, level);
    }

    internal static CommandDef Null()
    {
        return new CommandDef([], null, null, null, null, null);
    }
}


internal sealed class ParamDef(string? len, string name, ParamPType? ptype, ImmutableArray<IParam> paramBody)
{
    internal string? Len { get; } = len;

    internal string Name { get; } = name;
    
    internal ImmutableArray<IParam> ParamBody { get; } = paramBody;

    internal ParamPType? Ptype { get; } = ptype;

    internal void Resolve(Registry registry, Level level)
    {
        foreach (var b in ParamBody)
        {
            b.Resolve(registry, level);
        }
    }

    public T Visit<T>(T visitor) where T: IParamVisitor
    {
        foreach (var b in ParamBody)
        {
            b.Visit(visitor);
        }
        return visitor;
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

internal enum Support
{
    gl,
    gles1,
    gles2,
    glcore,
    glsc2,
    disabled,
}

internal sealed class ExtensionDef(
    Location location, string name, ImmutableArray<Support> supported, string? protect, string? comment, ImmutableArray<InterfaceDef> require, ImmutableArray<InterfaceDef> remove)
{
    private Location _location = location;
    internal string Name { get; } = name;
    internal ImmutableArray<Support> Supported { get; } = supported;
    internal string? Protect { get; } = protect;
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

internal enum ProfileName
{
    core,
    compatibility,
    common,
}

internal sealed class InterfaceDef(
    ProfileName? profile, NamedApi? api, string? comment, ImmutableArray<InterfaceEnum> enums, ImmutableArray<InterfaceCommand> commands, ImmutableArray<InterfaceType> types)
{
    internal ProfileName? Profile { get; } = profile;
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
    private static TypeDef ParseTypeDef(El el, int index)
    {
        var requires = el.ReadAttribute("requires");
        var api = el.ReadAttribute("api");
        var comment = el.ReadAttribute("comment");
        
        var body = CodeParser.ParseTypeCode(el);

        var attributeName = el.ReadAttribute("name");
        var name = CodeExtractor.ExtractNameFromTypeBlock(body) ?? attributeName;

        if (name == null)
        {
            el.Location.ReportError("Missing name for type");
            name = "missing";
        }
        return new TypeDef(
            el.Location,
            index,
            name: name,
            requiresRef: requires,
            comment: api,
            apiEntry: comment,
            codeBlock: body,
            gotAttributeFromName: attributeName != null
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
        var alias = el.ElementsNamed("alias").Select(a => a.ReadAttribute("name")).FirstOrDefault();
        var vecequiv = el.ElementsNamed("vecequiv").Select(a => a.ReadAttribute("name")).FirstOrDefault();
        var glx = el.ElementsNamed("glx").Select(ParseGlxDef).FirstOrDefault();
        var comment = el.ReadAttribute("comment");


        // parse proto
        El? protoEl = null;
        foreach (var e in el.ElementsNamed("proto", dispose: false))
        {
            if (protoEl == null)
            {
                protoEl = e;
            }
            else
            {
                e.Location.ReportError("More than one proto detected");
                e.Dispose();
            }
        }
        var group = protoEl?.ReadAttribute("group");
        var klassRef = protoEl?.ReadAttribute("class");
        var kind = protoEl?.ReadAttribute("kind");

        var command = new CommandDef(
            @params: [],
            alias: alias,
            vecequiv: vecequiv,
            glx: glx,
            comment: comment,
            ns: commandsNamespace
        );

        var body = protoEl != null ? CodeParser.ParseCommandBody(protoEl, command) : [];
        var name = CodeExtractor.ExtractNameFromCommandBlock(body);
        var ptypes = CodeExtractor.ExtractPtypes(body);

        var reporter = protoEl ?? el;

        if (name == null)
        {
            reporter.Location.ReportError("Missing name");
            name = "<missing>";
        }

        if (ptypes.Count > 1)
        {
            reporter.Location.ReportError("Too many ptypes");
        }

        var ptype = ptypes.FirstOrDefault();
        if (klassRef != null || kind != null || group != null)
        {
            if (ptype == null)
            {
                reporter.Location.ReportError("Has kind or class but no ptype");
            }
            else
            {
                ptype.KindRef = kind;
                ptype.KlassRef = klassRef;
                ptype.GroupRef = group;
            }
        }

        var @params = el.ElementsNamed("param").Select(e => ParseParamDef(e, command)).ToImmutableArray();

        // complete command construction!
        command.Name = name;
        command.ReturnValue = ptype;
        command.Prototype = body;
        command.Params = @params;

        protoEl?.Dispose();
        return command;
    }

    private static ParamDef ParseParamDef(El el, CommandDef command)
    {
        var groupRef = el.ReadAttribute("group");
        var kindRef = el.ReadAttribute("kind");
        var len = el.ReadAttribute("len");
        var klassRef = el.ReadAttribute("class");

        var body = CodeParser.ParseParamBody(el, command);

        var name = CodeExtractor.ExtractNameFromParam(body);
        if (name == null)
        {
            el.Location.ReportError("Missing name");
            name = "<missing>";
        }

        var ptype = CodeExtractor.ExtractPtypeFromParam(body).FirstOrDefault();
        if (klassRef != null || groupRef != null || kindRef != null)
        {
            if (ptype == null)
            {
                AnsiConsole.WriteLine("Missing ptype in param, trying to alter void with GLVoid");
                body = AddVoidToBody(body, command, el.Location).ToImmutableArray();
                ptype = CodeExtractor.ExtractPtypeFromParam(body).FirstOrDefault();
            }

            if (ptype == null)
            {
                var tb = body.Visit(new TextRenderer()).Text;
                el.Location.ReportWarning("Has kind or class but no ptype", $"{tb} - {klassRef} {groupRef} {kindRef}");
            }
            else
            {
                ptype.KindRef = kindRef;
                ptype.KlassRef = klassRef;
                ptype.GroupRef = groupRef;
            }
        }


        return new ParamDef(len: len,
            name: name,
            ptype: ptype,
            paramBody: body
        );
    }

    private static IEnumerable<IParam> AddVoidToBody(IEnumerable<IParam> src, CommandDef command, Location location)
    {
        foreach (var p in src)
        {
            var text = p as ParamText;
            if (text == null)
            {
                yield return p;
                continue;
            }

            var found = text.Value.IndexOf("void", StringComparison.Ordinal);
            if (found == -1)
            {
                yield return text;
                continue;
            }

            var before = text.Value.Substring(0, found);
            var after = text.Value.Substring(found + "void".Length);

            if (!string.IsNullOrEmpty(before)) yield return new ParamText(before);
            yield return new ParamPType(command, location, "GLvoid");
            if (!string.IsNullOrEmpty(after)) yield return new ParamText(after);
        }
    }

    private sealed class TextRenderer : IParamVisitor
    {
        public string Text { get; private set; } = "";

        public void VisitText(ParamText text)
        {
            Text += $"(text {text.Value})";
        }

        public void VisitName(ParamName name)
        {
            Text += $"(name {name.Name})";
        }

        public void VisitPtype(ParamPType ptype)
        {
            Text += $"(ptype {ptype.Type?.Name})";
        }

        public void VisitApiEntry(ParamApiEntry entry)
        {
            Text += "(API_ENTRY)";
        }
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
        var supportedString = el.ReadAttribute("supported");
        var protect = el.ReadAttribute("protect");
        var comment = el.ReadAttribute("comment");
        var require = el.ElementsNamed("require").Select(ParseRequireRemoveDef).ToImmutableArray();
        var remove = el.ElementsNamed("remove").Select(ParseRequireRemoveDef).ToImmutableArray();

        var supported = ParseSupported(el.Location, supportedString?.Split("|", StringSplitOptions.TrimEntries) ?? []).ToImmutableArray();

        return new ExtensionDef(el.Location,
            name: name,
            supported: supported,
            protect: protect,
            comment: comment,
            require: require,
            remove: remove
        );
    }

    private static IEnumerable<Support> ParseSupported(Location loc, string[] split)
    {
        foreach (var s in split)
        {
            switch (s)
            {
                case "gl":
                    yield return Support.gl;
                    break;
                case "gles1":
                    yield return Support.gles1;
                    break;
                case "gles2":
                    yield return Support.gles2;
                    break;
                case "glcore":
                    yield return Support.glcore;
                    break;
                case "glsc2":
                    yield return Support.glsc2;
                    break;
                case "disabled":
                    yield return Support.disabled;
                    break;
                default:
                    loc.ReportError($"Invalid support <{s}>");
                    break;
            }
        }
        yield break;
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
            profile: profile != null ? ParseProfileName(el.Location, profile) : null,
            api: api,
            comment: comment,
            enums: enums,
            commands: commands,
            types: types
        );
    }

    private static ProfileName? ParseProfileName(Location loc, string name)
    {
        switch (name)
        {
            case "core":
                return ProfileName.core;
            case "compatibility":
                return ProfileName.compatibility;
            case "common":
                return ProfileName.common;
            default:
                loc.ReportError($"Unknown profile {name}");
                return null;
        }
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
