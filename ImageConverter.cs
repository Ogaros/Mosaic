using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

namespace Mosaic
{
    internal static class ImageConverter
    {
        public static Bitmap BitmapImageToBitmap(BitmapImage bi)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                JpegBitmapDecoder jd = null;
                if(bi.UriSource != null)
                    jd = new JpegBitmapDecoder(bi.UriSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                else                    
                    jd = new JpegBitmapDecoder(bi.StreamSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                BitmapEncoder enc = new BmpBitmapEncoder();            
                enc.Frames.Add(jd.Frames[0]);
                enc.Save(outStream);
                Bitmap bitmap = new Bitmap(outStream);
                return new Bitmap(bitmap);
            }
        }

        public static BitmapImage BitmapToBitmapImage(Bitmap b)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                b.Save(outStream, ImageFormat.Bmp);
                outStream.Position = 0;

                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = outStream;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();

                return bi;
            }
        }
    }
}
