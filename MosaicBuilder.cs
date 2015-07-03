using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.Collections.Generic;

namespace Mosaic
{
    internal class Image : IEquatable<Image>
    {
        public Image(String p, Color c)
        {
            path = p;
            color = c;
            bitmap = null;
        }
        public bool Equals(Image other)
        {
            return other.path == this.path;
        }
        public String path;
        public Color color;
        public Bitmap bitmap;
    }

    class MosaicBuilder : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public int progress { get; set; }
        public BitmapImage original { get; private set; }
        public BitmapImage mosaic { get; private set; }
        private Bitmap originalBitmap;
        private List<Image> usedImages;
        private List<ImageSource> imageSources;
        private int sectorWidthMosaic;
        private int sectorHeightMosaic;
        private const int colorError = 15; // error for the average color. separate for each of the primary colors
        private Random rand = new Random();
        public ErrorType errorStatus { get; private set; }

        public MosaicBuilder() { }

        public MosaicBuilder(Bitmap bitmap)
        {
            setImage(bitmap);
        }
        public MosaicBuilder(BitmapImage bitmapImage)
        {
            setImage(bitmapImage);
        }

        public void setImage(Bitmap bitmap)
        {
            originalBitmap = (Bitmap)bitmap.Clone();
            original = ImageConverter.BitmapToBitmapImage(bitmap);
        }
        public void setImage(BitmapImage bitmapImage)
        {
            originalBitmap = ImageConverter.BitmapImageToBitmap(bitmapImage);
            original = bitmapImage.Clone();
        }

        public BitmapImage getOriginal()
        {
            return original;
        }

        public BitmapImage getMosaic()
        {
            return mosaic;
        }

        public void buildMosaic(int resolutionW, int resolutionH, int horSectors, int verSectors, List<ImageSource> imageSources)
        {
            errorStatus = ErrorType.NoErrors;
            this.imageSources = imageSources;
            progress = 0;
            usedImages = new List<Image>();

            int sectorWidthOriginal = originalBitmap.Width / horSectors;
            int sectorHeightOriginal = originalBitmap.Height / verSectors;
            if (sectorWidthOriginal == 0 || sectorHeightOriginal == 0)
            {
                errorStatus = ErrorType.TooManySectors;
                return;
            }
            horSectors = originalBitmap.Width / sectorWidthOriginal;
            verSectors = originalBitmap.Height / sectorHeightOriginal;
            sectorWidthMosaic = resolutionW > originalBitmap.Width ? (resolutionW / horSectors) + 1 : resolutionW / horSectors;
            sectorHeightMosaic = resolutionH > originalBitmap.Height ? (resolutionH / verSectors) + 1 : resolutionH / verSectors;
            if (sectorWidthMosaic == 0)
                sectorWidthMosaic = 1;
            if (sectorHeightMosaic == 0)
                sectorHeightMosaic = 1;
            using (Bitmap mosaicBitmapTemp = new Bitmap(horSectors * sectorWidthMosaic, verSectors * sectorHeightMosaic, originalBitmap.PixelFormat))
            {
                mosaicBitmapTemp.SetResolution(originalBitmap.HorizontalResolution, originalBitmap.VerticalResolution);

                for (int xOrig = 0, xMos = 0, horSecCount = 0; horSecCount < horSectors; xOrig += sectorWidthOriginal, xMos += sectorWidthMosaic, horSecCount++)
                {
                    for (int yOrig = 0, yMos = 0, verSecCount = 0; verSecCount < verSectors; yOrig += sectorHeightOriginal, yMos += sectorHeightMosaic, verSecCount++)
                    {
                        Bitmap sector = getSector(xOrig, yOrig, sectorWidthOriginal, sectorHeightOriginal);

                        sector = fillSector(sector);

                        Graphics g = Graphics.FromImage(mosaicBitmapTemp);
                        g.DrawImage(sector, xMos, yMos);
                        g.Dispose();

                        ++progress;
                        OnPropertyChanged("progress");
                        sector.Dispose();
                    }

                }
                using (Bitmap mosaicBitmap = new Bitmap(resolutionW, resolutionH, originalBitmap.PixelFormat))
                {
                    mosaicBitmap.SetResolution(originalBitmap.HorizontalResolution, originalBitmap.VerticalResolution);
                    Graphics gr = Graphics.FromImage(mosaicBitmap);
                    gr.DrawImage(mosaicBitmapTemp, 0, 0, resolutionW, resolutionH);
                    gr.Dispose();
                    mosaic = ImageConverter.BitmapToBitmapImage(mosaicBitmap);
                    mosaic.Freeze();

                    originalBitmap.Dispose();
                    usedImages.Clear();
                }
            }
        }

        private Bitmap fillSector(Bitmap sector)
        {
            Color color = ImageIndexer.getAverageColor(sector);
            sector.Dispose();
            sector = new Bitmap(sectorWidthMosaic, sectorHeightMosaic, originalBitmap.PixelFormat);

            sector.SetResolution(originalBitmap.HorizontalResolution, originalBitmap.VerticalResolution);
            var imageList = DBManager.getImages(imageSources, color, colorError);
            while (imageList.Count > 0)
            {
                Bitmap image = null;
                int i = rand.Next(0, imageList.Count - 1);
                if (usedImages.Contains(imageList[i]))
                {
                    image = usedImages.Find(x => x.path == imageList[i].path).bitmap;
                }
                else
                {
                    Bitmap tempImage = null;
                    var type = DBManager.getImageSourceType(imageList[i].path);
                    try
                    {
                        if (type == ImageSource.Type.Directory)
                        {
                            tempImage = new Bitmap(imageList[i].path);
                        }
                        else
                        {
                            BitmapImage bi = new BitmapImage(new Uri(imageList[i].path));
                            tempImage = ImageConverter.BitmapImageToBitmap(bi);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is FileNotFoundException || ex is ArgumentException)
                        {
                            imageList.RemoveAt(i);
                            continue;
                        }
                        else
                            throw;
                    }
                    image = new Bitmap(tempImage, sector.Width, sector.Height);
                    usedImages.Add(imageList[i]);
                    usedImages[usedImages.Count - 1].bitmap = image;
                    tempImage.Dispose();
                }
                Graphics g = Graphics.FromImage(sector);
                g.DrawImage(image, 0, 0, sector.Width, sector.Height);
                g.Dispose();
                break;
            }
            if (imageList.Count == 0)
            {
                Graphics g = Graphics.FromImage(sector);
                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.FillRectangle(brush, 0, 0, sector.Width, sector.Height);
                    g.Dispose();
                }
            }
            return sector;

        }

        private Bitmap getSector(int x, int y, int width, int height)
        {
            Bitmap sector = new Bitmap(width, height, originalBitmap.PixelFormat);
            sector.SetResolution(originalBitmap.HorizontalResolution, originalBitmap.VerticalResolution);
            Graphics g = Graphics.FromImage(sector);
            g.DrawImage(originalBitmap, 0, 0, new Rectangle(x, y, width, height), GraphicsUnit.Pixel);
            g.Dispose();
            return sector;
        }

        private static void sortByColorError(List<Image> imageList, Color color)
        {
            if (imageList.Count <= 1)
                return;
            imageList.Sort((img1, img2) => getColorError(img1, color).CompareTo(getColorError(img2, color)));
        }

        private static int getColorError(Image image, Color color)
        {
            int error = 0;
            error += Math.Abs(image.color.R - color.R);
            error += Math.Abs(image.color.G - color.G);
            error += Math.Abs(image.color.B - color.B);
            return error;
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
