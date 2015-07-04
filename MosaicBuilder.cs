using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
        public int progress { get{return _progress;} set{_progress = value;} }
        private volatile int _progress;
        public BitmapImage original { get; private set; }
        public BitmapImage mosaic { get; private set; }
        private Bitmap originalBitmap;
        private Bitmap mosaicCanvas;
        private List<Image> usedImages;
        private List<ImageSource> imageSources;
        private int sectorWidthMosaic;
        private int sectorHeightMosaic;
        private const int colorError = 15; // error for the average color. separate for each of the primary colors
        private Random rand = new Random();
        public ErrorType errorStatus { get; private set; }
        private SemaphoreSlim semaphore;
        private int threadCount;

        public MosaicBuilder() 
        {
            threadCount = Environment.ProcessorCount;
            semaphore = new SemaphoreSlim(threadCount, threadCount);
        }

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

        public async void buildMosaic(int resolutionW, int resolutionH, int horSectors, int verSectors, List<ImageSource> imageSources)
        {
            errorStatus = ErrorType.NoErrors;
            this.imageSources = imageSources;
            progress = 0;
            // this list holds images that was used already to avoid reopening(or redownloading) and resizing them again
            usedImages = new List<Image>();
            // calculate size of the sectors that will be red from the original image with given parameters 
            // 1680x1050 image with 100 horizontal and 100 vertical sectors will amount to 16x10 sector size
            int sectorWidthOriginal = originalBitmap.Width / horSectors;
            int sectorHeightOriginal = originalBitmap.Height / verSectors;
            if (sectorWidthOriginal == 0 || sectorHeightOriginal == 0)
            {
                errorStatus = ErrorType.TooManySectors;
                return;
            }
            // adjust the number of sectors to accomodate for the lost fractional part during sector size calculation 
            // 1680x1050 image with 100 horizontal and 100 vertical sectors lost 80 and 50 pixels that would amount to 5 extra horizontal and vertical sectors
            // this is done to avoid obvious image cropping
            horSectors = originalBitmap.Width / sectorWidthOriginal;
            verSectors = originalBitmap.Height / sectorHeightOriginal;
            // calculate size of sectors that will be placed on the mosaic canvas
            // sector sizez are rounded up to avoid black bars on the mosaic if original sector size can't fill the whole canvas
            sectorWidthMosaic = (int)Math.Ceiling((double)resolutionW / (double)horSectors);
            sectorHeightMosaic = (int)Math.Ceiling((double)resolutionH / (double)verSectors);
            if (sectorWidthMosaic == 0)
                sectorWidthMosaic = 1;
            if (sectorHeightMosaic == 0)
                sectorHeightMosaic = 1;
            using (mosaicCanvas = new Bitmap(horSectors * sectorWidthMosaic, verSectors * sectorHeightMosaic, originalBitmap.PixelFormat))
            {
                mosaicCanvas.SetResolution(originalBitmap.HorizontalResolution, originalBitmap.VerticalResolution);

                for (int xOrig = 0, xMos = 0, horSecCount = 0; horSecCount < horSectors; xOrig += sectorWidthOriginal, xMos += sectorWidthMosaic, horSecCount++)
                {
                    for (int yOrig = 0, yMos = 0, verSecCount = 0; verSecCount < verSectors; yOrig += sectorHeightOriginal, yMos += sectorHeightMosaic, verSecCount++)
                    {
                        int _xOrig = xOrig, _xMos = xMos, _yOrig = yOrig, _yMos = yMos;
                        Task.Run(() => replaceSector(_xOrig, _xMos, _yOrig, _yMos, sectorWidthOriginal, sectorHeightOriginal));
                        /*
                        Bitmap sector = getSector(xOrig, yOrig, sectorWidthOriginal, sectorHeightOriginal);

                        fillSector(ref sector);

                        Graphics g = Graphics.FromImage(mosaicCanvas);
                        g.DrawImage(sector, xMos, yMos);
                        g.Dispose();

                        ++_progress;
                        OnPropertyChanged("progress");
                        sector.Dispose();
                         */
                    }
                }
                while(_progress < horSectors * verSectors)
                {
                    Thread.Sleep(500);
                }
                using (Bitmap mosaicBitmap = new Bitmap(resolutionW, resolutionH, originalBitmap.PixelFormat))
                {
                    mosaicBitmap.SetResolution(originalBitmap.HorizontalResolution, originalBitmap.VerticalResolution);
                    Graphics gr = Graphics.FromImage(mosaicBitmap);
                    gr.DrawImage(mosaicCanvas, 0, 0, resolutionW, resolutionH);
                    gr.Dispose();
                    mosaic = ImageConverter.BitmapToBitmapImage(mosaicBitmap);
                    mosaic.Freeze();

                    originalBitmap.Dispose();
                    usedImages.Clear();
                }
            }
        }
        
        private void replaceSector(int xOrig, int xMos, int yOrig, int yMos, int sectorWidth, int sectorHeight)
        {
            semaphore.Wait();
            Bitmap sector = getSector(xOrig, yOrig, sectorWidth, sectorHeight);
            fillSector(ref sector);
            lock (mosaicCanvas)
            {
                Graphics g = Graphics.FromImage(mosaicCanvas);
                g.DrawImage(sector, xMos, yMos);
                g.Dispose();
            }
            ++_progress;
            OnPropertyChanged("progress");
            sector.Dispose();
            semaphore.Release();
        }

        private void fillSector(ref Bitmap sector)
        {
            Color color = ImageIndexer.getAverageColor(sector);
            sector.Dispose();
            lock (originalBitmap)
            {
                sector = new Bitmap(sectorWidthMosaic, sectorHeightMosaic, originalBitmap.PixelFormat);

                sector.SetResolution(originalBitmap.HorizontalResolution, originalBitmap.VerticalResolution);
            }
            var imageList = DBManager.getImages(imageSources, color, colorError);
            while (imageList.Count > 0)
            {
                Bitmap image = null;
                int i = rand.Next(0, imageList.Count - 1);
                bool isInUsedImages;
                lock(usedImages)
                {
                    isInUsedImages = usedImages.Contains(imageList[i]);
                }
                if (isInUsedImages)
                {
                    lock (usedImages)
                    {
                        image = (Bitmap)usedImages.Find(x => x.path == imageList[i].path).bitmap.Clone();
                    }
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
                    lock (usedImages)
                    {
                        usedImages.Add(imageList[i]);
                        usedImages[usedImages.Count - 1].bitmap = (Bitmap)image.Clone();
                    }
                    tempImage.Dispose();
                }
                Graphics g = Graphics.FromImage(sector);
                g.DrawImage(image, 0, 0, sector.Width, sector.Height);
                g.Dispose();
                image.Dispose();
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
        }

        private Bitmap getSector(int x, int y, int width, int height)
        {
            Bitmap sector;
            lock (originalBitmap)
            {
                sector = new Bitmap(width, height, originalBitmap.PixelFormat);
                sector.SetResolution(originalBitmap.HorizontalResolution, originalBitmap.VerticalResolution);
                Graphics g = Graphics.FromImage(sector);
                g.DrawImage(originalBitmap, 0, 0, new Rectangle(x, y, width, height), GraphicsUnit.Pixel);
                g.Dispose();                
            }            
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
