namespace ADO_Release_Note_Generator.Models {
    /// <summary>
    /// Application configuration from appsettings.json
    /// </summary>
    public class AppConfig {
        public string FooterAddress { get; set; } = "";
        public string FooterHyperlink { get; set; } = "";
        public string FooterHyperlinkText { get; set; } = "";
        public string FooterImagePath { get; set; } = "";
        public string HeaderImagePath { get; set; } = "";
        public bool SkipWorkItemsWithNoNotes { get; set; } = true;
        public string OutputPath { get; set; } = "";

        public ReleaseInfoConfig ReleaseInfo { get; set; } = new ReleaseInfoConfig();
        public AzureDevOpsConfig AzureDevOps { get; set; } = new AzureDevOpsConfig();

        public List<WorkItemGroup> WorkItemGroups { get; set; } = new List<WorkItemGroup>();
    }
}