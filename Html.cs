using System.Runtime.InteropServices;
using System.Collections.Immutable;
using Glox.Registry;
using System.Xml.Linq;
using Spectre.Console;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Action = Glox.Registry.Action;

namespace Glox.Html;

internal record Page(string FileName, string Title, string? Caption, string Body);

internal static class Writer
{
    internal static string Escape(string s) => System.Net.WebUtility.HtmlEncode(s);
    internal static string InCode(string s) => $"<pre><code>{s}</code></pre>";
    internal static string MakeBold(string s) => $"<b>{s}</b>";
    internal static string EscapeToCode(string s) => InCode(Escape(s));
    internal static string MakeLink(string link, string name) => $"<a href=\"{link}.html\">{Escape(name)}</a>";

    private static void WritePage(DirectoryInfo folder, Page page, string footer)
    {
        var path = Path.Combine(folder.FullName, page.FileName + ".html");
        var water = "<link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/water.css@2/out/water.css\">";
        File.WriteAllText(path, $"""
            <html>
            <head><title>{page.Title}</title>{water}</head>
            <body>
            <h1>{page.Title}</h1>
            {footer}
            {page.Body}
            {footer}
            </body>
            </html>
            """);
    }

    private static string FileForType(TypeDef t) => $"type_{t.Name}";
    private static string LinkToType(TypeDef t) => MakeLink(FileForType(t), t.Name);
    private static Page TypePage(TypeDef t) =>
        new Page(
            FileName: FileForType(t),
            Title: $"Type: {t.Name}",
            Caption: null,
            Body: new PropsBuilder(KeyStyle.Bold)
                .Add("CodeBlock", t.CodeBlock, EscapeToCode)
                .Add("Name", t.Name)
                .Add("Requires", t.Requires)
                .Add("Comment", t.Comment)
                .Add("ApiEntry", t.ApiEntry)
                .BuildList()
        );

    private static string FileForKind(KindDef k) => $"kind_{k.Name}";
    private static string LinkToKind(KindDef g) => MakeLink(FileForKind(g), g.Name);
    private static Page KindPage(Registry.KindDef k) =>
        new Page(
            FileName: FileForKind(k),
            Title: $"Kind: {k.Name}",
            Caption: null,
            Body: new PropsBuilder(KeyStyle.Bold)
                .Add("Name", k.Name)
                .Add("Description", k.Desc)
                .BuildList()
        );

    private static string FileForGroup(GroupDef g) => $"group_{g.Name}";
    private static string LinkToGroup(GroupDef g) => MakeLink(FileForGroup(g), g.Name);
    private static Page GroupPage(Registry.GroupDef g) =>
        new Page(
            FileName: FileForGroup(g),
            Title: $"Group: {g.Name}",
            Caption: null,
            Body: new PropsBuilder(KeyStyle.Bold)
                .Add("Name", g.Name)
                .AddArray("Enums", g.Enums, x => PropsFromEnumValue(x).BuildList())
                .BuildList()
        );

    private static Page EnumBlocksPage(Registry.EnumBlock e) =>
        new Page(
            FileName: $"enums_{e.Namespace ?? "none"}_{e.Index}",
            Title: $"Enums: {e.Namespace ?? "none"}",
            Caption: e.Comment,
            Body: new PropsBuilder(KeyStyle.Bold)
                    .Add("Namespace", e.Namespace ?? "")
                    .Add("Type", e.Type.ToString())
                    .Add("Vendor", e.Vendor)
                    .Add("Comment", e.Comment)
                    .Add("Start", e.Start)
                    .Add("End", e.End)
                    .Add("Group", e.Group, LinkToGroup)
                    .AddArray("Enums", e.Enums, x => PropsFromEnumValue(x).BuildList())
                    .AddArray("Unused", e.Unused, x => PropsFromUnused(x).BuildLiCS())
                    .BuildList()
        );

    private static PropsBuilder PropsFromUnused(UnusedDef u) =>
        new PropsBuilder()
            .Add("Start", u.Start)
            .Add("End", u.End)
            .Add("Vendor", u.Vendor)
            .Add("Comment", u.Comment);

    private static PropsBuilder PropsFromEnumValue(EnumValue ev) => new PropsBuilder()
        .AddEncoded($"{Escape(ev.Name)} = {Escape(ev.Value ?? "")}")
        .AddStruct("Api", ev.Api, HtmlFromApi)
        .Add("Type", ev.Type)
        .AddArray("Group", ev.Groups, LinkToGroup)
        .Add("Alias", ev.Alias)
        .Add("Comment", ev.Comment);

