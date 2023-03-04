using ADO_Release_Note_Generator.Utils;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ADO_Release_Note_Generator.QuestPDFComponents {
    internal class WorkItemPDFComponent : IComponent {
        private string title = "Work Items";
        private IEnumerable<WorkItem> items = new List<WorkItem>();
        private bool skipItems = true;

        public WorkItemPDFComponent(string title, IEnumerable<WorkItem> items, bool skipItems) {
            this.title = title;
            this.items = items;
            this.skipItems = skipItems;
        }

        public void Compose(IContainer container) {
            int count = 1;
            string itemDesc = "";

            container.Column(col => {
                col.Item().Text(title).FontSize(16);
                foreach (WorkItem item in items) {
                    if (!item.Fields.TryGetValue("Custom.ReleaseNotesNotes", out itemDesc) &&
                        !item.Fields.TryGetValue("TranscendentAgile.ReleaseNotes", out itemDesc)) {
#if DEBUG
                        itemDesc = $"{Placeholders.Sentence()} {Placeholders.Sentence()}";
#else
                        if (skipItems) {
                            continue;
                        }
#endif
                    }

                    // Remove any HTML from Azure DevOps and trim newlines
                    itemDesc = HTMLUtils.SanitizeHTML(itemDesc);
                    itemDesc = itemDesc.Trim().TrimEnd(Environment.NewLine.ToCharArray());

                    col.Item().PaddingBottom(10).Row(row => {
                        row.AutoItem().PaddingLeft(10).Text($"{count}. ").FontSize(12);
                        row.Spacing(10);

                        row.RelativeItem().Column(col => {
                            col.Item().Text(item.Fields["System.Title"]?.ToString()).FontSize(12);
                            col.Item().Text(itemDesc).FontSize(12).FontColor(Colors.Grey.Darken3);
                        });
                    });

                    count++;
                    col.Item().PaddingBottom(10);
                }

                if (count == 1) {
                    col.Item().PaddingLeft(10).Text($"No items were included in this release.");
                }
            });
        }
    }
}