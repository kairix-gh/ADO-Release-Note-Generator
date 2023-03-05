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
using Serilog;
using Serilog.Context;
using Serilog.Filters;

internal class Program {
    private static bool useFooterImage = true;
    private static bool useHeaderImage = true;
    private static AppConfig Config = new AppConfig();

    private static async Task Main(string[] args) {
        // Initialize Logger
        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Logger(lc => {
                lc.Filter.ByIncludingOnly(Matching.WithProperty("FileLog"));
                lc.WriteTo.Map("FileName", "", (fileName, wt) => {
                    if (!string.IsNullOrWhiteSpace(fileName)) {
                        wt.File($"{fileName}.txt");
                    }
                });
            })
            .CreateLogger();

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
        Dictionary<string, List<WorkItem>> workItemsForRelease = new Dictionary<string, List<WorkItem>>();

        try {
            Log.Debug("Retreivinig Work Items from Azure DevOps");
            workItemsForRelease = await GetAzureDevOpsWorkItems();
        } catch (VssUnauthorizedException) {
            Log.Fatal("Invalid credentials were provided to access Azure DevOps, please check the configuration settings in {0}", "appsettings.json");
            return;
        } catch (Exception ex) {
            Log.Fatal(ex, "An unexpected error occured while retriving items from Azue DevOps.");
            return;
        }

        string outputFileName = GetOutputFile();
        Log.Information("Generating Release Notes, and saving to: {0}", outputFileName);

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
                        var last = workItemsForRelease.Last();

                        foreach (var kv in workItemsForRelease) {
                            var itemGroup = Config.WorkItemGroups.Find(e => e.Name.ToLower() == kv.Key.ToLower());

                            if (itemGroup == null) continue;
                            col.Item().Component(new WorkItemPDFComponent(kv.Key, itemGroup, kv.Value, Config.SkipWorkItemsWithNoNotes));

                            if (kv.Key != last.Key) {
                                col.Item().PageBreak();
                            }
                        }
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
            Log.Fatal("Unable to save the release notes document to the specified path: {0}. Reason: {1}", outputFileName, ex.Message);
            return;
        } catch (Exception ex) {
            Log.Fatal(ex, "An unexpected error occured while generating the release notes document.");
            return;
        }

        Log.CloseAndFlush();
        Log.Information("Release Notes Generated Successfully!");
    }

    private static void CreateLogger() {
    }

    private static string GetOutputFile() {
        string fileName = $"Transcendent Release {Config.ReleaseInfo.Version} - {Config.ReleaseInfo.DateTime.Year}.{Config.ReleaseInfo.DateTime.Month.ToString().PadLeft(2, '0')}.{Config.ReleaseInfo.DateTime.Day.ToString().PadLeft(2, '0')}.pdf";
        if (string.IsNullOrWhiteSpace(Config.OutputPath)) {
            return Path.Combine(AppContext.BaseDirectory, fileName);
        }

        return Path.Combine(Config.OutputPath, fileName);
    }

    private static void ParseArgs(string[] args) {
        if (args.Length == 0) return;

        Log.Debug("Parsing Args");
        Dictionary<string, Action<string>> argsMap = new Dictionary<string, Action<string>>() {
            { "r", (string p) => { Config.ReleaseInfo.Version = p; } },
            { "d", (string p) => { Config.ReleaseInfo.Date = p; } },
            { "o", (string p) => { Config.OutputPath = p; } },
            { "l", (string p) => {
                LogContext.PushProperty("FileLog", true);
                LogContext.PushProperty("FileName", p);
            } }
        };

        for (int i = 0; i < args.Length; i += 2) {
            try {
                Log.Information("Executing '{0}' with param {1}.", args[i], args[i + 1]);
                argsMap[args[i].Substring(1)](args[i + 1]);
            } catch (Exception ex) {
                Log.Error(ex, "An unexpected error occured while parsing args.");
            }
        }
    }

    private static async Task<Dictionary<string, List<WorkItem>>> GetAzureDevOpsWorkItems() {
        Wiql wiql = new Wiql();
        List<WorkItem> bugs = new List<WorkItem>();
        List<WorkItem> stories = new List<WorkItem>();

        Dictionary<string, List<WorkItem>> ret = new Dictionary<string, List<WorkItem>>();

        var credentials = new VssBasicCredential(Config.AzureDevOps.Token, string.Empty);
        var connection = new VssConnection(Config.AzureDevOps.Uri, credentials);
        var client = connection.GetClient<WorkItemTrackingHttpClient>();

        foreach (WorkItemGroup group in Config.WorkItemGroups) {
            Log.Information("Retreiving Work Items for {0} using {1} as the title and {2} as the description.", group.Name, group.TitleField, group.DescriptionField);
            if (!ret.ContainsKey(group.Name)) {
                ret.Add(group.Name, new List<WorkItem>());
            }

            wiql.Query = group.Query;

            Log.Debug("Executing WIQL query: {0}", group.Query);
            var results = await client.QueryByWiqlAsync(wiql);
            var ids = results.WorkItems.Select(i => i.Id).ToArray();
            Log.Debug("Found {0} work items for group {1}", ids.Length, group.Name);

            if (ids.Length > 0) {
                ret[group.Name] = await client.GetWorkItemsAsync(ids, group.FieldArray, results.AsOf);
            }
        }

        return ret;
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

        foreach (WorkItemGroup wig in Config.WorkItemGroups) {
            if (string.IsNullOrWhiteSpace(wig.Query.Trim())) {
                return false;
            }

            if (wig.FieldArray.Length == 0) {
                return false;
            } else {
                if (!Array.Exists(wig.FieldArray, e => e.ToLower() == wig.TitleField.ToLower())) {
                    wig.Fields += $", {wig.TitleField}";
                }

                if (!Array.Exists(wig.FieldArray, e => e.ToLower() == wig.DescriptionField.ToLower())) {
                    wig.Fields += $", {wig.DescriptionField}";
                }
            }
        }

        return true;
    }
}