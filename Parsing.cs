using System.Collections.Immutable;
using System.Xml;
using Glox.Registry;
using Spectre.Console.Cli;

namespace Glox.Registry;


// =============================================================================================================================================



interface IParamVisitor
{
    void VisitText(ParamText text);
    void VisitName(ParamName name);
    void VisitPtype(ParamPType ptype);
    void VisitApiEntry(ParamApiEntry entry);
}

interface IParam
{
    void Resolve(Registry registry, Level level);
    void Visit(IParamVisitor visitor);
}

internal sealed class ParamText(string value) : IParam
{
    public string Value { get; } = value;
    public static IParam? Parse(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        return new ParamText(s);
    }

    public void Resolve(Registry registry, Level level)
    {
    }

    public void Visit(IParamVisitor visitor)
    {
        visitor.VisitText(this);
    }
}

internal sealed class ParamName(string name) : IParam
{
    public string Name { get; } = name;

    public void Resolve(Registry registry, Level level)
    {
    }

    public void Visit(IParamVisitor visitor)
    {
        visitor.VisitName(this);
    }
}

internal sealed class ParamPType(CommandDef ownerCommand, Location loc, string? typeRef) : IParam
{
    internal string? GroupRef { get; set; } = null;
    internal GroupDef? Group { get; private set; } = null;

    internal string? KindRef { get; set; } = null;
    public KindDef? Kind { get; private set; } = null;

    internal string? KlassRef { get; set; } = null;
    internal Klass? Klass { get; private set; } = null;

    public void Resolve(Registry registry, Level level)
    {
        if (level != Level.ResolveGroupAndKlassRefs) return;
        if (typeRef != null)
        {
            var found = registry.FindType(typeRef);
            if (found != null)
            {
                Type = found;
            }
            else
            {
                loc.ReportError($"missing reference {typeRef}");
            }
        }

        if (GroupRef != null)
        {
            Group = registry.GetGroup(GroupRef);
        }

        if (KlassRef != null)
        {
            Klass = registry.GetKlass(KlassRef);
            Klass.Commands.Add(ownerCommand);
        }

        if (KindRef != null)
        {
            Kind = registry.GetKind(KindRef);
            Kind.Commands.Add(ownerCommand);
        }
    }

    public void Visit(IParamVisitor visitor)
    {
        visitor.VisitPtype(this);
    }

    public TypeDef? Type { get; set; } = null;
}

internal sealed class ParamApiEntry : IParam
{
    public void Resolve(Registry registry, Level level)
    {
    }

    public void Visit(IParamVisitor visitor)
    {
        visitor.VisitApiEntry(this);
    }
}

// =============================================================================================================================================

interface IProtoVisitor
{
    void VisitText(ProtoText member);
    void VisitName(ProtoName member);
    void VisitPType(ProtoPType member);
}

interface IProto
{
    void Resolve(Registry registry, Level level);
    void Visit(IProtoVisitor vis);
}

internal sealed class ProtoText(string value) : IProto
{
    public string Value { get; } = value;
    public static ProtoText? Parse(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        return new ProtoText(s);
    }
    public void Resolve(Registry registry, Level level)
    {
        // Nothing to resolve
    }
    public void Visit(IProtoVisitor vis)
    {
        vis.VisitText(this);
    }
}

internal sealed class ProtoName(string name) : IProto
{
    public string Name { get; } = name;
    public void Resolve(Registry registry, Level level)
    {
        // Nothing to resolve
    }
    public void Visit(IProtoVisitor vis)
    {
        vis.VisitName(this);
    }
}

internal sealed class ProtoPType(CommandDef ownerCommand, Location location, string typeRef) : IProto
{
    public TypeDef? Type { get; private set; } = null;

    public string? KindRef { get; set; } = null;
    public KindDef? Kind { get; private set; } = null;

    public string? KlassRef { get; set; } = null;
    public Klass? Klass { get; private set; } = null;

    public string? GroupRef { get; set; } = null;
    public GroupDef? Group { get; set; }

