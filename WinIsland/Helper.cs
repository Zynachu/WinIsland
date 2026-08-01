using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Storage.Streams;
using static WinIsland.PInvoke;
using Color = System.Windows.Media.Color;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Drawing.Point;

namespace WinIsland
{
    public class Helper
    {
        public static Color Lighten(Color color, float factor)
        {
            // factor is typically between 0 and 1. 0 returns the original color, 1 returns white.
            return Color.FromArgb(
                color.A,
                (byte)(color.R + (255 - color.R) * factor),
                (byte)(color.G + (255 - color.G) * factor),
                (byte)(color.B + (255 - color.B) * factor)
            );
        }
        public static bool isWindows11()
        {
            RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var buildNumberString = registryKey.GetValue("CurrentBuildNumber").ToString();
            if (buildNumberString == null) return false;
            int buildNumber = Int32.Parse(buildNumberString);
            return buildNumber > 22000 ? true : false;
        }
        public static double GetDpiScale(Window handle)
        {
            var hwnd = new WindowInteropHelper(handle).Handle;
            var source = HwndSource.FromHwnd(hwnd);
            if (source?.CompositionTarget != null)
            {
                return source.CompositionTarget.TransformToDevice.M11; // X axis DPI scale
            }
            return 1.0; // Default scale
        }

        // Color format: ABGR (DO NOT SPECIFY ALPHA VALUE)
        public static void setBorderColor(Window window, System.Windows.Media.Color rgb, int hexColor = 0x000000FF, Border w10Border = null)
        {
            MainWindow.logger.logVerbose("Setting border color to " + rgb.ToString());
            if (isWindows11())
            {
                IntPtr hWnd = new WindowInteropHelper(Window.GetWindow(window)).EnsureHandle();
                int color = hexColor;
                DwmSetWindowAttribute(hWnd, PInvoke.DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, ref color, Marshal.SizeOf(color));
            }
            else
            {
                if (w10Border == null) return;
                if (rgb == null) return;
                w10Border.BorderBrush = new SolidColorBrush(rgb);
            }
        }

        public static int ConvertToABGR(int r, int g, int b)
        {
            MainWindow.logger.logVerbose("Converting RGB to ABGR");
            string rstr = r.ToString("X");
            string gstr = g.ToString("X");
            string bstr = b.ToString("X");
            string abgr = "0x00" + bstr + gstr + rstr;
            return Convert.ToInt32(abgr, 16);
        }
        public static System.Windows.Media.Color CalculateAverageColor(Bitmap bm)
        {
            Stopwatch calcDuration = MainWindow.logger.startCounter();
            if (bm == null) return System.Windows.Media.Color.FromRgb(0, 0, 0);
            MainWindow.logger.logVerbose("Getting average color...");

            // Downsample to image for faster processing
            int targetSize = 50;
            Bitmap downsampledBm;

            if (bm.Width > targetSize || bm.Height > targetSize)
            {
                MainWindow.logger.logVerbose($"[CalculateAverageColor] Downsampling from {bm.Width}x{bm.Height} to {targetSize}x{targetSize}");
                downsampledBm = new Bitmap(targetSize, targetSize);
                using (Graphics g = Graphics.FromImage(downsampledBm))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
                    g.DrawImage(bm, 0, 0, targetSize, targetSize);
                }
            }
            else
            {
                downsampledBm = bm;
            }

            int width = downsampledBm.Width;
            int height = downsampledBm.Height;
            int red = 0;
            int green = 0;
            int blue = 0;
            int minDiversion = 15;
            int dropped = 0;
            long[] totals = new long[] { 0, 0, 0 };
            int bppModifier = downsampledBm.PixelFormat == System.Drawing.Imaging.PixelFormat.Format24bppRgb ? 3 : 4;

            MainWindow.logger.logVerbose("[CalculateAverageColor] Locking BitmapBits...");
            BitmapData srcData = downsampledBm.LockBits(
                new System.Drawing.Rectangle(0, 0, downsampledBm.Width, downsampledBm.Height),
                ImageLockMode.ReadOnly,
                downsampledBm.PixelFormat);
            int stride = srcData.Stride;
            IntPtr Scan0 = srcData.Scan0;

