using System.Collections.Immutable;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Xml;
using Glox.Html;
using Glox.Registry;

var app = new CommandApp<MainCommand>();
return app.Run(args);

internal sealed class MainCommand : Command<MainCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to gl.xml")]
        [CommandArgument(0, "<gl.xml>")]
        public string OpenGlXml { get; set; } = ""; 
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        var x = new XmlDocument();
        x.LoadXml(File.ReadAllText(settings.OpenGlXml));
        const string registryElementName = "registry";
        var reg = x[registryElementName];
        var errors = new Errors();
        if(reg != null)
        {
            var p = $"/{registryElementName}";
            using var doc = new El(new(errors, p, p), reg);
            var registry = Parser.Parse(doc);

            Writer.Write(new DirectoryInfo(Directory.GetCurrentDirectory()), registry);
        }
        else
        {
            errors.Report("/", "/", $"Missing {registryElementName}");
        }
        AnsiConsole.WriteLine("Program done.");
        return errors.Return();
    }
}

internal class Location(Errors errors, string path, string genericPath)
{
    public void ReportError(string message, string? note = null)
    {
        errors.Report(path, genericPath, message, note);
    }

    public Location Sub(string name, int index)
    {
        return new Location(errors,
            path: $"{path}/{name}[{index}]",
            genericPath: $"{genericPath}/{name}"
            );
    }
}

internal class El : IDisposable
{
    public El(Location location, XmlElement element)
    {
        _location = location;
        _elements = [..element.Cast<XmlNode>().Where(x => x is XmlElement).Cast<XmlElement>()];
        _unusedElements = _elements.Select(x => x.Name).ToHashSet();

        _attributes = element.Attributes.Cast<XmlAttribute>().ToDictionary(x => x.Name, x => x.Value);


        var text = element.Cast<XmlNode>().Where(x => x is XmlText).Cast<XmlText>().Select(x => x.Value).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToImmutableArray();
        _innerText = text;
    }

    private readonly ImmutableArray<XmlElement> _elements;
    private readonly HashSet<string> _unusedElements;
    private ImmutableArray<string> _innerText;
    private readonly Dictionary<string, string> _attributes;
    private readonly Location _location;

    public Location Location => _location;

    public IEnumerable<El> ElementsNamed(string name)
    {
        _unusedElements.Remove(name);
        int index = 0;
        foreach (var x in _elements)
        {
            if (x.Name != name) continue;
            using var r = new El(_location.Sub(name, index), x);
            yield return r;
            index += 1;
        }
    }

    public void Dispose()
    {
        if (_unusedElements.Count > 0)
        {
            var m = string.Join(", ", _unusedElements);
            _location.ReportError($"Unused elements: {m}");
        }

        if (_innerText.Length > 0)
        {
            var i = string.Join(" ", _innerText);
            _location.ReportError("Unused inner text", i);
        }

        if (_attributes.Count > 0)
        {
            var a = string.Join(", ", _attributes.Keys);
            _location.ReportError($"Unused attributes: {a}");
        }
    }

    public ImmutableArray<string> ReadInnerText()
    {
        var r = _innerText;
        _innerText = [];
        return r;
    }

    public string? ReadAttribute(string name)
        => _attributes.Remove(name, out var ret) ? ret : null;
}

internal class Errors
{
    private readonly HashSet<string> _reported = new();
    private int _errorCount = 0;

    public void Report(string path, string generic, string message, string? note = null)
    {
        var gm = $"{generic}/{message}";
        _errorCount++;

        if (!_reported.Add(gm)) return;
        AnsiConsole.MarkupLineInterpolated($"[red]ERROR[/]: [blue]{path}[/]: {message}");
        if(note != null)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]note[/]: {note}");
        }

    }

    public int Return()
    {
        AnsiConsole.MarkupLineInterpolated($"Detected [red]{_errorCount}[/] errors");

        if (_errorCount == 0) return 0;
        else return -_errorCount;
    }
}
