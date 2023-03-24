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