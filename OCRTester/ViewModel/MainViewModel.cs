using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using OCRTester.Model;
using OCRTester.Model.Settings;
using OCRTester.Model.Utility;
using Tesseract;

namespace OCRTester.ViewModel
{

    public class MainViewModel : ViewModelBase
    {
        private readonly IDataService _dataService;
        private Bitmap _originalBitmap;
        private Bitmap _convertedBitmap;

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

        private bool _useGrayscale;
        public bool UseGrayscale
        {
            get => _useGrayscale;
            set
            {
                if (Set(ref _useGrayscale, value))
                    UpdateBitmap();
            }
        }

        private bool _useThreshold;
        public bool UseThreshold
        {
            get => _useThreshold;
            set
            {
                if (Set(ref _useThreshold, value))
                    UpdateBitmap();
            }
        }

        private bool _useGsAfterTh;
        public bool UseGsAfterTh
        {
            get => _useGsAfterTh;
            set
            {
                if (Set(ref _useGsAfterTh, value))
                    UpdateBitmap();
            }
        }

        private byte _thValue;
        public byte ThValue
        {
            get => _thValue;
            set
            {
                if (Set(ref _thValue, value))
                {
                    if (LPFValue < value)
                        LPFValue = value;

                    if (HPFValue > value)
                        HPFValue = value;

                    UpdateBitmap();
                }
            }
        }

        private bool _useLPF;
        public bool UseLPF
        {
            get => _useLPF;
            set
            {
                if (Set(ref _useLPF, value))
                    UpdateBitmap();
            }
        }

        private byte _lpfValue;
        public byte LPFValue
        {
            get => _lpfValue;
            set
            {
                if (Set(ref _lpfValue, value))
                {
                    if (ThValue > value)
                        ThValue = value;

                    if (HPFValue > value)
                        HPFValue = value;

                    UpdateBitmap();
                }
            }
        }

        private bool _useHPF;
        public bool UseHPF
        {
            get => _useHPF;
            set
            {
                if (Set(ref _useHPF, value))
                    UpdateBitmap();
            }
        }

        private byte _hpfValue;
        public byte HPFValue
        {
            get => _hpfValue;
            set
            {
                if (Set(ref _hpfValue, value))
                {
                    if (ThValue < value)
                        ThValue = value;

                    if (HPFValue < value)
                        HPFValue = value;

                    UpdateBitmap();
                }
            }
        }

        private System.Windows.Media.ImageSource _snapshot;
        public System.Windows.Media.ImageSource Snapshot
        {
            get => _snapshot;
            set => Set(ref _snapshot, value);
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

        public ICommand ReadTextCommand { get; private set; }
        public void ReadTextCommandMethod()
        {
            OCRResult = ReadBitmapText(_convertedBitmap);
        }

        private readonly object _updateLock = new object();
        private Timer _updateTimer;
        private void UpdateBitmap()
        {
            if (_updateTimer != null)
                _updateTimer.Dispose();

            _updateTimer = new Timer((arg) =>
            {
                lock (_updateLock)
                {
                    if (_originalBitmap == null)
                        return;

                    _convertedBitmap = _originalBitmap.Clone() as Bitmap;

                    void applyFilter()
                    {
                        if (UseLPF)
                            BitmapUtility.SetLPF(_convertedBitmap, LPFValue);

                        if (UseHPF)
                            BitmapUtility.SetHPF(_convertedBitmap, HPFValue);

                        if (UseThreshold)
                            BitmapUtility.SetThreshold(_convertedBitmap, ThValue);
                    }

                    if (UseGrayscale)
                    {
                        if (UseGsAfterTh)
                            applyFilter();

                        BitmapUtility.ConvertToGrayscale(_convertedBitmap);

                        if (!UseGsAfterTh)
                            applyFilter();
                    }
                    else
                    {
                        applyFilter();
                    }

                }

                Application.Current.Dispatcher.Invoke(
                    () => Snapshot = BitmapUtility.ImageSourceFromBitmap(_convertedBitmap));
            }, null, 500, -1);
        }

        private string ReadBitmapText(Bitmap bitmap)
        {
            string ocrText = null;

            try
            {
                byte[] pixArray = null;

                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
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

            return ocrText;
        }

        public MainViewModel(IDataService dataService)
        {
            _dataService = dataService;
            _dataService.WinHandler.OnScreenCaptured += (sender, bm) =>
            {
                _originalBitmap = bm.Clone() as Bitmap;
                _convertedBitmap = bm.Clone() as Bitmap;

                if (UseGrayscale && !UseGsAfterTh)
                    BitmapUtility.ConvertToGrayscale(_convertedBitmap);

                if (UseThreshold)
                    BitmapUtility.SetThreshold(_convertedBitmap, ThValue);

                if (UseGrayscale && UseGsAfterTh)
                    BitmapUtility.ConvertToGrayscale(_convertedBitmap);

                Application.Current.Dispatcher.Invoke(
                    () => Snapshot = BitmapUtility.ImageSourceFromBitmap(_convertedBitmap));

                string ocrText = ReadBitmapText(_convertedBitmap);

                if (!string.IsNullOrEmpty(ocrText))
                    OCRResult = ocrText;
            };

            OCRResult = "OCR Tester";
            ThValue = 128;
            LPFValue = 255;
            HPFValue = 0;
            CaptureCommand = new RelayCommand(CaptureCommandMethod);
            ReadTextCommand = new RelayCommand(ReadTextCommandMethod);

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