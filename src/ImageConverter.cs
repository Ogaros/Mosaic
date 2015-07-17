using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace Mosaic
{
    internal static class ImageConverter
    {
        public static Bitmap BitmapImageToBitmap(BitmapImage bi)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapDecoder decoder = null;
                BitmapEncoder encoder = null;
                if (bi.UriSource != null)
                {
                    String uriString = bi.UriSource.ToString();
                    String format = uriString.Substring(uriString.LastIndexOf('.'));
                    switch (format)
                    {
                        case ".png":
                            decoder = new PngBitmapDecoder(bi.UriSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                            encoder = new PngBitmapEncoder();
                            break;
                        case ".gif":
                            decoder = new GifBitmapDecoder(bi.UriSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                            encoder = new GifBitmapEncoder();
                            break;
                        case ".bmp":
                            decoder = new BmpBitmapDecoder(bi.UriSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                            encoder = new BmpBitmapEncoder();
                            break;
                        case ".tiff":
                            decoder = new TiffBitmapDecoder(bi.UriSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                            encoder = new TiffBitmapEncoder();
                            break;
                        default:
                            decoder = new JpegBitmapDecoder(bi.UriSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                            encoder = new JpegBitmapEncoder();
                            break;
                    }
                }
                else
                {
                    decoder = new JpegBitmapDecoder(bi.StreamSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                    encoder = new JpegBitmapEncoder();
                }           
                encoder.Frames.Add(decoder.Frames[0]);
                encoder.Save(outStream);
                using (Bitmap bitmap = new Bitmap(outStream))
                {
                    Bitmap result = new Bitmap(bitmap);
                    result.SetResolution((float)bi.DpiX, (float)bi.DpiY);
                    return result;
                }
            }
        }

        public static BitmapImage BitmapToBitmapImage(Bitmap b)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                b.Save(outStream, ImageFormat.Jpeg);
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
