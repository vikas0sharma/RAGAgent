using HtmlAgilityPack;

namespace PersonalAssistant.Services;

public class UrlExtractor(HttpClient httpClient)
{
    public async Task<string> ExtractTextAsync(string url)
    {
        var html = await httpClient.GetStringAsync(url);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Remove script and style nodes
        var nodesToRemove = doc.DocumentNode.SelectNodes("//script|//style|//nav|//footer|//header");
        if (nodesToRemove != null)
        {
            foreach (var node in nodesToRemove)
            {
                node.Remove();
            }
        }

        var text = doc.DocumentNode.InnerText;

        // Clean up whitespace
        var lines = text.Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l));

        return string.Join("\n", lines);
    }
}
