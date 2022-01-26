using System.Windows.Forms;
using OCRTester.Model.Utility;
using OCRTester.Model.Settings;

namespace OCRTester.Model
{
    public class DataService : IDataService
    {
        public WindowHandler WinHandler { get; private set; }
        public Config Config { get; private set; }

        public DataService()
        {
            WinHandler = new WindowHandler(Application.StartupPath + @"\Handler\");
            Config = new Config(
                new byte[] { // AES Key
                    67, 62, 35, 217, 124, 177, 145, 40, 9, 36, 61, 101, 38, 18, 41, 222, 182, 142,
                    27, 249, 117, 221, 2, 228, 110, 111, 176, 5, 206, 206, 42, 144
                },
                new byte[] { // AES IV
                    157, 35, 98, 58, 109, 55, 97, 63, 194, 131, 137, 37, 2, 107, 102, 66
                }
            );
        }
    }
}