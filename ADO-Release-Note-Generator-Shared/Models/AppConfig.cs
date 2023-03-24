namespace ADO_Release_Note_Generator_Shared.Models {
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

        public bool FunctionContext { get; set; } = false;
        public string FunctionPath { get; set; } = "";

        public ReleaseInfoConfig ReleaseInfo { get; set; } = new ReleaseInfoConfig();
        public AzureDevOpsConfig AzureDevOps { get; set; } = new AzureDevOpsConfig();

        public List<WorkItemGroup> WorkItemGroups { get; set; } = new List<WorkItemGroup>();

        public string GetBasePath() {
            return (FunctionContext ? FunctionPath : AppContext.BaseDirectory);
        }

        // This is bad, should return validation errors instead of bool
        public bool IsValidConfig() {
            if (FunctionContext && string.IsNullOrWhiteSpace(FunctionPath)) {
                return false;
            }

            // If we don't have a valid ADO Url, we cannot retreive items
            if (!Uri.IsWellFormedUriString(AzureDevOps.Url, UriKind.Absolute)) {
                return false;
            }

            // Check if we have a valid ADO token
            if (string.IsNullOrWhiteSpace(AzureDevOps.Token.Trim())) {
                return false;
            }

            foreach (WorkItemGroup wig in WorkItemGroups) {
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
}