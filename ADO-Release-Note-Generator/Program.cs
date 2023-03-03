using ADO_Release_Note_Generator.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

internal class Program {

    private static async Task Main(string[] args) {
        // Setup Configuration
        var configRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
#if DEBUG
            .AddUserSecrets("b4a5b96c-9b36-4279-822a-a882526d6965")
#endif
            .Build();

        AppConfig config = new AppConfig();
        configRoot.Bind(config);

        // Get work items from ADO
        Wiql wiql = new Wiql();
        List<WorkItem> bugs = new List<WorkItem>();
        List<WorkItem> stories = new List<WorkItem>();

        var credentials = new VssBasicCredential(config.AzureDevOps.Token, string.Empty);
        var connection = new VssConnection(config.AzureDevOps.Uri, credentials);
        var client = connection.GetClient<WorkItemTrackingHttpClient>();

        // Get Bug List
        wiql.Query = config.WorkItems.FixesQuery;
        var results = client.QueryByWiqlAsync(wiql).Result;
        var ids = results.WorkItems.Select(i => i.Id).ToArray();

        if (ids.Length > 0) {
            bugs = client.GetWorkItemsAsync(ids, config.WorkItems.WorkItemFieldArray, results.AsOf).Result;
        }

        wiql.Query = config.WorkItems.FeaturesQuery;
        results = client.QueryByWiqlAsync(wiql).Result;
        ids = results.WorkItems.Select(i => i.Id).ToArray();

        if (ids.Length > 0) {
            stories = client.GetWorkItemsAsync(ids, config.WorkItems.WorkItemFieldArray, results.AsOf).Result;
        }

        Document.Create(container => {
            // Cover Page
            container.Page(page => {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);

                // Content
                page.Content().AlignMiddle().Column(col => {
                    col.Item().PaddingBottom(10).Text("Release Notes").SemiBold().FontSize(26);
                    col.Item().Text($"{config.ReleaseInfo.DateTime.ToString("MMMM dd, yyyy")} Update");
                });

                // Footer
                page.Footer().AlignMiddle().Row(row => {
                    if (File.Exists(Path.Combine(AppContext.BaseDirectory, config.FooterImagePath))) {
                        row.AutoItem().AlignLeft().Height(48).Hyperlink(config.FooterHyperlink).Image(Path.Combine(AppContext.BaseDirectory, config.FooterImagePath), ImageScaling.FitHeight);
                    } else {
                        row.AutoItem().AlignLeft().Width(48).Height(36).PaddingRight(10).Hyperlink(config.FooterHyperlink).Placeholder();
                    }

                    row.RelativeItem().AlignMiddle().Column(col => {
                        col.Item().Text(config.FooterAddress);
                        col.Item().Hyperlink(config.FooterHyperlink).Text(config.FooterHyperlinkText);
                    });
                });
            });

            container.Page(page => {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);

                page.Header().AlignMiddle().Row(row => {
                    if (File.Exists(Path.Combine(AppContext.BaseDirectory, config.HeaderImagePath))) {
                        row.AutoItem().AlignLeft().Width(200).Height(36).Image(Path.Combine(AppContext.BaseDirectory, config.HeaderImagePath), ImageScaling.FitHeight);
                    } else {
                        row.AutoItem().AlignLeft().Width(125).Height(36).Placeholder();
                    }

                    row.RelativeItem().AlignRight().AlignMiddle().Text($"Release {config.ReleaseInfo.Version} - {config.ReleaseInfo.DateTime.ToString("M/dd/yyyy")}").FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(x => {
                    // Output Features & Fixes
                    x.Item().Text("Features").FontSize(16);
                    x.WriteWorkItems(stories);

                    x.Item().PageBreak();

                    x.Item().Text("Fixes").FontSize(18);
                    x.WriteWorkItems(bugs);
                });

                page.Footer().Column(col => {
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken2);
                    col.Spacing(2);
                    col.Item().Row(row => {
                        row.AutoItem().AlignLeft().Text($"Copyright {DateTime.Now.Year}, Transcendent© Corporation").FontColor(Colors.Grey.Darken2); ;
                        row.RelativeItem().AlignRight().Text(x => {
                            x.Span("Page ").FontColor(Colors.Grey.Darken2); ;
                            x.CurrentPageNumber().FontColor(Colors.Grey.Darken2); ;
                            x.Span(" of ").FontColor(Colors.Grey.Darken2); ;
                            x.TotalPages().FontColor(Colors.Grey.Darken2); ;
                        });
                    });
                });
            });
        }).GeneratePdf("demo.pdf");

        Console.WriteLine("Done");
    }
}

public static class QuestPDFExtensions {

    public static void WriteWorkItems(this ColumnDescriptor col, IEnumerable<WorkItem> items) {
        string itemDesc = "";
        int count = 1;

        foreach (WorkItem item in items) {
            if (!item.Fields.TryGetValue("Custom.ReleaseNotesNotes", out itemDesc)) {
                itemDesc = Placeholders.LoremIpsum();
            }

            col.Item().PaddingBottom(10).Row(row => {
                row.AutoItem().PaddingLeft(10).Text($"{count}. ").FontSize(12);
                row.Spacing(10);

                row.RelativeItem().Column(col => {
                    col.Item().Text(item.Fields["System.Title"]?.ToString()).FontSize(12);
                    col.Item().Text(itemDesc).FontSize(12).FontColor(Colors.Grey.Darken3);
                });
            });

            count++;
            col.Spacing(10);
        }
    }
}