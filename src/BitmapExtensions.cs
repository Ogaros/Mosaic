using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;

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

        public static Color GetAverageColor(this Bitmap image)
        {
            long R = 0, G = 0, B = 0;
            Color tempColor;

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    tempColor = image.GetPixel(x, y);
                    R += tempColor.R;
                    G += tempColor.G;
                    B += tempColor.B;
                }
            }

            long pixelCount = image.Height * image.Width;
            return Color.FromArgb((int)(R / pixelCount), (int)(G / pixelCount), (int)(B / pixelCount));
        }
    }
}
