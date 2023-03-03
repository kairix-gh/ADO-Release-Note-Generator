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

        public ReleaseInfoConfig ReleaseInfo { get; set; } = new ReleaseInfoConfig();
        public AzureDevOpsConfig AzureDevOps { get; set; } = new AzureDevOpsConfig();
        public WorkItemConfig WorkItems { get; set; } = new WorkItemConfig();
    }

    /// <summary>
    /// Release Information configuration loaded from appsettings.json
    /// </summary>
    public class ReleaseInfoConfig {
        public string Version { get; set; } = string.Empty;

        public string _dateString = string.Empty;

        public string Date {
            get {
                return _dateString;
            }
            set {
                if (_dateString != value) _dateString = value;

                if (DateTime.TryParse(value, out DateTime tmpDateTime)) {
                    DateTime = tmpDateTime;
                } else {
                    DateTime = DateTime.Today;
                }
            }
        }

        public DateTime DateTime { get; private set; }
    }

    /// <summary>
    /// Azure DevOps configuration loaded from appsettings.json
    /// </summary>
    public class AzureDevOpsConfig {
        public string Token { get; set; } = string.Empty;

        private string _url = "";

        public string Url {
            get => _url;
            set {
                if (_url != value) _url = value;

                if (Uri.TryCreate(_url, UriKind.Absolute, out Uri? tmpUri)) {
                    Uri = tmpUri;
                } else {
                    Uri = null;
                }
            }
        }

        public Uri? Uri { get; private set; }
    }

    /// <summary>
    /// Work Item configuration laoded from appsettings.json
    /// </summary>
    public class WorkItemConfig {
        public string FeaturesQuery { get; set; } = string.Empty;
        public string FixesQuery { get; set; } = string.Empty;

        private string _workItemFields = "";

        public string WorkItemFields {
            get => _workItemFields;
            set {
                if (_workItemFields != value) _workItemFields = value;

                WorkItemFieldArray = _workItemFields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        }

        public string[] WorkItemFieldArray { get; private set; } = new string[0];
    }
}