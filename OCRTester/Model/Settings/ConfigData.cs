namespace OCRTester.Model.Settings
{
    public class ConfigData
    {
        public const string WINDOW_NAME = "window_name";
        public const string X = "x";
        public const string Y = "y";
        public const string WIDTH = "width";
        public const string HEIGHT = "height";

        public string Name { get; set; }
        public string Value { get; set; }
        public bool Encrypt { get; set; }

        public ConfigData(string name, string value, bool encrypt)
        {
            Name = name;
            Value = value;
            Encrypt = encrypt;
        }
    }
}