    public void Resolve(Registry registry, Level level)
    {
        if (level != Level.ResolveGroupAndKlassRefs) return;

        var found = registry.FindType(typeRef);
        if (found == null)
        {
            location.ReportError($"Missing type {typeRef}");
            return;
        }

        Type = found;

        if (KlassRef != null)
        {
            Klass = registry.GetKlass(KlassRef);
            Klass.Commands.Add(ownerCommand);
        }

        if (GroupRef != null)
        {
            Group = registry.GetGroup(GroupRef);
        }

        if (KindRef != null)
        {
            Kind = registry.GetKind(KindRef);
            Kind.Commands.Add(ownerCommand);
        }
    }

    public void Visit(IProtoVisitor vis)
    {
        vis.VisitPType(this);
    }
}




// =============================================================================================================================================


interface ITypeCodeVisitor
{
    void VisitText(TypeCodeText text);
    void VisitApiEntry(TypeCodeApiEntry apiEntry);
    void VisitName(TypeCodeName name);
}

interface ITypeCode
{
    void Visit(ITypeCodeVisitor visitor);
}

internal sealed class TypeCodeText(string value) : ITypeCode
{
    public string Value { get; } = value;
    public static TypeCodeText? Parse(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        return new TypeCodeText(s);
    }

    public void Visit(ITypeCodeVisitor visitor)
    {
        visitor.VisitText(this);
    }
}

internal sealed class TypeCodeName(string name) : ITypeCode
{
    public void Visit(ITypeCodeVisitor visitor)
    {
        visitor.VisitName(this);
    }

    public string Name { get; } = name;
}

internal sealed class TypeCodeApiEntry : ITypeCode
{
    public void Visit(ITypeCodeVisitor visitor)
    {
        visitor.VisitApiEntry(this);
    }
}

// ===================================



internal static class ParseExtensions
{
    public static T Visit<T>(this IEnumerable<IProto> member, T visitor) where T : IProtoVisitor
    {
        foreach (var m in member)
        {
            m.Visit(visitor);
        }
        return visitor;
    }

    public static T Visit<T>(this IEnumerable<IParam> member, T visitor) where T : IParamVisitor
    {
        foreach (var m in member)
        {
            m.Visit(visitor);
        }
        return visitor;
    }

    public static T Visit<T>(this IEnumerable<ITypeCode> member, T visitor) where T : ITypeCodeVisitor
    {
        foreach (var m in member)
        {
            m.Visit(visitor);
        }
        return visitor;
    }

    public static IEnumerable<T> InsertSpace<T>(this IEnumerable<T> mems, Func<T> makeSpace, Func<T, bool> isBlock) where T : class
    {
        T? last = null;
        foreach (var current in mems)
        {
            if (last != null)
            {
                if (isBlock(last) && isBlock(current))
                {
                    yield return makeSpace();
                }
            }

            last = current;
            yield return current;
        }
    }
}

internal static class CodeParser
{
    public static ImmutableArray<ITypeCode> ParseTypeCode(El el)
    {
        var body = ParseXmlList(el.Location, el.ReadChildren(), TypeCodeText.Parse, ParseTypeCodeElement)
            // .InsertSpace(() => new ParamTextMember(" "), m => m is ParamPTypeMember or ParamNameMember)
            .ToImmutableArray();
        return body;
    }

    private static ITypeCode? ParseTypeCodeElement(XmlElement elem, Location loc)
    {
        switch (elem.Name)
        {
            case "name":
                return new TypeCodeName(elem.InnerText);
            case "apientry":
                return new TypeCodeApiEntry();
            default:
                return null;
        }
    }

    // ------------------

    internal static ImmutableArray<IParam> ParseParamBody(El el, CommandDef command)
    {
        var body = ParseXmlList(el.Location, el.ReadChildren(), ParamText.Parse, (x, loc) => ParseParamElement(x, loc, command))
            .InsertSpace(() => new ParamText(" "), m => m is ParamPType or ParamName)
            .ToImmutableArray();
        return body;
    }

