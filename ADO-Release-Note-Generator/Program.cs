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
    private static bool useFooterImage = true;
    private static bool useHeaderImage = true;
    private static AppConfig Config = new AppConfig();

    private static async Task Main(string[] args) {
        // Setup Configuration
        var configRoot = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
#if DEBUG
            .AddUserSecrets("b4a5b96c-9b36-4279-822a-a882526d6965")
#endif
            .Build();

        configRoot.Bind(Config);

        if (!ValidateConfiguration()) {
            return;
        }

        // Get work items from ADO
        List<WorkItem> bugs = new List<WorkItem>();
        List<WorkItem> stories = new List<WorkItem>();

        (stories, bugs) = await GetAzureDevOpsWorkItems();

        string outputFile = $"Transcendent Release {Config.ReleaseInfo.Version} - {Config.ReleaseInfo.DateTime.Year}.{Config.ReleaseInfo.DateTime.Month.ToString().PadLeft(2, '0')}.{Config.ReleaseInfo.DateTime.Day.ToString().PadLeft(2, '0')}.pdf";

        Document.Create(container => {
            // Cover Page
            container.Page(page => {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);

                // Content
                page.Content().AlignMiddle().Column(col => {
                    col.Item().PaddingBottom(10).Text("Release Notes").SemiBold().FontSize(26);
                    col.Item().Text($"{Config.ReleaseInfo.DateTime.ToString("MMMM dd, yyyy")} Update");
                });

                // Footer
                page.Footer().AlignMiddle().Row(row => {
                    if (useFooterImage) {
                        row.AutoItem().AlignLeft().Height(48).Hyperlink(Config.FooterHyperlink).Image(Path.Combine(AppContext.BaseDirectory, Config.FooterImagePath), ImageScaling.FitHeight);
                    } else {
                        row.AutoItem().AlignLeft().Width(48).Height(36).PaddingRight(10).Hyperlink(Config.FooterHyperlink).Placeholder();
                    }

                    row.RelativeItem().AlignMiddle().Column(col => {
                        col.Item().Text(Config.FooterAddress);
                        col.Item().Hyperlink(Config.FooterHyperlink).Text(Config.FooterHyperlinkText);
                    });
                });
            });

            // Notes Page
            container.Page(page => {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);

                // Notes Header
                page.Header().AlignMiddle().Row(row => {
                    if (useHeaderImage) {
                        row.AutoItem().AlignLeft().Width(200).Height(36).Image(Path.Combine(AppContext.BaseDirectory, Config.HeaderImagePath), ImageScaling.FitHeight);
                    } else {
                        row.AutoItem().AlignLeft().Width(125).Height(36).Placeholder();
                    }

                    row.RelativeItem().AlignRight().AlignMiddle().Text($"Release {Config.ReleaseInfo.Version} - {Config.ReleaseInfo.DateTime.ToString("M/dd/yyyy")}").FontColor(Colors.Grey.Darken2);
                });

                // Notes Content
                page.Content().PaddingVertical(1, Unit.Centimetre).Column(x => {
                    // Write Features
                    x.Item().Text("Features").FontSize(16);
                    x.WriteWorkItems(stories);

                    x.Item().PageBreak();

                    // Write Fixes
                    x.Item().Text("Fixes").FontSize(18);
                    x.WriteWorkItems(bugs);
                });

                // Notes Footer
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
        }).GeneratePdf(outputFile);

        Console.WriteLine("Done");
    }

    private static async Task<Tuple<List<WorkItem>, List<WorkItem>>> GetAzureDevOpsWorkItems() {
        Wiql wiql = new Wiql();
        List<WorkItem> bugs = new List<WorkItem>();
        List<WorkItem> stories = new List<WorkItem>();

        var credentials = new VssBasicCredential(Config.AzureDevOps.Token, string.Empty);
        var connection = new VssConnection(Config.AzureDevOps.Uri, credentials);
        var client = connection.GetClient<WorkItemTrackingHttpClient>();

        // Get Bug List
        wiql.Query = Config.WorkItems.FixesQuery;
        var results = await client.QueryByWiqlAsync(wiql);
        var ids = results.WorkItems.Select(i => i.Id).ToArray();

        if (ids.Length > 0) {
            bugs = await client.GetWorkItemsAsync(ids, Config.WorkItems.WorkItemFieldArray, results.AsOf);
        }

        wiql.Query = Config.WorkItems.FeaturesQuery;
        results = await client.QueryByWiqlAsync(wiql);
        ids = results.WorkItems.Select(i => i.Id).ToArray();

        if (ids.Length > 0) {
            stories = await client.GetWorkItemsAsync(ids, Config.WorkItems.WorkItemFieldArray, results.AsOf);
        }

        return Tuple.Create(stories, bugs);
    }

    private static bool ValidateConfiguration() {
        // Check if the cover page footer image exists and is usable
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, Config.FooterImagePath))) {
            Program.useFooterImage = false;
        }

        // Check if the notes page header image exists and is usable
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, Config.HeaderImagePath))) {
            Program.useHeaderImage = false;
        }

        // If we don't have a valid ADO Url, we cannot retreive items
        if (!Uri.IsWellFormedUriString(Config.AzureDevOps.Url, UriKind.Absolute)) {
            return false;
        }

        // Check if we have a valid ADO token
        if (string.IsNullOrWhiteSpace(Config.AzureDevOps.Token.Trim())) {
            return false;
        }

        // Confirm we have a query to retreive features
        if (string.IsNullOrWhiteSpace(Config.WorkItems.FeaturesQuery.Trim())) {
            return false;
        }

        // Confirm we have a query to retreive bug fixes
        if (string.IsNullOrWhiteSpace(Config.WorkItems.FixesQuery.Trim())) {
            return false;
        }

        // Confirm we have a fields from the Work Items & that we
        // always have the required fields.
        if (Config.WorkItems.WorkItemFieldArray.Length == 0) {
            return false;
        } else {
            if (!Array.Exists(Config.WorkItems.WorkItemFieldArray, e => e.ToLower() == "system.title")) {
                Config.WorkItems.WorkItemFields += ", System.Title";
            }

            if (!Array.Exists(Config.WorkItems.WorkItemFieldArray, e => e.ToLower() == "custom.releasenotesnotes")) {
                Config.WorkItems.WorkItemFields += ", Custom.ReleaseNotesNotes";
            }
        }

        return true;
    }
}

public static class QuestPDFExtensions {

    public static void WriteWorkItems(this ColumnDescriptor col, IEnumerable<WorkItem> items) {
        string itemDesc = "";
        int count = 1;

        foreach (WorkItem item in items) {
            if (!item.Fields.TryGetValue("Custom.ReleaseNotesNotes", out itemDesc)) {
#if DEBUG
                itemDesc = Placeholders.LoremIpsum();
#else
                continue;
#endif
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