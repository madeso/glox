using System.Runtime.InteropServices;
using System.Collections.Immutable;
using Glox.Registry;
using System.Xml.Linq;
using Spectre.Console;
using System.Collections.Generic;

namespace Glox.Html;

public record Page(string FileName, string Title, string Body);

public static class Writer
{
    internal static string Encode(string s) => System.Net.WebUtility.HtmlEncode(s);
    internal static string MakeLink(string link, string name) => $"<a href=\"{link}.html\">{Encode(name)}</a>";

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
            Body: $"""
                <pre>{Encode(t.CodeBlock)}</pre>
                <ul>
                    <li><b>Name:</b> {Encode(t.Name)}</li>
                    <li><b>Requires:</b> {Encode(t.Requires ?? "")}</li>
                    <li><b>Comment:</b> {Encode(t.Comment ?? "")}</li>
                    <li><b>ApiEntry:</b> {Encode(t.ApiEntry ?? "")}</li>
                </ul>
            """
        );

    private static Page KindPage(Registry.KindDef k) =>
        new Page(
            FileName: $"kind_{k.Name}",
            Title: $"Kind: {k.Name}",
            Body: $"""
                <ul>
                    <li><b>Name:</b> {Encode(k.Name)}</li>
                    <li><b>Description:</b> {Encode(k.Desc ?? "")}</li>
                </ul>
            """
        );

    private static string GroupDefLink(GroupDef g) => $"group_{g.Name}";
    private static string LinkToGroupDef(GroupDef g) => MakeLink(GroupDefLink(g), g.Name);
    private static Page GroupPage(Registry.GroupDef g) =>
        new Page(
            FileName: GroupDefLink(g),
            Title: $"Group: {g.Name}",
            Body: $"""
                <ul>
                    <li><b>Name:</b> {Encode(g.Name)}</li>
                    <li><b>Enums:</b>
                        {EnumValueList(g.Enums)}
                    </li>
                </ul>
            """
        );

    private static Page EnumBlocksPage(Registry.EnumBlock e) =>
        new Page(
            FileName: $"enums_{e.Namespace ?? "none"}_{e.Index}",
            Title: $"Enums: {e.Namespace ?? "none"}",
            Body: $"""
                <ul>
                    <li><b>Namespace:</b> {Encode(e.Namespace ?? "")}</li>
                    <li><b>Type:</b> {Encode(e.Type.ToString())}</li>
                    <li><b>Vendor:</b> {Encode(e.Vendor ?? "")}</li>
                    <li><b>Comment:</b> {Encode(e.Comment ?? "")}</li>
                    <li><b>Start:</b> {Encode(e.Start ?? "")}</li>
                    <li><b>End:</b> {Encode(e.End ?? "")}</li>
                    <li><b>Group:</b> {Encode(e.Group ?? "")}</li>
                    <li><b>Enums:</b>
                        {EnumValueList(e.Enums)}
                    </li>
                    <li><b>Unused:</b>
                        <ul>
                            {string.Join("", e.Unused.Select(u =>
                                $"<li>Start: {Encode(u.Start ?? "")}, End: {Encode(u.End ?? "")}, Vendor: {Encode(u.Vendor ?? "")}, Comment: {Encode(u.Comment ?? "")}</li>"
                            ))}
                        </ul>
                    </li>
                </ul>
            """
        );

    private static string EnumValueList(IEnumerable<EnumValue> eEnums) =>
        $"""
         <ul>
             {string.Join("", eEnums.Select(x => SingleEnumValueLi(x).BuildLiCS()))}
         </ul>
         """;

    private static PropsBuilder SingleEnumValueLi(EnumValue ev) => new PropsBuilder()
        .AddEconded($"{Encode(ev.Name)} = {Encode(ev.Value ?? "")}")
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
            Body: $"""
                <ul>
                    <li><b>Name:</b> {Encode(c.Proto.Name)}</li>
                    <li><b>Alias:</b> {Encode(c.Alias ?? "")}</li>
                    <li><b>VecEquiv:</b> {Encode(c.VecEquiv ?? "")}</li>
                    <li><b>Namespace:</b> {Encode(c.Namespace ?? "")}</li>
                    <li><b>Comment:</b> {Encode(c.Comment ?? "")}</li>
                    <li><b>Proto:</b>
                        <ul>
                            <li><b>Group:</b> {Encode(c.Proto.Group ?? "")}</li>
                            <li><b>Kind:</b> {Encode(c.Proto.Kind ?? "")}</li>
                            <li><b>Ptype:</b> {Encode(c.Proto.Ptype ?? "")}</li>
                            <li><b>ApiEntry:</b> {Encode(c.Proto.ApiEntry ?? "")}</li>
                            <li><b>Class:</b> {Encode(c.Proto.Class ?? "")}</li>
                            <li><b>Name:</b> {Encode(c.Proto.Name)}</li>
                            <li><b>Body:</b> <pre>{Encode(string.Join(" ", c.Proto.Body))}</pre></li>
                        </ul>
                    </li>
                    <li><b>Params:</b>
                        <ul>
                            {string.Join("", c.Params.Select(ParamDefToHtml))}
                        </ul>
                    </li>
                    <li><b>Glx:</b>
                        {(c.Glx != null ? $"Type: {Encode(c.Glx.Type ?? "")}, Opcode: {Encode(c.Glx.Opcode ?? "")}" : "")}
                    </li>
                </ul>
            """
        );

    private static string ParamDefToHtml(ParamDef p) =>
        new PropsBuilder()
            .Add("Name", p.Name)
            .Add("Group", p.Group, LinkToGroupDef)
            .Add("Kind", p.Kind)
            .Add("Len", p.Len)
            .Add("Class", p.Class)
            .Add("Ptype", p.Ptype)
            .Add("ApiEntry", p.ApiEntry)
            .Add("Body", string.Join(" ", p.Body))
            .BuildLiCS();

    private static Page FeaturePage(Registry.FeatureDef f) =>
        new Page(
            FileName: $"feature_{f.Name}",
            Title: $"Feature: {f.Name}",
            Body: $"""
                <ul>
                    <li><b>API:</b> {Encode(f.Api)}</li>
                    <li><b>Name:</b> {Encode(f.Name)}</li>
                    <li><b>Protect:</b> {Encode(f.Protect ?? "")}</li>
                    <li><b>Number:</b> {Encode(f.Number)}</li>
                    <li><b>Comment:</b> {Encode(f.Comment ?? "")}</li>
                    <li><b>Require:</b>
                        {InterfaceList(f.Require)}
                    </li>
                    <li><b>Remove:</b>
                        {InterfaceList(f.Remove)}
                    </li>
                </ul>
            """
        );

    private static string InterfaceList(IEnumerable<InterfaceDef> list)
    {
        var body = string.Join("", list.Select(InterfaceToHtml));
        return $"<ul>{body}</ul>";
    }

    private static string InterfaceToHtml(InterfaceDef r) =>
        new PropsBuilder()
            .Add("Profile", r.Profile)
            .Add("Api", r.Api)
            .Add("Comment", r.Comment)
            .AddArray("Enums", r.Enums, ResolveEnum)
            .AddArray("Commands", r.Commands, ResolveCommands)
            .AddArray("Types", r.Types, ResolveTypes)
            .BuildLiCS();

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
            Body: $"""
                <ul>
                    <li><b>Name:</b> {Encode(ext.Name)}</li>
                    <li><b>Supported:</b> {Encode(ext.Supported)}</li>
                    <li><b>Protect:</b> {Encode(ext.Protect ?? "")}</li>
                    <li><b>Comment:</b> {Encode(ext.Comment ?? "")}</li>
                    <li><b>Require:</b>
                        {InterfaceList(ext.Require)}
                    </li>
                    <li><b>Remove:</b>
                        {InterfaceList(ext.Remove)}
                    </li>
                </ul>
            """
        );

    private static Page CreateListingPage(string name, IEnumerable<Page> pages)
    {
        var links = string.Join(
            "",
            pages.Select(p => $"<li><a href=\"{p.FileName}.html\">{Encode(p.Title)}</a></li>")
        );
        return new Page(
            FileName: $"listing_{name}",
            Title: $"{name}",
            Body: $"<ul>{links}</ul>"
        );
    }

    private static string CreateHtmlFooter(IEnumerable<Page> pages)
    {
        var links = string.Join(
            " | ",
            pages.Select(p => $"<a href=\"{p.FileName}.html\">{Encode(p.Title)}</a>")
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

        var listingPages = new[]
        {
            CreateListingPage("Types", typePages),
            CreateListingPage("Kinds", kindPages),
            CreateListingPage("Groups", groupPages),
            CreateListingPage("Enums blocks", enumBlocks),
            CreateListingPage("Commands", commandPages),
            CreateListingPage("Features", featurePages),
            CreateListingPage("Extensions", extensionPages)
        };

        var footer = CreateHtmlFooter(listingPages);
        
        var indexPage = new Page(
            FileName: "index",
            Title: "Index",
            Body: "Welcome!"
        );

        return ( [
            ..typePages
                .Concat(kindPages)
                .Concat(groupPages)
                .Concat(enumBlocks)
                .Concat(commandPages)
                .Concat(featurePages)
                .Concat(extensionPages)
                .Concat(listingPages)
                .Append(indexPage)
        ], footer);
    }

    public static void Write(DirectoryInfo folder, Registry.Registry registry)
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

internal class PropsBuilder
{
    private readonly List<string> _allProps = new();

    public PropsBuilder AddEconded(string value)
    {
        _allProps.Add(value);
        return this;
    }
    public PropsBuilder Add(string? value)
    {
        if (value != null)
        {
            _allProps.Add($"{Writer.Encode(value)}");
        }
        return this;
    }

    public PropsBuilder Add(string name, string? value)
    {
        if(value != null) {
            _allProps.Add($"{Writer.Encode(name)}: {Writer.Encode(value)}");
        }
        return this;
    }

    public PropsBuilder Add<T>(string name, T? value, Func<T, string> converter) where T: class
    {
        if (value != null)
        {
            _allProps.Add($"{Writer.Encode(name)}: {converter(value)}");
        }
        return this;
    }

    public PropsBuilder AddArray<T>(string name, IEnumerable<T> list, Func<T, string> resolve)
    {
        var r = list.Select(resolve).ToImmutableArray();
        if(r.Length > 0)
        {
            var value = string.Join("", r.Select(x => $"<li>{x}</li>"));
            _allProps.Add($"{Writer.Encode(name)}: <ul>{value}</ul>");
        }
        return this;
    }

    public string BuildCommaSeparated() => string.Join(", ", _allProps);

    public string BuildLiCS()
        => $"<li>{BuildCommaSeparated()}</li>";

    public PropsBuilder AddSeveral<T>(IEnumerable<T> list, Func<T, string> resolve)
    {
        _allProps.AddRange(list.Select(resolve));
        return this;
    }
}