            unsafe
            {
                MainWindow.logger.logVerbose("[CalculateAverageColor] Getting color...");
                byte* p = (byte*)(void*)Scan0;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int idx = (y * stride) + x * bppModifier;
                        red = p[idx + 2];
                        green = p[idx + 1];
                        blue = p[idx];
                        if (Math.Abs(red - green) > minDiversion || Math.Abs(red - blue) > minDiversion || Math.Abs(green - blue) > minDiversion)
                        {
                            totals[2] += red;
                            totals[1] += green;
                            totals[0] += blue;
                        }
                        else
                        {
                            dropped++;
                        }
                    }
                }
            }

            downsampledBm.UnlockBits(srcData);
            
            if (downsampledBm != bm)
            {
                downsampledBm.Dispose();
            }

            int count = width * height - dropped;
            int avgR, avgB, avgG;
            if (totals[2] != 0)
                avgR = (int)(totals[2] / count);
            else
                avgR = 255;
            if (totals[1] != 0)
                avgG = (int)(totals[1] / count);
            else
                avgG = 255;
            if (totals[0] != 0)
                avgB = (int)(totals[0] / count);
            else
                avgB = 255;
            MainWindow.logger.logVerbose("[CalculateAverageColor] Color successfully calculated.");
      
            MainWindow.logger.logVerbose("[CalculateAverageColor] Color Data: R:" +
            Convert.ToByte(avgR) + " G: " + Convert.ToByte(avgG) + " B: " +
            Convert.ToByte(avgB));
            MainWindow.logger.stopCounter(calcDuration, "CalculateAverageColor");
            return System.Windows.Media.Color.FromRgb(Convert.ToByte(avgR), Convert.ToByte(avgG), Convert.ToByte(avgB));
        }

        public static Bitmap CreateBlurredBitmap(Bitmap source, int blurRadius)
        {
            if (source == null) return null;
            if (blurRadius <= 0) return source;

            Stopwatch blurDuration = MainWindow.logger.startCounter();
            MainWindow.logger.logVerbose($"[CreateBlurredBitmap] Creating blurred bitmap with radius {blurRadius}");

            // Downsample for better performance - blur works fine on smaller images
            int targetWidth = 100;  // Reasonable size for background
            int targetHeight = (int)(source.Height * (targetWidth / (float)source.Width));

            Bitmap resized = new Bitmap(targetWidth, targetHeight);
            using (Graphics g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(source, 0, 0, targetWidth, targetHeight);
            }

            //Bitmap blurred = ApplyBoxBlur(resized, blurRadius);
            int scaledRadius = Math.Max(1, (int)(blurRadius * (targetWidth / (float)source.Width)));
            Bitmap blurred = ApplyGaussianBlur(resized, scaledRadius);

            resized.Dispose();

            MainWindow.logger.stopCounter(blurDuration, "CreateBlurredBitmap");
            return blurred;
        }
        private static Bitmap ApplyGaussianBlur(Bitmap source, int radius)
        {
            if (radius < 1) return source;

            // Generate Gaussian kernel
            double sigma = radius / 3.0; // Standard deviation
            int kernelSize = radius * 2 + 1;
            double[,] kernel = CreateGaussianKernel(kernelSize, sigma);

            Bitmap result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);

            BitmapData srcData = source.LockBits(
                new System.Drawing.Rectangle(0, 0, source.Width, source.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            BitmapData dstData = result.LockBits(
                new System.Drawing.Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            int width = source.Width;
            int height = source.Height;
            int stride = srcData.Stride;

            unsafe
            {
                byte* srcPtr = (byte*)srcData.Scan0;
                byte* dstPtr = (byte*)dstData.Scan0;

                // Apply Gaussian blur
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        double r = 0, g = 0, b = 0, a = 0;

                        for (int ky = -radius; ky <= radius; ky++)
                        {
                            for (int kx = -radius; kx <= radius; kx++)
                            {
                                int px = Math.Max(0, Math.Min(width - 1, x + kx));
                                int py = Math.Max(0, Math.Min(height - 1, y + ky));

                                int idx = py * stride + px * 4;
                                double weight = kernel[ky + radius, kx + radius];

                                b += srcPtr[idx] * weight;
                                g += srcPtr[idx + 1] * weight;
                                r += srcPtr[idx + 2] * weight;
                                a += srcPtr[idx + 3] * weight;
                            }
                        }

                        int dstIdx = y * stride + x * 4;
                        dstPtr[dstIdx] = (byte)Math.Min(255, Math.Max(0, b));
                        dstPtr[dstIdx + 1] = (byte)Math.Min(255, Math.Max(0, g));
                        dstPtr[dstIdx + 2] = (byte)Math.Min(255, Math.Max(0, r));
                        dstPtr[dstIdx + 3] = (byte)Math.Min(255, Math.Max(0, a));
                    }
                }
            }

            source.UnlockBits(srcData);
            result.UnlockBits(dstData);

            return result;
        }

        private static double[,] CreateGaussianKernel(int size, double sigma)
        {
            double[,] kernel = new double[size, size];
            double sum = 0;
            int radius = size / 2;

            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    double exponent = -(x * x + y * y) / (2 * sigma * sigma);
                    double value = Math.Exp(exponent) / (2 * Math.PI * sigma * sigma);
                    kernel[y + radius, x + radius] = value;
                    sum += value;
                }
            }

            // Normalize the kernel
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    kernel[y, x] /= sum;
                }
            }

            return kernel;
        }

        private static Bitmap ApplyBoxBlur(Bitmap source, int radius)
        {
            if (radius < 1) return source;

            Bitmap result = new Bitmap(source.Width, source.Height,
                PixelFormat.Format32bppArgb);

            BitmapData srcData = source.LockBits(
                new System.Drawing.Rectangle(0, 0, source.Width, source.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            BitmapData dstData = result.LockBits(
                new System.Drawing.Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            int width = source.Width;
            int height = source.Height;
            int stride = srcData.Stride;

            unsafe
            {
                byte* srcPtr = (byte*)srcData.Scan0;
                byte* dstPtr = (byte*)dstData.Scan0;

                // Horizontal pass
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int r = 0, g = 0, b = 0, a = 0, count = 0;

                        for (int kx = -radius; kx <= radius; kx++)
                        {
                            int px = x + kx;
                            if (px >= 0 && px < width)
                            {
                                int idx = y * stride + px * 4;
                                b += srcPtr[idx];
                                g += srcPtr[idx + 1];
                                r += srcPtr[idx + 2];
                                a += srcPtr[idx + 3];
                                count++;
                            }
                        }

                        int dstIdx = y * stride + x * 4;
                        dstPtr[dstIdx] = (byte)(b / count);
                        dstPtr[dstIdx + 1] = (byte)(g / count);
                        dstPtr[dstIdx + 2] = (byte)(r / count);
                        dstPtr[dstIdx + 3] = (byte)(a / count);
                    }
                }
            }

            source.UnlockBits(srcData);
            result.UnlockBits(dstData);

            Bitmap finalResult = new Bitmap(result.Width, result.Height, PixelFormat.Format32bppArgb);
            BitmapData tmpData = result.LockBits(
                new System.Drawing.Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            BitmapData finalData = finalResult.LockBits( 
                new System.Drawing.Rectangle(0, 0, finalResult.Width, finalResult.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* tmpPtr = (byte*)tmpData.Scan0;
                byte* finalPtr = (byte*)finalData.Scan0;

                // Vertical pass
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int r = 0, g = 0, b = 0, a = 0, count = 0;

                        for (int ky = -radius; ky <= radius; ky++)
                        {
                            int py = y + ky;
                            if (py >= 0 && py < height)
                            {
                                int idx = py * stride + x * 4;
                                b += tmpPtr[idx];
                                g += tmpPtr[idx + 1];
                                r += tmpPtr[idx + 2];
                                a += tmpPtr[idx + 3];
                                count++;
                            }
                        }

                        int dstIdx = y * stride + x * 4;
                        finalPtr[dstIdx] = (byte)(b / count);
                        finalPtr[dstIdx + 1] = (byte)(g / count);
                        finalPtr[dstIdx + 2] = (byte)(r / count);
                        finalPtr[dstIdx + 3] = (byte)(a / count);
                    }
                }
            }

            result.UnlockBits(tmpData);
            finalResult.UnlockBits(finalData);
            result.Dispose();

            return finalResult;
        }

        public static Bitmap getImageFromUrl(string imageUrl, ImageFormat format)
        {
            WebClient client = new WebClient();
            Stream stream = client.OpenRead(imageUrl);
            Bitmap bitmap = new Bitmap(stream);

            stream.Flush();
            stream.Close();
            client.Dispose();

            return bitmap;
        }
        public static BitmapImage? GetThumbnail(IRandomAccessStreamReference Thumbnail, bool convertToPng = true)
        {
            Stopwatch getThumbDuration = MainWindow.logger.startCounter();
            if (Thumbnail == null)
                return null;

            var thumbnailStream = Thumbnail.OpenReadAsync().GetAwaiter().GetResult();
            byte[] thumbnailBytes = new byte[thumbnailStream.Size];
            using (DataReader reader = new DataReader(thumbnailStream))
            {
                reader.LoadAsync((uint)thumbnailStream.Size).GetAwaiter().GetResult();
                reader.ReadBytes(thumbnailBytes);
            }

            byte[] imageBytes = thumbnailBytes;

            if (convertToPng)
            {
                using var fileMemoryStream = new System.IO.MemoryStream(thumbnailBytes);
                Bitmap thumbnailBitmap = (Bitmap)Bitmap.FromStream(fileMemoryStream);

                if (!thumbnailBitmap.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Png))
                {
                    using var pngMemoryStream = new System.IO.MemoryStream();
                    thumbnailBitmap.Save(pngMemoryStream, System.Drawing.Imaging.ImageFormat.Png);
                    imageBytes = pngMemoryStream.ToArray();
                }
            }

            var image = new BitmapImage();
            using (var ms = new System.IO.MemoryStream(imageBytes))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();
            }
            MainWindow.logger.stopCounter(getThumbDuration, "GetThumbnail");
            return image;
        }
        public static Bitmap? GetBitmap(IRandomAccessStreamReference Thumbnail)
        {
            if (Thumbnail == null)
                return null;

            var thumbnailStream = Thumbnail.OpenReadAsync().GetAwaiter().GetResult();
            byte[] thumbnailBytes = new byte[thumbnailStream.Size];
            using (DataReader reader = new DataReader(thumbnailStream))
            {
                reader.LoadAsync((uint)thumbnailStream.Size).GetAwaiter().GetResult();
                reader.ReadBytes(thumbnailBytes);
            }

            byte[] imageBytes = thumbnailBytes;

            using var fileMemoryStream = new System.IO.MemoryStream(thumbnailBytes);

            return (Bitmap)Bitmap.FromStream(fileMemoryStream);
        }
        
        public static BitmapImage ConvertToImageSource(Bitmap src)
        {
            Stopwatch convertDuration = MainWindow.logger.startCounter();
            // Fix for crash on certain images.
            // Clone into a new 32bpp ARGB bitmap (safe for encoding)
            using (var safeBitmap = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(safeBitmap))
                {
                    g.DrawImage(src, 0, 0, src.Width, src.Height);
                }

                using (var memory = new MemoryStream())
                {
                    safeBitmap.Save(memory, ImageFormat.Png);
                    memory.Position = 0;

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = memory;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    MainWindow.logger.stopCounter(convertDuration, "ConvertToImageSource");
                    return bitmapImage;
                }
            }
        }
    }
}
