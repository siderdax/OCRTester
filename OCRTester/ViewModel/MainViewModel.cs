using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using OCRTester.Model;

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
            set => Set(ref _x, value);
        }

        private int _y;
        public int Y
        {
            get => _y;
            set => Set(ref _y, value);
        }

        private int _width;
        public int Width
        {
            get => _width;
            set => Set(ref _width, value);
        }

        private int _height;
        public int Height
        {
            get => _height;
            set => Set(ref _height, value);
        }

        private string _windowName;
        public string WindowName
        {
            get => _windowName;
            set => Set(ref _windowName, value);
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
                    OCRResult = "Captured";
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
                Application.Current.Dispatcher.Invoke(() => SnapShot = ImageSourceFromBitmap(bm));

            Width = 320;
            Height = 240;
            OCRResult = "OCR Tester";
            CaptureCommand = new RelayCommand(CaptureCommandMethod);
        }
    }
}