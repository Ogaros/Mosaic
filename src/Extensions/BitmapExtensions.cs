using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;

namespace Mosaic
{
    static internal class BitmapExtensions
    {
        public static String GetSHA256Hash(this Bitmap image)
        {
            using (SHA256 sha = SHA256Managed.Create())
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    image.Save(ms, ImageFormat.Jpeg);
                    var hash = sha.ComputeHash(ms.ToArray());
                    return System.Text.Encoding.Default.GetString(hash);
                }
            }
        }

        public static Color GetAverageColor(this Bitmap image, int x, int y, int width, int height)
        {
            if (x < 0 || x >= image.Width)
                throw new ArgumentOutOfRangeException("x");
            if (y < 0 || y >= image.Height)
                throw new ArgumentOutOfRangeException("y");

            int xEnd = x + width;
            int yEnd = y + height;

            if (width < 0 || xEnd > image.Width)
                throw new ArgumentOutOfRangeException("width");
            if (height < 0 || yEnd > image.Height)
                throw new ArgumentOutOfRangeException("height");

            long R = 0, G = 0, B = 0, pixelCount = 0;
            Color pixelColor;
                        
            for (; y < yEnd; y++)
            {
                for (int tempX = x; tempX < xEnd; tempX++, pixelCount++)
                {
                    pixelColor = image.GetPixel(tempX, y);
                    R += pixelColor.R;
                    G += pixelColor.G;
                    B += pixelColor.B;
                }
            }

            return Color.FromArgb((int)(R / pixelCount), (int)(G / pixelCount), (int)(B / pixelCount));
        }

        public static Color GetAverageColor(this Bitmap image)
        {
            return GetAverageColor(image, 0, 0, image.Width, image.Height);
        }

        public static BitmapImage ToBitmapImage(this Bitmap image)
        {
            BitmapImage bi;
            using (MemoryStream outStream = new MemoryStream())
            {
                image.Save(outStream, ImageFormat.Jpeg);                
                outStream.Position = 0;

                bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = outStream;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
            }
            return bi;            
        }
    }
}
