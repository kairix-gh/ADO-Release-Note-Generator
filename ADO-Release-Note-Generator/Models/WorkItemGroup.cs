namespace ADO_Release_Note_Generator.Models {
    public class WorkItemGroup {
        public string Name { get; set; } = "";
        public string Query { get; set; } = "";

        private string _fields = "";
        public string Fields {
            get => _fields;
            set {
                if (_fields != value) _fields = value;

                FieldArray = _fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        }

        public string[] FieldArray { get; private set; } = new string[0];

        public string TitleField { get; set; } = "";
        public string DescriptionField { get; set; } = "";
    }
}