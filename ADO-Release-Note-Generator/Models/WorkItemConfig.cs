namespace ADO_Release_Note_Generator.Models {
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