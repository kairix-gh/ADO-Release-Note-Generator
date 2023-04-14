using ADO_Release_Note_Generator_Shared;
using ADO_Release_Note_Generator_Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Newtonsoft.Json;
using Serilog;
using Serilog.Filters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web.Http;

namespace ADO_Release_Note_Generator_FunctionApp {
    public static class Function1 {
        private static ILogger logger;
        private static AppConfig Config = new AppConfig();

        [FunctionName("GetReleaseNotes")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ExecutionContext context) {
            // Initialize Logger
            logger = new LoggerConfiguration()
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

            // First, we should initialize an object to represent the configuration, with default values from json file
            // this would represent the request being made, text, work items to retreive, access token, etc.
            string json;
            try {
                json = await new StreamReader(req.Body).ReadToEndAsync();

                Config = JsonConvert.DeserializeObject<AppConfig>(json);

                // Set function context to true so we use the right paths for loading images!
                Config.FunctionContext = true;
                Config.FunctionPath = context.FunctionAppDirectory;

                if (!Config.IsValidConfig()) {
                    return new BadRequestObjectResult("Invalid Request Body");
                }
            } catch (Exception ex) {
                logger.Error(ex, "Unhandled error: {0}", ex.Message);
            }

            // Afterwards, we should then retreive the work items
            Dictionary<string, List<WorkItem>> workItemsForRelease = new Dictionary<string, List<WorkItem>>();

            try {
                logger.Debug("Retreivinig Work Items from Azure DevOps");
                workItemsForRelease = await Utils.GetAzureDevOpsWorkItems(Config, logger);
            } catch (VssUnauthorizedException) {
                logger.Fatal("Invalid credentials were provided to access Azure DevOps, please check the configuration settings in {0}", "appsettings.json");
                return new BadRequestObjectResult("Invalid Azure DevOps Credentials");
            } catch (Exception ex) {
                logger.Fatal(ex, "An unexpected error occured while retriving items from Azue DevOps.");
                return new InternalServerErrorResult();
            }

            // Then generate the PDF file
            string fileName = Utils.GetOutputFilename(Config, ignorePath: true);
            byte[] pdfDoc = Utils.GetPDFFile(logger, Config, workItemsForRelease);

            // Lastly, return it to the user
            return new FileContentResult(pdfDoc, "application/pdf") {
                FileDownloadName = fileName,
            };
        }
    }
}