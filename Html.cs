using System.Runtime.InteropServices;
using System.Collections.Immutable;
using Glox.Registry;
using System.Xml.Linq;
using Spectre.Console;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace Glox.Html;

internal record Page(string FileName, string Title, string? Caption, string Body);

internal static class Writer
{
    internal static string Escape(string s) => System.Net.WebUtility.HtmlEncode(s);
    internal static string EscapeToCode(string s) => $"<pre><code>{Escape(s)}</code></pre>";
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

    private static string TypeLink(TypeDef t) => $"type_{t.Name}";
    private static string LinkToType(TypeDef t) => MakeLink(TypeLink(t), t.Name);
    private static Page TypePage(Registry.TypeDef t) =>
        new Page(
            FileName: TypeLink(t),
            Title: $"Type: {t.Name}",
            Caption: null,
            Body: new PropsBuilder(KeyStyle.Bold)
                .Add("CodeBlock", t.CodeBlock, EscapeToCode)
                .Add("Name", t.Name)
                .Add("Requires", t.Requires)
                .Add("Comment", t.Comment)
                .Add("ApiEntry", t.ApiEntry)
                .BuildUl()
        );

    private static Page KindPage(Registry.KindDef k) =>
        new Page(
            FileName: $"kind_{k.Name}",
            Title: $"Kind: {k.Name}",
            Caption: null,
            Body: new PropsBuilder(KeyStyle.Bold)
                .Add("Name", k.Name)
                .Add("Description", k.Desc)
                .BuildUl()
        );

    private static string GroupDefLink(GroupDef g) => $"group_{g.Name}";
    private static string LinkToGroupDef(GroupDef g) => MakeLink(GroupDefLink(g), g.Name);

    private static Page GroupPage(Registry.GroupDef g) =>
        new Page(
            FileName: GroupDefLink(g),
            Title: $"Group: {g.Name}",
            Caption: null,
            Body: new PropsBuilder(KeyStyle.Bold)
                .Add("Name", g.Name)
                .AddArray("Enums", g.Enums, x => SingleEnumValueLi(x).BuildUl())
                .BuildUl()
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
                    .Add("Group", e.Group, LinkToGroupDef)
                    .AddArray("Enums", e.Enums, x => SingleEnumValueLi(x).BuildUl())
                    .AddArray("Unused", e.Unused, x => UnusedToString(x).BuildLiCS())
                    .BuildUl()
        );

    private static PropsBuilder UnusedToString(UnusedDef u) =>
        new PropsBuilder()
            .Add("Start", u.Start)
            .Add("End", u.End)
            .Add("Vendor", u.Vendor)
            .Add("Comment", u.Comment);

    private static PropsBuilder SingleEnumValueLi(EnumValue ev) => new PropsBuilder()
        .AddEconded($"{Escape(ev.Name)} = {Escape(ev.Value ?? "")}")
        .Add("Api", ev.Api)
        .Add("Type", ev.Type)
        .AddArray("Group", ev.Groups, LinkToGroupDef)
        .Add("Alias", ev.Alias)
        .Add("Comment", ev.Comment);


    private static string CommandLink(CommandDef c) => $"command_{c.Proto.Name}";
    private static string LinkToCommand(CommandDef c) => MakeLink(CommandLink(c), c.Proto.Name);
    private static Page CommandPage(Registry.CommandDef c) =>
        new Page(
            FileName: CommandLink(c),
            Title: $"Command: {c.Proto.Name}",
            Caption: null,
            Body:
                new PropsBuilder(KeyStyle.Bold)
                    .Add("Name", c.Proto.Name)
                    .Add("Alias", c.Alias)
                    .Add("VecEquiv", c.VecEquiv)
                    .Add("Namespace", c.Namespace)
                    .Add("Comment", c.Comment)
                    .Add("Prop Group", c.Proto.Group)
                    .Add("Prop Kind", c.Proto.Kind)
                    .Add("Prop Ptype", c.Proto.Ptype)
                    .Add("Prop ApiEntry", c.Proto.ApiEntry)
                    .Add("Prop Class", c.Proto.ClassRef)
                    .Add("Prop Name", c.Proto.Name)
                    .AddArray("Prop Body", c.Proto.Body, EscapeToCode)
                    .AddArray("Params", c.Params, x => ParamDefToHtml(x).BuildCommaSeparated())
                    .Add("Glx", c.Glx, GlxToHtml)
                    .BuildUl()

        );


    private static string KlassLink(Klass c) => $"class_{c.Name}";
    private static string LinkToKlass(Klass c) => MakeLink(KlassLink(c), c.Name);
    private static Page KlassPage(Registry.Klass c) =>
        new Page(
            FileName: KlassLink(c),
            Title: $"Class: {c.Name}",
            Caption: null,
            Body:
            new PropsBuilder(KeyStyle.Bold)
                .Add("Name", c.Name)
                .AddArray("Used in", c.Params.Select(x => x.OwnerCommand), LinkToCommand)
                .BuildUl()

        );

    private static string GlxToHtml(GlxDef glx) => new PropsBuilder()
        .Add("Type", glx.Type)
        .Add("Opcode", glx.Opcode)
        .BuildCommaSeparated();

