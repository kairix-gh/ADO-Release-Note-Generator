using ADO_Release_Note_Generator_Shared.Models;
using HtmlAgilityPack;

namespace ADO_Release_Note_Generator.Utils {
    public static class HTMLUtils {

        // Allowed tags to not strip from a string. We keep br is allowed so
        // we can replace them with new lines instead.
        private static string[] ALLOWED_TAGS = new string[] { "br" };

        public static string SanitizeHTML(string html, string[]? allowedTags = null) {
            if (string.IsNullOrWhiteSpace(html)) return html;

            if (allowedTags == null) allowedTags = ALLOWED_TAGS;

            // Replace &nbsp; or &quot; entities with string equivalents
            html = HtmlEntity.DeEntitize(html);

            // Load html into an HtmlDocument object we can maniupulate
            var doc = new HtmlDocument() {
                OptionWriteEmptyNodes = true,
                OptionFixNestedTags = true,
                OptionAutoCloseOnEnd = true,
            };
            doc.LoadHtml(html);

            // Select all nodes in the DOM and if we have none, we exit
            HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("./*|./text()");

            if (nodes == null || !nodes.Any()) return html;

            // Create a queue of our nodes and iterate through them
            var nodeQueue = new Queue<HtmlNode>(nodes);
            while (nodeQueue.Count > 0) {
                // Get node and parent
                HtmlNode node = nodeQueue.Dequeue();
                HtmlNode parent = node.ParentNode;

                // If our node is an invalid tag, we remove it
                if (!allowedTags.Contains(node.Name) && node.Name != "#text") {
                    if (node.InnerHtml == string.Empty) {
                        node.Remove();
                        continue;
                    }

                    // If our node has children, we unparent them and add them to the queue before
                    // removing the node from the DOM
                    HtmlNodeCollection children = node.SelectNodes("./*|./text()");
                    if (children != null) {
                        foreach (var child in children) {
                            nodeQueue.Enqueue(child);
                            parent.InsertBefore(child, node);
                        }
                    }

                    parent.RemoveChild(node);
                }
            }

            // Replace <br/> with Environment.NewLine
            nodes = doc.DocumentNode.SelectNodes("//br");
            if (nodes != null) {
                foreach (var node in nodes) {
                    node.ParentNode.ReplaceChild(doc.CreateTextNode(Environment.NewLine + Environment.NewLine), node);
                }
            }

            return doc.DocumentNode.InnerHtml;
        }

        public static List<ReleaseNoteImageInfo> GetImageUrls(string html) {
            if (string.IsNullOrWhiteSpace(html)) return Enumerable.Empty<ReleaseNoteImageInfo>().ToList();

            List<ReleaseNoteImageInfo> ret = new List<ReleaseNoteImageInfo>();

            // Replace &nbsp; or &quot; entities with string equivalents
            html = HtmlEntity.DeEntitize(html);

            // Load html into an HtmlDocument object we can maniupulate
            var doc = new HtmlDocument() {
                OptionWriteEmptyNodes = true,
                OptionFixNestedTags = true,
                OptionAutoCloseOnEnd = true,
            };
            doc.LoadHtml(html);

            // Find all the image nodes in the document
            var nodes = doc.DocumentNode.SelectNodes("//img");

            // If we have nodes, then we add them up and return them
            if (nodes != null) {
                foreach (var node in nodes) {
                    ret.Add(new ReleaseNoteImageInfo() {
                        Url = node.Attributes["src"]?.Value ?? "",
                        Width = int.Parse(node.Attributes["width"]?.Value ?? "0"),
                        Height = int.Parse(node.Attributes["height"]?.Value ?? "0"),
                    });
                }
            }

            return ret;
        }
    }
}