namespace ADO_Release_Note_Generator_Shared.Models {
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
}