    private static string HtmlFromApi(NamedApi api) => api switch
    {
        NamedApi.GL => "OpenGL",
        NamedApi.GlEmbedded1 => "OpenGL ES 1",
        NamedApi.GlEmbedded2 => "OpenGL ES 2",
        NamedApi.GlSafety1 => "OpenGL Safety Critical 1",
        NamedApi.GlSafety2 => "OpenGL Safety Critical 2",
        _ => throw new ArgumentOutOfRangeException(nameof(api), api, null)
    };

    private static IEnumerable<ActionWith<TRoot>>FindRoots<TRoot, TChild>(IEnumerable<TRoot> roots, Func<TChild, bool> predicate, Func<TRoot, IEnumerable<ActionWith<TChild>>> allCommands)
    {
        var list = roots.SelectMany(root => allCommands(root).Select(x => new { Root = root, Action = x.Action, Command = x.What}));
        var partial = list.Where(x => predicate(x.Command));
        return partial.Select(x => x.Action.With(x.Root));
    }

    private static string FileForCommand(CommandDef c) => $"command_{c.Name}";
    private static string LinkToCommand(CommandDef c) => MakeLink(FileForCommand(c), c.Name);
    private static Page CommandPage(Registry.CommandDef c, Registry.Registry reg) =>
        new Page(
            FileName: FileForCommand(c),
            Title: $"Command: {c.Name}",
            Caption: null,
            Body:
                new PropsBuilder(KeyStyle.Bold)
                    .Add("Name", c.Name)
                    .Add("Alias", c.Alias)
                    .Add("VecEquiv", c.VecEquiv)
                    .Add("Namespace", c.Namespace)
                    .Add("Comment", c.Comment)
                    .Add("Return Group", c.ReturnValue?.Group, LinkToGroup)
                    .Add("Return Kind", c.ReturnValue?.Kind, LinkToKind)
                    .Add("Return Class", c.ReturnValue?.Klass, LinkToKlass)
                    .AddStruct("Prototype", c.Prototype, b => InCode(b.Visit(new HtmlCodeGenerator()).Code))
                    .AddArray("Params", c.Params, x => PropsForParam(x).BuildCommaSeparated())
                    .Add("Glx", c.Glx, x => PropsForGlx(x).BuildCommaSeparated())
                    .AddArray("Mentioned in features", FindRoots(reg.Features, otherCommand => otherCommand.Name == c.Name, f => f.AllCommands)
                        , f => $"{LinkToFeature(f.What)} ({f.Action})")
                    .AddArray("Mentioned in extension", FindRoots(reg.Extensions, otherCommand => otherCommand.Name == c.Name, f => f.AllCommands)
                        , f => $"{LinkToExtension(f.What)} ({f.Action})")
                    .BuildList()
        );

    private sealed class HtmlCodeGenerator : IProtoMemberVisitor
    {
        public string Code { get; private set; } = "";

        public void VisitText(ProtoTextMember member)
        {
            Code += Escape(member.Value);
        }

        public void VisitName(ProtoNameMember name)
        {
            Code += MakeBold(Escape(name.Name));
        }

        public void VisitPType(ProtoPTypeMember member)
        {
            if(member.Type != null)
            {
                Code += LinkToType(member.Type);
            }
            else
            {
                Code += Escape("<invalid ptype>");
            }
        }
    }


    private static string FileForKlass(Klass c) => $"class_{c.Name}";
    private static string LinkToKlass(Klass c) => MakeLink(FileForKlass(c), c.Name);
    private static Page KlassPage(Registry.Klass c) =>
        new Page(
            FileName: FileForKlass(c),
            Title: $"Class: {c.Name}",
            Caption: null,
            Body:
            new PropsBuilder(KeyStyle.Bold)
                .Add("Name", c.Name)
                .AddArray("Used in", c.Commands, LinkToCommand)
                .BuildList()

        );

    private static PropsBuilder PropsForGlx(GlxDef glx) => new PropsBuilder()
        .Add("Type", glx.Type)
        .Add("Opcode", glx.Opcode);

    private static PropsBuilder PropsForParam(ParamDef p) =>
        new PropsBuilder()
            .Add("Name", p.Name)
            .Add("Group", p.Group, LinkToGroup)
            .Add("Kind", p.Kind)
            .Add("Len", p.Len)
            .Add("Class", p.Klass, LinkToKlass)
            .Add("Type", p.Type, LinkToType)
            .Add("ApiEntry", p.ApiEntry)
            .AddArray("Body", p.Body, EscapeToCode);

