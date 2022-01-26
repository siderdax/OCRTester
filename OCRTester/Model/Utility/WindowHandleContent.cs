namespace OCRTester.Model.Utility
{
    public class WindowHandleContent
    {
        public struct ContentItem
        {
            public int Delay { get; set; }
            public string Name { get; set; }
            public string Class { get; set; }
            public string Command { get; set; }
            public string[] Args { get; set; }
            public ContentItem[] ChildItem { get; set; }
        }

        public ContentItem[] Items { get; set; }
    }
}
