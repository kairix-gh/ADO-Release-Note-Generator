using ADO_Release_Note_Generator.Utils;
using ADO_Release_Note_Generator_Shared.Models;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using QuestPDF.Drawing.Exceptions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Net;

namespace ADO_Release_Note_Generator_Shared.QuestPDF {
    internal class WorkItemPDFComponent : IComponent {
        private string title = "Work Items";
        private IEnumerable<WorkItem> items = new List<WorkItem>();
        private bool skipItems = true;
        private WorkItemGroup itemGroup;

        private int maxWidth;
        private int maxHeight;

        public WorkItemPDFComponent(string title, WorkItemGroup itemGroup, IEnumerable<WorkItem> items, bool skipItems, int maxWidth = 400, int maxHeight = 500) {
            this.title = title;
            this.items = items;
            this.skipItems = skipItems;
            this.itemGroup = itemGroup;
            this.maxWidth = maxWidth;
            this.maxHeight = maxHeight;
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
                        if (!item.Fields.TryGetValue("System.Title", out itemTitle) || string.IsNullOrWhiteSpace(itemTitle)) {
                            // All items should have a title, if this doesn't we want to skip
                            continue;
                        }
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
                            col.Item().PaddingBottom(10).Text(itemDesc).FontSize(12).FontColor(Colors.Grey.Darken3);

                            // Create an inline container for images, this will always be a little wonky because
                            // of scaling.
                            col.Item().Inlined(inline => {
                                // Check if we have an ImageList field and try to get the list of byte[]
                                if (item.Fields.ContainsKey("ImageList")) {
                                    if (item.Fields.TryGetValue("ImageList", out List<ReleaseNoteImageInfo> imageList)) {
                                        foreach (ReleaseNoteImageInfo imageData in imageList) {
                                            try {
                                                bool useResizeWidth = false;

                                                if (imageData.Width > maxWidth || imageData.Height > maxHeight) {
                                                    useResizeWidth = true;
                                                }

                                                // If our image exceeds maximum width/height we have QuestPDF resize the image
                                                // for us.
                                                if (useResizeWidth) {
                                                    inline.Item().Padding(5).Image(imageData.Bytes, ImageScaling.FitWidth);
                                                } else {
                                                    // Otherwise we use the width/height of the image
                                                    inline.Item()
                                                        .Width(imageData.Width)
                                                        .Height(imageData.Height)
                                                        .Padding(5)
                                                    .Image(imageData.Bytes, ImageScaling.Resize);
                                                }
                                            } catch (DocumentComposeException) {
                                                // Likely because the byte[] couldn't be rendered to an image. Instead
                                                // let's show placeholder graphics to indicate to the user that there
                                                // was an issue with their image. Otherwise they might assume it just
                                                // "doesn't work".
                                                inline.Item().Width(150).Height(75).Placeholder();
                                            }
                                        }
                                    }
                                }
                            });
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