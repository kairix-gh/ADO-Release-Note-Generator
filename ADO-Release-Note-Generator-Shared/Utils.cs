using ADO_Release_Note_Generator.Utils;
using ADO_Release_Note_Generator_Shared.Models;
using ADO_Release_Note_Generator_Shared.QuestPDF;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Serilog;
using SkiaSharp;

namespace ADO_Release_Note_Generator_Shared {
    public static class Utils {
        public static async Task<Dictionary<string, List<WorkItem>>> GetAzureDevOpsWorkItems(AppConfig Config, ILogger logger) {
            Wiql wiql = new Wiql();

            Dictionary<string, List<WorkItem>> ret = new Dictionary<string, List<WorkItem>>();

            var credentials = new VssBasicCredential(Config.AzureDevOps.Token, string.Empty);
            var connection = new VssConnection(Config.AzureDevOps.Uri, credentials);
            var client = connection.GetClient<WorkItemTrackingHttpClient>();

            // Iterate over each WorkItemGroup from the config/request
            foreach (WorkItemGroup group in Config.WorkItemGroups) {
                logger.Information("Retreiving Work Items for {0} using {1} as the title and {2} as the description.", group.Name, group.TitleField, group.DescriptionField);
                if (!ret.ContainsKey(group.Name)) {
                    ret.Add(group.Name, new List<WorkItem>());
                }

                wiql.Query = group.Query;

                logger.Debug("Executing WIQL query: {0}", group.Query);
                var results = await client.QueryByWiqlAsync(wiql);
                var ids = results.WorkItems.Select(i => i.Id).ToArray();
                logger.Debug("Found {0} work items for group {1}", ids.Length, group.Name);

                if (ids.Length > 0) {
                    // If we have work items, let's get them
                    ret[group.Name] = await client.GetWorkItemsAsync(ids, group.FieldArray, results.AsOf);

                    // Get Images
                    try {
                        await Task.WhenAll(
                            ret[group.Name].Select(async item => {
                                if (!item.Fields.TryGetValue(group.DescriptionField, out string itemDesc) || string.IsNullOrWhiteSpace(itemDesc)) {
#if DEBUG
                                    itemDesc = $"{Placeholders.Sentence()} {Placeholders.Sentence()}";
#else
                                    return;
#endif
                                }

                                List<ReleaseNoteImageInfo> imageUrls = HTMLUtils.GetImageUrls(itemDesc);

                                foreach (ReleaseNoteImageInfo imageInfo in imageUrls) {
                                    var imageArray = await GetAzureDevOpsWorkItemImages(logger, client, imageInfo.Url);

                                    if (imageArray.Length > 0) {
                                        imageInfo.Bytes = imageArray;
                                        if (!item.Fields.ContainsKey("ImageList")) {
                                            item.Fields.Add("ImageList", new List<ReleaseNoteImageInfo>());
                                        }

                                        // Fix sizes
                                        SKBitmap bitmap = SKBitmap.Decode(imageInfo.Bytes);

                                        if (imageInfo.Width == 0) {
                                            imageInfo.Width = bitmap.Width;
                                        }

                                        if (imageInfo.Height == 0) {
                                            imageInfo.Height = bitmap.Height;
                                        }

                                        (item.Fields["ImageList"] as List<ReleaseNoteImageInfo>).Add(imageInfo);
                                    }
                                }
                            })
                        );
                    } catch (Exception ex) {
                        logger.Error(ex.Message);
                    }
                }
            }

            return ret;
        }

        public static async Task<byte[]> GetAzureDevOpsWorkItemImages(ILogger logger, WorkItemTrackingHttpClient client, string url) {
            byte[] ret;

            try {
                Uri imgUrl = new Uri(url);
                string[] urlParts = imgUrl.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                int guidIndex = urlParts.Length - 1;
                Guid imgGuid = new Guid(urlParts[guidIndex]);

                var imageStream = await client.GetAttachmentContentAsync(imgGuid);

                using (var stream = new MemoryStream()) {
                    imageStream.CopyTo(stream);
                    ret = stream.ToArray();
                }

                if (ret == null) {
                    ret = new byte[0];
                }

                return ret;
            } catch (Exception) {
                throw;
            }
        }

        public static string GetOutputFilename(AppConfig Config, bool ignorePath = false) {
            string fileName = $"Transcendent Release {Config.ReleaseInfo.Version} - {Config.ReleaseInfo.DateTime.Year}.{Config.ReleaseInfo.DateTime.Month.ToString().PadLeft(2, '0')}.{Config.ReleaseInfo.DateTime.Day.ToString().PadLeft(2, '0')}.pdf";

            if (ignorePath) {
                return fileName;
            }

            if (string.IsNullOrWhiteSpace(Config.OutputPath)) {
                return Path.Combine(AppContext.BaseDirectory, fileName);
            }

            return Path.Combine(Config.OutputPath, fileName);
        }

        public static byte[] GetPDFFile(ILogger logger, AppConfig Config, Dictionary<string, List<WorkItem>> workItemsForRelease) {
            byte[] ret;

            try {
                ret = Document.Create(container => {
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
                            string imgPath = Path.Combine(Config.GetBasePath(), Config.FooterImagePath);
                            if (File.Exists(imgPath)) {
                                row.AutoItem().AlignLeft().Height(48).Hyperlink(Config.FooterHyperlink).Image(imgPath, ImageScaling.FitHeight);
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
                            string imgPath = Path.Combine(Config.GetBasePath(), Config.HeaderImagePath);
                            if (File.Exists(imgPath)) {
                                row.AutoItem().AlignLeft().Width(200).Height(36).Image(imgPath, ImageScaling.FitHeight);
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

                                // TODO: Calc safe width/height based on page size
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
                }).GeneratePdf();
            } catch (Exception ex) {
                if (Config.FunctionContext) {
                    logger.Fatal(ex, "An unexpected error occured while generating the release notes document.");
                    ret = Document.Create(doc => { doc.Page(p => { p.Content().Text("An unexpected error occured while saving the document."); }); }).GeneratePdf();
                } else {
                    throw;
                }
            }

            return ret;
        }
    }
}