    private static PropsBuilder ParamDefToHtml(ParamDef p) =>
        new PropsBuilder()
            .Add("Name", p.Name)
            .Add("Group", p.Group, LinkToGroupDef)
            .Add("Kind", p.Kind)
            .Add("Len", p.Len)
            .Add("Class", p.Klass, LinkToKlass)
            .Add("Type", p.Type, LinkToType)
            .Add("ApiEntry", p.ApiEntry)
            .AddArray("Body", p.Body, EscapeToCode);

    private static Page FeaturePage(Registry.FeatureDef f) =>
        new Page(
            FileName: $"feature_{f.Name}",
            Title: $"Feature: {f.Name}",
            Caption: null,
            Body: new PropsBuilder(KeyStyle.Bold)
                .Add("API", f.Api)
                .Add("Name", f.Name)
                .Add("Protect", f.Protect)
                .Add("Number", f.Number)
                .Add("Comment", f.Comment)
                .AddArray("Require", f.Require, x => InterfaceToHtml(x).BuildCommaSeparated())
                .AddArray("Remove", f.Remove, x => InterfaceToHtml(x).BuildCommaSeparated())
                .BuildUl()
    );

    private static PropsBuilder InterfaceToHtml(InterfaceDef r) =>
        new PropsBuilder()
            .Add("Profile", r.Profile)
            .Add("Api", r.Api)
            .Add("Comment", r.Comment)
            .AddArray("Enums", r.Enums, ResolveEnum)
            .AddArray("Commands", r.Commands, ResolveCommands)
            .AddArray("Types", r.Types, ResolveTypes);

    private static string ResolveTypes(InterfaceType arg) => new PropsBuilder()
        .Add("Comment", arg.Comment)
        .AddEconded(LinkToType(arg.Type))
        .BuildCommaSeparated();

    private static string ResolveCommands(InterfaceCommand arg) => new PropsBuilder()
        .Add("Comment", arg.Comment)
        .AddEconded(LinkToCommand(arg.Command))
        .BuildCommaSeparated();

    private static string ResolveEnum(InterfaceEnum arg) => new PropsBuilder()
        .Add("Comment", arg.Comment)
        .AddSeveral(arg.Value, x => SingleEnumValueLi(x).BuildCommaSeparated())
        .BuildCommaSeparated();

    private static Page ExtensionPage(Registry.ExtensionDef ext) =>
        new Page(
            FileName: $"extension_{ext.Name}",
            Title: $"Extension: {ext.Name}",
            Caption: null,
            Body: new PropsBuilder(KeyStyle.Bold)
                    .Add("Name", ext.Name)
                    .AddArray("Supported", ext.Supported, Escape)
                    .Add("Protect", ext.Protect)
                    .Add("Comment", ext.Comment)
                    .AddArray("Require", ext.Require, x => InterfaceToHtml(x).BuildCommaSeparated())
                    .AddArray("Remove", ext.Remove, x => InterfaceToHtml(x).BuildCommaSeparated())
                    .BuildUl()
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

    private static string CreateHtmlFooter(IEnumerable<Page> pages)
    {
        var links = string.Join(
            " | ",
            pages.Select(p => $"<a href=\"{p.FileName}.html\">{Escape(p.Title)}</a>")
        );
        return $"<footer><nav>{links}</nav></footer>";
    }

    private static (ImmutableArray<Page>, string footer) GenerateAllPages(Registry.Registry registry)
    {
        var typePages = registry.Types.Values.Select(TypePage).ToImmutableArray();
        var kindPages = registry.Kinds.Select(KindPage).ToImmutableArray();
        var groupPages = registry.GroupFromName.Values.Select(GroupPage).ToImmutableArray();
        var enumBlocks = registry.EnumBlocks.Select(EnumBlocksPage).ToImmutableArray();
        var commandPages = registry.CommandFromName.Values.Select(CommandPage).ToImmutableArray();
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

        var footer = CreateHtmlFooter(listingPages);

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
        ], footer);
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

    internal PropsBuilder AddEconded(string value)
    {
        _allProps.Add(value);
        return this;
    }
    internal PropsBuilder Add(string? value)
    {
        if (value != null)
        {
            _allProps.Add($"{Writer.Escape(value)}");
        }
        return this;
    }

    internal string Key(string name)
        => keyStyle switch
        {
            KeyStyle.Normal => Writer.Escape(name),
            KeyStyle.Bold => $"<b>{Writer.Escape(name)}</b>",
            _ => throw new ArgumentOutOfRangeException(nameof(keyStyle), keyStyle, null)
        };

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

    internal string BuildCommaSeparated() => string.Join(", ", _allProps);

    internal string BuildLiCS()
        => $"<li>{BuildCommaSeparated()}</li>";

    internal PropsBuilder AddSeveral<T>(IEnumerable<T> list, Func<T, string> resolve)
    {
        _allProps.AddRange(list.Select(resolve));
        return this;
    }

    internal string BuildUl()
    {
        var all = _allProps.Select(x => $"<li>{x}</li>");
        var li = string.Join("", all);
        return $"<ul>{li}</ul>";
    }
}
