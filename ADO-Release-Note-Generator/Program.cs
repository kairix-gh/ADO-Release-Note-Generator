using ADO_Release_Note_Generator_Shared;
using ADO_Release_Note_Generator_Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using Serilog;
using Serilog.Context;
using Serilog.Filters;

internal class Program {
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

        if (!Config.IsValidConfig()) {
            return;
        }

        ParseArgs(args);

        // Get work items from ADO
        Dictionary<string, List<WorkItem>> workItemsForRelease = await Utils.GetAzureDevOpsWorkItems(Config, Log.Logger);
        /*
        try {
            Log.Debug("Retreivinig Work Items from Azure DevOps");
            workItemsForRelease = await GetAzureDevOpsWorkItems();
        } catch (VssUnauthorizedException) {
            Log.Fatal("Invalid credentials were provided to access Azure DevOps, please check the configuration settings in {0}", "appsettings.json");
            return;
        } catch (Exception ex) {
            Log.Fatal(ex, "An unexpected error occured while retriving items from Azue DevOps.");
            return;
        }*/

        string outputFileName = Utils.GetOutputFilename(Config);
        Log.Information("Generating Release Notes, and saving to: {0}", outputFileName);

        // Create PDF file and save to disk
        try {
            byte[] pdfDoc = Utils.GetPDFFile(Log.Logger, Config, workItemsForRelease);

            using BinaryWriter writer = new BinaryWriter(File.OpenWrite(outputFileName));
            writer.Write(pdfDoc);
        } catch (UnauthorizedAccessException ex) {
            Log.Fatal("Unable to save the release notes document to the specified path: {0}. Reason: {1}", outputFileName, ex.Message);
            return;
        } catch (Exception ex) {
            Log.Fatal(ex, "An unexpected error occured while generating the release notes document.");
            return;
        }

        /*
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
        }*/

        Log.CloseAndFlush();
        Log.Information("Release Notes Generated Successfully!");
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
            } catch (IndexOutOfRangeException ex) {
                Log.Error(ex, "Missing parameter for argument {0}. The {0} argument will be ignored", args[i]);
            } catch (Exception ex) {
                Log.Error(ex, "An unexpected error occured while parsing the argument {0}.", args[i]);
            }
        }
    }
}