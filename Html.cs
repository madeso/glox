using System.Runtime.InteropServices;
using System.Collections.Immutable;

namespace Glox.Html;

public record Page(string FileName, string Title, string Body);

public static class Writer
{
    private static string Encode(string s) => System.Net.WebUtility.HtmlEncode(s);

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

    private static Page TypePage(Registry.TypeDef t) =>
        new Page(
            FileName: $"type_{t.Name}",
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

    private static Page GroupPage(Registry.GroupDef g) =>
        new Page(
            FileName: $"group_{g.Name}",
            Title: $"Group: {g.Name}",
            Body: $"""
                <ul>
                    <li><b>Name:</b> {Encode(g.Name)}</li>
                    <li><b>Enums:</b>
                        <ul>
                            {string.Join("", g.Enums.Select(e => $"<li>{Encode(e.Name)}</li>"))}
                        </ul>
                    </li>
                </ul>
            """
        );

    private static Page EnumsPage(Registry.EnumType e) =>
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
                        <ul>
                            {string.Join("", e.Enums.Select(ev =>
                                $"<li>{Encode(ev.Name)} = {Encode(ev.Value ?? "")} " +
                                $"<small>Api: {Encode(ev.Api ?? "")}, Type: {Encode(ev.Type ?? "")}, " +
                                $"Group: {Encode(ev.Group ?? "")}, Alias: {Encode(ev.Alias ?? "")}, " +
                                $"Comment: {Encode(ev.Comment ?? "")}</small></li>"
                            ))}
                        </ul>
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

    private static Page CommandPage(Registry.CommandDef c) =>
        new Page(
            FileName: $"command_{c.Proto.Name}",
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
                            {string.Join("", c.Params.Select(p =>
                                $"<li>Name: {Encode(p.Name)}, Group: {Encode(p.Group ?? "")}, Kind: {Encode(p.Kind ?? "")}, Len: {Encode(p.Len ?? "")}, Class: {Encode(p.Class ?? "")}, Ptype: {Encode(p.Ptype ?? "")}, ApiEntry: {Encode(p.ApiEntry ?? "")}, Body: {Encode(string.Join(" ", p.Body))}</li>"
                            ))}
                        </ul>
                    </li>
                    <li><b>Glx:</b>
                        {(c.Glx != null ? $"Type: {Encode(c.Glx.Type ?? "")}, Opcode: {Encode(c.Glx.Opcode ?? "")}" : "")}
                    </li>
                </ul>
            """
        );

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
                        <ul>
                            {string.Join("", f.Require.Select(r => $"<li>Profile: {Encode(r.Profile ?? "")}, Api: {Encode(r.Api ?? "")}, Comment: {Encode(r.Comment ?? "")}</li>"))}
                        </ul>
                    </li>
                    <li><b>Remove:</b>
                        <ul>
                            {string.Join("", f.Remove.Select(r => $"<li>Profile: {Encode(r.Profile ?? "")}, Api: {Encode(r.Api ?? "")}, Comment: {Encode(r.Comment ?? "")}</li>"))}
                        </ul>
                    </li>
                </ul>
            """
        );

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
                        <ul>
                            {string.Join("", ext.Require.Select(r => $"<li>Profile: {Encode(r.Profile ?? "")}, Api: {Encode(r.Api ?? "")}, Comment: {Encode(r.Comment ?? "")}</li>"))}
                        </ul>
                    </li>
                    <li><b>Remove:</b>
                        <ul>
                            {string.Join("", ext.Remove.Select(r => $"<li>Profile: {Encode(r.Profile ?? "")}, Api: {Encode(r.Api ?? "")}, Comment: {Encode(r.Comment ?? "")}</li>"))}
                        </ul>
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
        var typePages = registry.Types.Select(TypePage).ToImmutableArray();
        var kindPages = registry.Kinds.Select(KindPage).ToImmutableArray();
        var groupPages = registry.Groups.Select(GroupPage).ToImmutableArray();
        var enumsPages = registry.Enums.Select(EnumsPage).ToImmutableArray();
        var commandPages = registry.Commands.Select(CommandPage).ToImmutableArray();
        var featurePages = registry.Features.Select(FeaturePage).ToImmutableArray();
        var extensionPages = registry.Extensions.Select(ExtensionPage).ToImmutableArray();

        var listingPages = new[]
        {
            CreateListingPage("types", typePages),
            CreateListingPage("kinds", kindPages),
            CreateListingPage("groups", groupPages),
            CreateListingPage("enums", enumsPages),
            CreateListingPage("commands", commandPages),
            CreateListingPage("features", featurePages),
            CreateListingPage("extensions", extensionPages)
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
                .Concat(enumsPages)
                .Concat(commandPages)
                .Concat(featurePages)
                .Concat(extensionPages)
                .Concat(listingPages)
                .Append(indexPage)
        ], footer);
    }

    public static void Write(DirectoryInfo folder, Registry.Registry registry)
    {
        var (pages, footer) = GenerateAllPages(registry);
        foreach (var p in pages)
        {
            WritePage(folder, p, footer);
        }
    }
}
