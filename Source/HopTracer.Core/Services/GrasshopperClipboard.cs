using System.Xml;
using System.Xml.Linq;

namespace HopTracer.Core.Services;

public readonly record struct ClipboardRead(IReadOnlyList<string> Ids, string? Error)
{
    public static ClipboardRead Fail(string error) => new(Array.Empty<string>(), error);
}

/// <summary>
/// Reads component instance ids from a Grasshopper canvas copy.
/// </summary>
public static class GrasshopperClipboard
{
    public const int MaxChars = 8_000_000;

    public static ClipboardRead Read(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return ClipboardRead.Fail("Clipboard is empty.");
        }

        if (xml.Length > MaxChars)
        {
            return ClipboardRead.Fail("Clipboard is too large.");
        }

        var start = xml.IndexOf("<Archive", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return ClipboardRead.Fail("Clipboard is not a Grasshopper selection.");
        }

        if (start > 0)
        {
            xml = xml[start..];
        }

        XDocument doc;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxChars
            };
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            doc = XDocument.Load(reader, LoadOptions.None);
        }
        catch (Exception)
        {
            return ClipboardRead.Fail("Clipboard is not valid Grasshopper XML.");
        }

        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var obj in doc.Descendants("chunk"))
        {
            if (!Named(obj, "Object")) continue;

            var objectsChunk = obj.Parent?.Parent;
            if (!Named(objectsChunk, "DefinitionObjects")) continue;
            if (!Named(objectsChunk?.Parent?.Parent, "Clipboard")) continue;

            var container = DirectChunk(obj, "Container");
            if (container == null || IsGroup(obj, container)) continue;

            var id = DirectItem(container, "InstanceGuid");
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!seen.Add(id)) continue;
            ids.Add(id);
        }

        return new ClipboardRead(ids, null);
    }

    private static bool IsGroup(XElement obj, XElement container)
    {
        if (string.Equals(DirectItem(obj, "Name"), "Group", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(DirectItem(container, "Name"), "Group", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var description = DirectItem(container, "Description");
        return description.Contains("group of grasshopper objects", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Named(XElement? chunk, string name)
    {
        return chunk != null &&
            string.Equals(chunk.Attribute("name")?.Value, name, StringComparison.Ordinal);
    }

    private static XElement? DirectChunk(XElement parent, string name)
    {
        return parent.Element("chunks")?
            .Elements("chunk")
            .FirstOrDefault(chunk => Named(chunk, name));
    }

    private static string DirectItem(XElement parent, string name)
    {
        var item = parent.Element("items")?
            .Elements("item")
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Attribute("name")?.Value, name, StringComparison.Ordinal));
        return item?.Value?.Trim() ?? string.Empty;
    }
}
