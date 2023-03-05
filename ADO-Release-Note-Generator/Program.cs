using ADO_Release_Note_Generator.Models;
using ADO_Release_Note_Generator.QuestPDFComponents;
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

        ParseArgs(args);

        // Get work items from ADO
        List<WorkItem> bugs = new List<WorkItem>();
        List<WorkItem> stories = new List<WorkItem>();

        try {
            (stories, bugs) = await GetAzureDevOpsWorkItems();
        } catch (VssUnauthorizedException) {
            Console.WriteLine($"Invalid credentials were provided to access Azure DevOps, please check configuration settings.");
            return;
        } catch (Exception ex) {
            Console.WriteLine($"Ran into an unexpected error while retreiving Azure DevOps work items.");
            Console.WriteLine($"{ex.Message}");
            return;
        }

        string outputFileName = GetOutputFile();
        Console.WriteLine($"Generating Release Notes, and saving to: {outputFileName}");

        // Create PDF file and save to disk
        try {
            Document.Create(container => {
                // Cover Page
                container.Page(page => {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);

                    // Content
                    page.Content().AlignMiddle().Column(col => {
                        col.Item().PaddingBottom(10).Text("Release Notes").SemiBold().FontSize(26);
                        col.Item().Text($"Update {Config.ReleaseInfo.Version} - {Config.ReleaseInfo.DateTime.ToString("MMMM dd, yyyy")}");
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
                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col => {
                        // Write Features
                        col.Item().Component(new WorkItemPDFComponent("Features", stories, Config.SkipWorkItemsWithNoNotes));

                        col.Item().PageBreak();

                        // Write Fixes
                        col.Item().Component(new WorkItemPDFComponent("Fixes", bugs, Config.SkipWorkItemsWithNoNotes));
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
            }).GeneratePdf($"{outputFileName}");
        } catch (UnauthorizedAccessException ex) {
            Console.WriteLine($"We were unable to save the release notes document to the specified path.");
            Console.WriteLine($"Exception Message: {ex.Message}");
        } catch (Exception ex) {
            Console.WriteLine($"Ran into an unexpected error while composing PDF document.");
            Console.WriteLine($"{ex.Message}");
        }
        Console.WriteLine("Done");
    }

    private static string GetOutputFile() {
        string fileName = $"Transcendent Release {Config.ReleaseInfo.Version} - {Config.ReleaseInfo.DateTime.Year}.{Config.ReleaseInfo.DateTime.Month.ToString().PadLeft(2, '0')}.{Config.ReleaseInfo.DateTime.Day.ToString().PadLeft(2, '0')}.pdf";
        if (string.IsNullOrWhiteSpace(Config.OutputPath)) {
            return fileName;
        }

        return Path.Combine(Config.OutputPath, fileName);
    }

    private static void ParseArgs(string[] args) {
        if (args.Length == 0) return;

        Console.WriteLine("Parsing args");
        Dictionary<string, Action<string>> argsMap = new Dictionary<string, Action<string>>() {
            { "r", (string p) => { Config.ReleaseInfo.Version = p; } },
            { "d", (string p) => { Config.ReleaseInfo.Date = p; } },
            { "o", (string p) => { Config.OutputPath = p; } },
        };

        for (int i = 0; i < args.Length; i += 2) {
            try {
                Console.WriteLine($"Executing: {args[i]} with param {args[i + 1]}");
                argsMap[args[i].Substring(1)](args[i + 1]);
            } catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }
    }

    private static async Task<Tuple<List<WorkItem>, List<WorkItem>>> GetAzureDevOpsWorkItems() {
        Wiql wiql = new Wiql();
        List<WorkItem> bugs = new List<WorkItem>();
        List<WorkItem> stories = new List<WorkItem>();

        var credentials = new VssBasicCredential(Config.AzureDevOps.Token, string.Empty);
        var connection = new VssConnection(Config.AzureDevOps.Uri, credentials);
        var client = connection.GetClient<WorkItemTrackingHttpClient>();

        // Get Bug Items
        wiql.Query = Config.WorkItems.FixesQuery;
        var results = await client.QueryByWiqlAsync(wiql);
        var ids = results.WorkItems.Select(i => i.Id).ToArray();

        if (ids.Length > 0) {
            bugs = await client.GetWorkItemsAsync(ids, Config.WorkItems.WorkItemFieldArray, results.AsOf);
        }

        // Get Feature items
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