    private static string FileForFeature(FeatureDef f) => $"feature_{f.Name}";
    private static string LinkToFeature(FeatureDef f) => MakeLink(FileForFeature(f), f.Name);
    private static Page FeaturePage(FeatureDef f) =>
        new Page(
            FileName: FileForFeature(f),
            Title: $"Feature: {f.Name}",
            Caption: null,
            Body: new PropsBuilder(KeyStyle.Bold)
                .AddStruct("API", f.Api, HtmlFromApi)
                .Add("Name", f.Name)
                .Add("Protect", f.Protect)
                .Add("Number", f.Number, v => v.ToString())
                .Add("Comment", f.Comment)
                .AddArray("Require", f.Require, x => PropsForInterface(x).BuildCommaSeparated())
                .AddArray("Remove", f.Remove, x => PropsForInterface(x).BuildCommaSeparated())
                .BuildList()
    );

    private static PropsBuilder PropsForInterface(InterfaceDef r) =>
        new PropsBuilder()
            .Add("Profile", r.Profile)
            .AddStruct("Api", r.Api, HtmlFromApi)
            .Add("Comment", r.Comment)
            .AddArray("Enums", r.Enums, x => PropsForInterfaceEnum(x).BuildCommaSeparated() )
            .AddArray("Commands", r.Commands, x => PropsForInterfaceCommand(x).BuildCommaSeparated() )
            .AddArray("Types", r.Types, x => PropsForInterfaceType(x).BuildCommaSeparated());

    private static PropsBuilder PropsForInterfaceType(InterfaceType arg) => new PropsBuilder()
        .Add("Comment", arg.Comment)
        .AddEncoded(LinkToType(arg.Type));

    private static PropsBuilder PropsForInterfaceCommand(InterfaceCommand arg) => new PropsBuilder()
        .Add("Comment", arg.Comment)
        .AddEncoded(LinkToCommand(arg.Command));

    private static PropsBuilder PropsForInterfaceEnum(InterfaceEnum arg) => new PropsBuilder()
        .Add("Comment", arg.Comment)
        .AddSeveral(arg.Value, x => PropsFromEnumValue(x).BuildCommaSeparated());

    private static string FileForExtension(ExtensionDef ext) => $"extension_{ext.Name}";
    private static string LinkToExtension(ExtensionDef f) => MakeLink(FileForExtension(f), f.Name);
    private static Page ExtensionPage(Registry.ExtensionDef ext) =>
        new Page(
            FileName: FileForExtension(ext),
            Title: $"Extension: {ext.Name}",
            Caption: null,
            Body: new PropsBuilder(KeyStyle.Bold)
                    .Add("Name", ext.Name)
                    .AddArray("Supported", ext.Supported, Escape)
                    .Add("Protect", ext.Protect)
                    .Add("Comment", ext.Comment)
                    .AddArray("Require", ext.Require, x => PropsForInterface(x).BuildCommaSeparated())
                    .AddArray("Remove", ext.Remove, x => PropsForInterface(x).BuildCommaSeparated())
                    .BuildList()
        );

    private static Page CreateListingPage(string name, IEnumerable<Page> pages)
    {
        var links = string.Join(
            "",
            pages.Select(p => $"<li><a href=\"{p.FileName}.html\">{Escape(p.Title)}</a>{Escape(p.Caption == null ? "" : $" - {p.Caption}")} </li>")
        );
        return new Page(
            FileName: $"listing_{name}",
            Title: $"{name}",
            Caption: null,
            Body: $"<ul>{links}</ul>"
        );
    }

    private static string HtmlForFooter(IEnumerable<Page> pages)
    {
        var links = string.Join(
            " | ",
            pages.Select(p => $"<a href=\"{p.FileName}.html\">{Escape(p.Title)}</a>")
        );
        return $"<footer><nav>{links}</nav></footer>";
    }

