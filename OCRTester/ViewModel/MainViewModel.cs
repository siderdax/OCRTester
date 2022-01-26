using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using OCRTester.Model;
using OCRTester.Model.Settings;
using Tesseract;

namespace OCRTester.ViewModel
{

    public class MainViewModel : ViewModelBase
    {
        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        private readonly IDataService _dataService;

        private int _x;
        public int X
        {
            get => _x;
            set
            {
                if (Set(ref _x, value))
                {
                    _dataService.Config.SaveConfigData(
                        new ConfigData(ConfigData.X, value.ToString(), false));
                }
            }
        }

        private int _y;
        public int Y
        {
            get => _y;
            set
            {
                if (Set(ref _y, value))
                {
                    _dataService.Config.SaveConfigData(
                        new ConfigData(ConfigData.Y, value.ToString(), false));
                }
            }
        }

        private int _width;
        public int Width
        {
            get => _width;
            set
            {
                if (Set(ref _width, value))
                {
                    _dataService.Config.SaveConfigData(
                        new ConfigData(ConfigData.WIDTH, value.ToString(), false));
                }
            }
        }

        private int _height;
        public int Height
        {
            get => _height;
            set
            {
                if (Set(ref _height, value))
                {
                    _dataService.Config.SaveConfigData(
                        new ConfigData(ConfigData.HEIGHT, value.ToString(), false));
                }
            }
        }

        private string _windowName;
        public string WindowName
        {
            get => _windowName;
            set
            {
                if (Set(ref _windowName, value))
                {
                    _dataService.Config.SaveConfigData(
                        new ConfigData(ConfigData.WINDOW_NAME, value, false));
                }
            }
        }

        private string _ocrResult;
        public string OCRResult
        {
            get => _ocrResult;
            set => Set(ref _ocrResult, value);
        }

        private System.Windows.Media.ImageSource _snapShot;
        public System.Windows.Media.ImageSource SnapShot
        {
            get => _snapShot;
            set => Set(ref _snapShot, value);
        }

        public ICommand CaptureCommand { get; private set; }
        public void CaptureCommandMethod()
        {
            if (!_dataService.WinHandler.IsRunning("capture.json"))
            {
                OCRResult = "Capturing...";
                Task.Run(() =>
                {
                    _dataService.WinHandler.Run("capture.json", X, Y, Width, Height, WindowName);

                    if (OCRResult.Equals("Capturing..."))
                        OCRResult = string.Empty;
                });
            }
        }

        private System.Windows.Media.ImageSource ImageSourceFromBitmap(Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    handle, IntPtr.Zero, Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); }
        }

        public MainViewModel(IDataService dataService)
        {
            _dataService = dataService;
            _dataService.WinHandler.OnScreenCaptured += (sender, bm) =>
            {
                string ocrText = null;

                try
                {
                    byte[] pixArray = null;

                    using (var stream = new MemoryStream())
                    {
                        bm.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
                        pixArray = stream.ToArray();
                    }

                    using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
                    {
                        if (pixArray != null)
                        {
                            using (var img = Pix.LoadFromMemory(pixArray))
                            {
                                using (var page = engine.Process(img))
                                {
                                    ocrText = page.GetText();
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    System.Windows.Forms.MessageBox.Show(e.ToString());
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    SnapShot = ImageSourceFromBitmap(bm);

                    if (!string.IsNullOrEmpty(ocrText))
                        OCRResult = ocrText;
                });
            };

            OCRResult = "OCR Tester";
            CaptureCommand = new RelayCommand(CaptureCommandMethod);

            foreach (ConfigData cd in dataService.Config.LoadConfigData())
            {
                switch (cd.Name)
                {
                    case ConfigData.WINDOW_NAME:
                        WindowName = cd.Value;
                        break;
                    case ConfigData.X:
                        if (int.TryParse(cd.Value, out int x))
                            X = x;
                        break;
                    case ConfigData.Y:
                        if (int.TryParse(cd.Value, out int y))
                            Y = y;
                        break;
                    case ConfigData.WIDTH:
                        if (int.TryParse(cd.Value, out int width))
                            Width = width;
                        break;
                    case ConfigData.HEIGHT:
                        if (int.TryParse(cd.Value, out int height))
                            Height = height;
                        break;
                }
            }
        }
    }
}