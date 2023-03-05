namespace ADO_Release_Note_Generator.Models {
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
}