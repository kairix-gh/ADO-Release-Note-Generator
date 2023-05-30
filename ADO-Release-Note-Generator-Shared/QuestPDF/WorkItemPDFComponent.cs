using ADO_Release_Note_Generator.Utils;
using ADO_Release_Note_Generator_Shared.Models;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ADO_Release_Note_Generator_Shared.QuestPDF {
    internal class WorkItemPDFComponent : IComponent {
        private string title = "Work Items";
        private IEnumerable<WorkItem> items = new List<WorkItem>();
        private bool skipItems = true;
        private WorkItemGroup itemGroup;

        public WorkItemPDFComponent(string title, WorkItemGroup itemGroup, IEnumerable<WorkItem> items, bool skipItems) {
            this.title = title;
            this.items = items;
            this.skipItems = skipItems;
            this.itemGroup = itemGroup;
        }

        public void Compose(IContainer container) {
            int count = 1;
            string itemTitle = "";
            string itemDesc = "";

            container.Column(col => {
                col.Item().Text(title).FontSize(16);
                foreach (WorkItem item in items) {
                    // Ensure we have title and description
                    if (!item.Fields.TryGetValue(itemGroup.TitleField, out itemTitle) || string.IsNullOrWhiteSpace(itemTitle)) {
#if DEBUG
                        itemTitle = $"{itemGroup.Name} Item";
#else
                        continue;
#endif
                    }

                    if (!item.Fields.TryGetValue(itemGroup.DescriptionField, out itemDesc) || string.IsNullOrWhiteSpace(itemDesc)) {
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
                            col.Item().Text(itemTitle).FontSize(12);
                            col.Item().Text("Dev Ops: " + item.Id.ToString()).FontSize(12);
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