    private static (ImmutableArray<Page>, string footer) GenerateAllPages(Registry.Registry registry)
    {
        var typePages = registry.TypeFromName.Values.Select(TypePage).ToImmutableArray();
        var kindPages = registry.KindFromName.Values.Select(KindPage).ToImmutableArray();
        var groupPages = registry.GroupFromName.Values.Select(GroupPage).ToImmutableArray();
        var enumBlocks = registry.EnumBlocks.Select(EnumBlocksPage).ToImmutableArray();
        var commandPages = registry.CommandFromName.Values.Select(c => CommandPage(c, registry)).ToImmutableArray();
        var featurePages = registry.Features.Select(FeaturePage).ToImmutableArray();
        var extensionPages = registry.Extensions.Select(ExtensionPage).ToImmutableArray();
        var klassPages = registry.KlassFromName.Values.Select(KlassPage).ToImmutableArray();

        var listingPages = new[]
        {
            CreateListingPage("Types", typePages),
            CreateListingPage("Kinds", kindPages),
            CreateListingPage("Groups", groupPages),
            CreateListingPage("Enums blocks", enumBlocks),
            CreateListingPage("Commands", commandPages),
            CreateListingPage("Features", featurePages),
            CreateListingPage("Extensions", extensionPages),
            CreateListingPage("Classes", klassPages)
        };

        var htmlFooter = HtmlForFooter(listingPages);

        var indexPage = new Page(
            FileName: "index",
            Title: "Index",
            Caption: null,
            Body: "Welcome!"
        );

        return ([
            ..typePages
                .Concat(kindPages)
                .Concat(groupPages)
                .Concat(enumBlocks)
                .Concat(commandPages)
                .Concat(featurePages)
                .Concat(extensionPages)
                .Concat(listingPages)
                .Concat(klassPages)
                .Append(indexPage)
        ], htmlFooter);
    }

    internal static void Write(DirectoryInfo folder, Registry.Registry registry)
    {
        AnsiConsole.WriteLine("Generating pages...");
        var (pages, footer) = GenerateAllPages(registry);

        AnsiConsole.WriteLine("Writing html...");
        foreach (var p in pages)
        {
            WritePage(folder, p, footer);
        }
    }
}

enum KeyStyle
{
    Normal, Bold
}

internal class PropsBuilder(KeyStyle keyStyle = KeyStyle.Normal)
{
    private readonly List<string> _allProps = new();

    internal PropsBuilder AddEncoded(string value)
    {
        _allProps.Add(value);
        return this;
    }

    internal string Key(string name)
        => keyStyle switch
        {
            KeyStyle.Normal => Writer.Escape(name),
            KeyStyle.Bold => $"<b>{Writer.Escape(name)}</b>",
            _ => throw new ArgumentOutOfRangeException(nameof(keyStyle), keyStyle, null)
        };

    internal PropsBuilder Add(string? value)
    {
        if (value != null)
        {
            _allProps.Add($"{Writer.Escape(value)}");
        }
        return this;
    }

    internal PropsBuilder Add(string name, string? value)
    {
        if (value != null)
        {
            _allProps.Add($"{Key(name)}: {Writer.Escape(value)}");
        }
        return this;
    }

    internal PropsBuilder Add<T>(string name, T? value, Func<T, string> converter) where T : class
    {
        if (value != null)
        {
            _allProps.Add($"{Key(name)}: {converter(value)}");
        }
        return this;
    }

    internal PropsBuilder AddStruct<T>(string name, T value, Func<T, string> converter) where T : struct
    {
        _allProps.Add($"{Key(name)}: {converter(value)}");
        return this;
    }

    internal PropsBuilder AddStruct<T>(string name, T? value, Func<T, string> converter) where T : struct
    {
        if (value.HasValue)
        {
            _allProps.Add($"{Key(name)}: {converter(value.Value)}");
        }
        return this;
    }

    internal PropsBuilder AddArray<T>(string name, IEnumerable<T> list, Func<T, string> resolve)
    {
        var r = list.Select(resolve).ToImmutableArray();
        if (r.Length > 0)
        {
            var value = string.Join("", r.Select(x => $"<li>{x}</li>"));
            _allProps.Add($"{Key(name)}: <ul>{value}</ul>");
        }
        return this;
    }

    internal PropsBuilder AddSeveral<T>(IEnumerable<T> list, Func<T, string> resolve)
    {
        _allProps.AddRange(list.Select(resolve));
        return this;
    }

    internal string BuildCommaSeparated() => string.Join(", ", _allProps);

    internal string BuildLiCS()
        => $"<li>{BuildCommaSeparated()}</li>";

    internal string BuildList()
    {
        var all = _allProps.Select(x => $"<li>{x}</li>");
        var li = string.Join("", all);
        return $"<ul>{li}</ul>";
    }
}
