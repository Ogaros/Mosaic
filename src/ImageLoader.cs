using System;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace Mosaic
{
    static class ImageLoader
    {
        public static Bitmap LoadImageBitmap(Image image, ImageSource.Type sourceType)
        {
            Bitmap bitmap = null;
            try
            {
                if (sourceType == ImageSource.Type.Directory)
                {
                    bitmap = new Bitmap(image.path);
                }
                else
                {
                    BitmapImage bi = new BitmapImage(new Uri(image.path));
                    bitmap = bi.ToBitmap();
                }
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException || ex is ArgumentException)
                {
                    return null;
                }
                else
                    throw;
            }
            return bitmap;
        }

        /// <summary>
        /// Only works if the image is already indexed
        /// </summary>
        public static Bitmap LoadImageBitmap(Image image)
        {
            var sourceType = DBManager.GetImageSourceType(image);
            return LoadImageBitmap(image, sourceType);
        }
    }
}
