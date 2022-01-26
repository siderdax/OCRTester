using System.Windows.Forms;
using OCRTester.Model.Utility;

namespace OCRTester.Model
{
    public class DataService : IDataService
    {
        public WindowHandler WinHandler { get; private set; }

        public DataService()
        {
            WinHandler = new WindowHandler(Application.StartupPath + @"\Handler\");
        }
    }
}