    private static IEnumerable<T> ParseXmlList<T>(Location root, IEnumerable<XmlNode> nodes, Func<string?, T?> parseText, Func<XmlElement, Location, T?> parseElem) where T: class
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
                    var r = parseElem(xmlElement, root.Sub(xmlElement.Name, index));
                    if (r != null) yield return r;
                }
                    break;
                case XmlSignificantWhitespace sw:
                {
                    var r = parseText(sw.Value);
                    if (r != null) yield return r;
                }
                    break;
                case XmlText t:
                {
                    var r = parseText(t.Value);
                    if (r != null) yield return r;
                }
                    break;
                case XmlWhitespace ws:
                {
                    var r = parseText(ws.Value);
                    if (r != null) yield return r;
                }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(n));
            }

            index += 1;
        }
    }

    private static IParam? ParseParamElement(XmlElement elem, Location loc, CommandDef command)
    {
        switch (elem.Name)
        {
            case "name":
                return new ParamName(elem.InnerText);
            case "ptype":
                return new ParamPType(command, loc, elem.InnerText);
            case "apientry":
                return new ParamApiEntry();
            default:
                return null;
        }
    }

    // --------------------

    internal static ImmutableArray<IProto> ParseCommandBody(El protoEl, CommandDef command)
    {
        var body = ParseXmlList(protoEl.Location, protoEl.ReadChildren(), ProtoText.Parse, (elem, loc) => ParseProtoElement(command, elem, loc))
            .InsertSpace(() => new ProtoText(" "), m => m is ProtoPType or ProtoName)
            .ToImmutableArray();
        return body;
    }

    private static IProto? ParseProtoElement(CommandDef ownerCommand, XmlElement elem, Location loc)
    {
        switch (elem.Name)
        {
            case "name":
                // <name> tag: function/type/param name
                return new ProtoName(elem.InnerText);
            case "ptype":
                // <ptype> tag: type name
                return new ProtoPType(ownerCommand, loc, elem.InnerText);
            default:
                return null;
        }
    }
}

// ====================================


internal static class CodeExtractor
{
    internal static List<ProtoPType> ExtractPtypes(ImmutableArray<IProto> body) => body.Visit(new PtypeVistor()).Ptypes;

    private sealed class PtypeVistor : IProtoVisitor
    {
        public List<ProtoPType> Ptypes { get; } = [];

        public void VisitText(ProtoText member) {}
        public void VisitName(ProtoName member) {}

        public void VisitPType(ProtoPType member) => Ptypes.Add(member);
    }

    // -----
    
    public static List<ParamPType> ExtractPtypeFromParam(ImmutableArray<IParam> body) => body.Visit(new PtypeParamVisisor()).Ptypes;

    private sealed class PtypeParamVisisor : IParamVisitor
    {
        public List<ParamPType> Ptypes { get; } = [];

        public void VisitText(ParamText text) {}
        public void VisitName(ParamName name) {}
        public void VisitApiEntry(ParamApiEntry entry) {}

        public void VisitPtype(ParamPType ptype) => Ptypes.Add(ptype);
    }

    // -----

    internal static string? ExtractNameFromParam(ImmutableArray<IParam> body) => body.Visit(new NameVisitor()).Names.FirstOrDefault();
    internal static string? ExtractNameFromCommandBlock(ImmutableArray<IProto> body) => body.Visit(new NameVisitor()).Names.FirstOrDefault();
    internal static string? ExtractNameFromTypeBlock(ImmutableArray<ITypeCode> body) => body.Visit(new NameVisitor()).Names.FirstOrDefault();


    private sealed class NameVisitor : IProtoVisitor, IParamVisitor, ITypeCodeVisitor
    {
        public List<string> Names { get; } = [];

        public void VisitName(ProtoName member) => Names.Add(member.Name);
        public void VisitText(ProtoText member) {}
        public void VisitPType(ProtoPType member) {}

        public void VisitName(ParamName name) => Names.Add(name.Name);
        public void VisitText(ParamText text) {}
        public void VisitPtype(ParamPType ptype) {}
        public void VisitApiEntry(ParamApiEntry entry) {}

        public void VisitName(TypeCodeName name) => Names.Add(name.Name);
        public void VisitText(TypeCodeText text) {}
        public void VisitApiEntry(TypeCodeApiEntry apiEntry) {}
    }
}