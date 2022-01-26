using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OCRTester.Model.Utility
{
    public static class BitmapUtility
    {
        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        public static System.Windows.Media.ImageSource ImageSourceFromBitmap(Bitmap bmp)
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

        public static void ConvertToGrayscale(Bitmap bitmap)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    int value = (int)Math.Round(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                    bitmap.SetPixel(x, y, Color.FromArgb(pixel.A, value, value, value));
                }
            };
        }

        public static void SetLPF(Bitmap bitmap, byte value)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    bitmap.SetPixel(x, y, Color.FromArgb(pixel.A,
                                                         pixel.R < value ? pixel.R : value,
                                                         pixel.G < value ? pixel.G : value,
                                                         pixel.B < value ? pixel.B : value));
                }
            };
        }

        public static void SetHPF(Bitmap bitmap, byte value)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    bitmap.SetPixel(x, y, Color.FromArgb(pixel.A,
                                                         pixel.R > value ? pixel.R : value,
                                                         pixel.G > value ? pixel.G : value,
                                                         pixel.B > value ? pixel.B : value));
                }
            };
        }

        public static void SetThreshold(Bitmap bitmap, byte value)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    bitmap.SetPixel(x, y, Color.FromArgb(pixel.A,
                                                         pixel.R < value ? 0 : 255,
                                                         pixel.G < value ? 0 : 255,
                                                         pixel.B < value ? 0 : 255));
                }
            };
        }
    }
}
