using System.Collections.Immutable;
using System.Xml;
using Glox.Registry;

namespace Glox.Registry;


// =============================================================================================================================================



interface ParamVisitor
{
    void VisitText(ParamTextMember text);
    void VisitName(ParamNameMember name);
    void VisitPtype(ParamPTypeMember ptype);
    void VisitApiEntry(ParamApiEntryMember entry);
}

interface ParamBody
{
    void Resolve(Registry registry, Level level);
    void Visit(ParamVisitor visitor);
}

internal sealed class ParamTextMember(string value) : ParamBody
{
    public string Value { get; } = value;
    public static ParamBody? Parse(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        return new ParamTextMember(s);
    }

    public void Resolve(Registry registry, Level level)
    {
    }

    public void Visit(ParamVisitor visitor)
    {
        visitor.VisitText(this);
    }
}

internal sealed class ParamNameMember(string name) : ParamBody
{
    public string Name { get; } = name;

    public void Resolve(Registry registry, Level level)
    {
    }

    public void Visit(ParamVisitor visitor)
    {
        visitor.VisitName(this);
    }
}

internal sealed class ParamPTypeMember(Location loc, string? typeRef) : ParamBody
{
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
    }

    public void Visit(ParamVisitor visitor)
    {
        visitor.VisitPtype(this);
    }

    public TypeDef? Type { get; set; } = null;
}

internal sealed class ParamApiEntryMember : ParamBody
{
    public void Resolve(Registry registry, Level level)
    {
    }

    public void Visit(ParamVisitor visitor)
    {
        visitor.VisitApiEntry(this);
    }
}

// =============================================================================================================================================


interface IProtoMember
{
    void Resolve(Registry registry, Level level);
    void Visit(IProtoMemberVisitor vis);
}

internal static class IProtoMemberUtils
{
    public static T Visit<T>(this IEnumerable<IProtoMember> member, T visitor) where T : IProtoMemberVisitor
    {
        foreach (var m in member)
        {
            m.Visit(visitor);
        }
        return visitor;
    }

    public static T Visit<T>(this IEnumerable<ParamBody> member, T visitor) where T : ParamVisitor
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

internal sealed class ProtoPTypeMember(CommandDef ownerCommand, Location location, string typeRef) : IProtoMember
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

    public void Visit(IProtoMemberVisitor vis)
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

internal static class Utils
{
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

    internal static ImmutableArray<ParamBody> ParseParamBody(El el)
    {
        var body = ParseXmlList(el.Location, el.ReadChildren(), ParamTextMember.Parse, ParseParamElement)
            .InsertSpace(() => new ParamTextMember(" "), m => m is ParamPTypeMember or ParamNameMember)
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

    private static ParamBody? ParseParamElement(XmlElement elem, Location loc)
    {
        switch (elem.Name)
        {
            case "name":
                return new ParamNameMember(elem.InnerText);
            case "ptype":
                return new ParamPTypeMember(loc, elem.InnerText);
            case "apientry":
                return new ParamApiEntryMember();
            default:
                return null;
        }
    }

    // --------------------

    internal static ImmutableArray<IProtoMember> ParseCommandBody(El protoEl, CommandDef command)
    {
        var body = ParseXmlList(protoEl.Location, protoEl.ReadChildren(), ProtoTextMember.Parse, (elem, loc) => ParseProtoElement(command, elem, loc))
            .InsertSpace(() => new ProtoTextMember(" "), m => m is ProtoPTypeMember or ProtoNameMember)
            .ToImmutableArray();
        return body;
    }

    private static IProtoMember? ParseProtoElement(CommandDef ownerCommand, XmlElement elem, Location loc)
    {
        switch (elem.Name)
        {
            case "name":
                // <name> tag: function/type/param name
                return new ProtoNameMember(elem.InnerText);
            case "ptype":
                // <ptype> tag: type name
                return new ProtoPTypeMember(ownerCommand, loc, elem.InnerText);
            default:
                return null;
        }
    }
}

// ====================================


internal static class CodeExtractor
{
    internal static List<ProtoPTypeMember> ExtractPtypes(ImmutableArray<IProtoMember> body) => body.Visit(new PtypeVistor()).Ptypes;

    private sealed class PtypeVistor : IProtoMemberVisitor
    {
        public List<ProtoPTypeMember> Ptypes { get; } = [];

        public void VisitText(ProtoTextMember member) {}
        public void VisitName(ProtoNameMember member) {}

        public void VisitPType(ProtoPTypeMember member) => Ptypes.Add(member);
    }

    // -----

    internal static string? ExtractNameFromParam(ImmutableArray<ParamBody> body) => body.Visit(new NameVisitor()).Names.FirstOrDefault();
    internal static string? ExtractNameFromCommandBlock(ImmutableArray<IProtoMember> body) => body.Visit(new NameVisitor()).Names.FirstOrDefault();
    internal static string? ExtractNameFromTypeBlock(ImmutableArray<ITypeCode> body) => body.Visit(new NameVisitor()).Names.FirstOrDefault();


    private sealed class NameVisitor : IProtoMemberVisitor, ParamVisitor, ITypeCodeVisitor
    {
        public List<string> Names { get; } = [];

        public void VisitName(ProtoNameMember member) => Names.Add(member.Name);
        public void VisitText(ProtoTextMember member) {}
        public void VisitPType(ProtoPTypeMember member) {}

        public void VisitName(ParamNameMember name) => Names.Add(name.Name);
        public void VisitText(ParamTextMember text) {}
        public void VisitPtype(ParamPTypeMember ptype) {}
        public void VisitApiEntry(ParamApiEntryMember entry) {}

        public void VisitName(TypeCodeName name) => Names.Add(name.Name);
        public void VisitText(TypeCodeText text) {}
        public void VisitApiEntry(TypeCodeApiEntry apiEntry) {}
    }
}