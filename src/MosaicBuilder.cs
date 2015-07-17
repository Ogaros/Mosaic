using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Collections.Concurrent;

namespace Mosaic
{  
    class MosaicBuilder : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public int Progress { get { return progress; } }
        public int SectorsCount { get; private set; }
        public BitmapImage Original { get; private set; }
        public BitmapImage Mosaic { get; private set; }
        public ErrorType ErrorStatus { get; private set; }

        private volatile int progress;
        private Bitmap originalBitmap;
        private Bitmap mosaicCanvas;
        private ConcurrentBag<Bitmap> bitmapPool; // holds premade sectors
        private Dictionary<Image, Bitmap> usedImages; // this list holds images that was used already to avoid reopening(or redownloading) and resizing them again
        private List<ImageSource> imageSources;
        private int sectorWidthMosaic, sectorWidthOriginal;
        private int sectorHeightMosaic, sectorHeightOriginal;
        private const int colorError = 15; // error for the average color. separate for each of the primary colors
        private Random rand = new Random();        
        private SemaphoreSlim semaphore;
        private int threadCount;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (semaphore != null)
                    semaphore.Dispose();
                if (originalBitmap != null)
                    originalBitmap.Dispose();
                if (mosaicCanvas != null)
                    mosaicCanvas.Dispose();
            }
        }

        public MosaicBuilder() 
        {
            SectorsCount = 1; // To make progress bar start empty
            threadCount = Environment.ProcessorCount;
            semaphore = new SemaphoreSlim(threadCount, threadCount);
            Mosaic = null;
            Original = null;
        }

        public MosaicBuilder(Bitmap bitmap) : this()
        {
            SetImage(bitmap);
        }
        public MosaicBuilder(BitmapImage bitmapImage) : this()
        {
            SetImage(bitmapImage);
        }

        public void SetImage(Bitmap bitmap)
        {
            originalBitmap = (Bitmap)bitmap.Clone();
            Original = ImageConverter.BitmapToBitmapImage(bitmap);
        }
        public void SetImage(BitmapImage bitmapImage)
        {
            originalBitmap = ImageConverter.BitmapImageToBitmap(bitmapImage);
            Original = bitmapImage.CloneCurrentValue();
        }

        /// <summary>
        /// <para>Sets up vector sizes and adjusts amount of vectors in mosaic.</para>
        /// <para>Returns false if error occured and error status was changed.</para>
        /// </summary>
        private bool setupSectorParameters(int resolutionW, int resolutionH, ref int horSectors, ref int verSectors)
        {
            // calculate size of the sectors that will be red from the original image with given parameters 
            // 1680x1050 image with 100 horizontal and 100 vertical sectors will amount to 16x10 sector size
            sectorWidthOriginal = originalBitmap.Width / horSectors;
            sectorHeightOriginal = originalBitmap.Height / verSectors;
            if (sectorWidthOriginal == 0 || sectorHeightOriginal == 0)
            {
                ErrorStatus = ErrorType.TooManySectors;
                return false;
            }
            // adjust the number of sectors to accomodate for the lost fractional part during sector size calculation 
            // 1680x1050 image with 100 horizontal and 100 vertical sectors lost 80 and 50 pixels that would amount to 5 extra horizontal and vertical sectors
            // this is done to avoid obvious image cropping
            horSectors = originalBitmap.Width / sectorWidthOriginal;
            verSectors = originalBitmap.Height / sectorHeightOriginal;
            SectorsCount = horSectors * verSectors;
            OnPropertyChanged("SectorsCount");            
            // calculate size of sectors that will be placed on the mosaic canvas
            // sector sizez are rounded up to avoid black bars on the mosaic if original sector size can't fill the whole canvas
            sectorWidthMosaic = (int)Math.Ceiling((double)resolutionW / (double)horSectors);
            sectorHeightMosaic = (int)Math.Ceiling((double)resolutionH / (double)verSectors);
            if (sectorWidthMosaic == 0)
                sectorWidthMosaic = 1;
            if (sectorHeightMosaic == 0)
                sectorHeightMosaic = 1;
            return true;
        }

        /// <summary>
        /// <para>Builds mosaic of required size with required amount of sectors using images from the list of sources.</para>
        /// <para>Result is put in the Mosaic parameter if the build process succeeds.</para>
        /// <para>If mosaic is successfully constructed, ErrorStatus is NoErrors.</para>
        /// </summary>
        public void BuildMosaic(int resolutionW, int resolutionH, int horSectors, int verSectors, List<ImageSource> imageSources)
        {
            ErrorStatus = ErrorType.NoErrors;
            this.imageSources = imageSources;
            progress = 0;                     
            usedImages = new Dictionary<Image, Bitmap>();
            if (!setupSectorParameters(resolutionW, resolutionH, ref horSectors, ref verSectors))
                return;
            SetupBitmapPool();
            using (mosaicCanvas = new Bitmap(horSectors * sectorWidthMosaic, verSectors * sectorHeightMosaic, originalBitmap.PixelFormat))
            {
                mosaicCanvas.SetResolution(originalBitmap.HorizontalResolution, originalBitmap.VerticalResolution);
                for (int xOrig = 0, xMos = 0, horSecCount = 0; horSecCount < horSectors; xOrig += sectorWidthOriginal, xMos += sectorWidthMosaic, horSecCount++)
                {
                    for (int yOrig = 0, yMos = 0, verSecCount = 0; verSecCount < verSectors; yOrig += sectorHeightOriginal, yMos += sectorHeightMosaic, verSecCount++)
                    {
                        int _xOrig = xOrig, _xMos = xMos, _yOrig = yOrig, _yMos = yMos;
                        Task.Run(() => ReplaceSector(_xOrig, _xMos, _yOrig, _yMos, sectorWidthOriginal, sectorHeightOriginal));
                    }
                }
                while (progress < SectorsCount)
                {
                    Thread.Sleep(500);
                }
                // Fit resulting bitmap into the desired resolution
                using (Bitmap mosaicBitmap = new Bitmap(resolutionW, resolutionH, originalBitmap.PixelFormat))
                {
                    mosaicBitmap.SetResolution(originalBitmap.HorizontalResolution, originalBitmap.VerticalResolution);
                    Graphics gr = Graphics.FromImage(mosaicBitmap);
                    gr.DrawImage(mosaicCanvas, 0, 0, resolutionW, resolutionH);
                    gr.Dispose();
                    Mosaic = ImageConverter.BitmapToBitmapImage(mosaicBitmap);
                    Mosaic.Freeze();                    
                }
            }
            originalBitmap.Dispose();
            originalBitmap = null;
            usedImages.Clear();
            ClearBitmapPool();
            GC.Collect();
        }
        
        /// <summary>
        /// Gets a sector from the original bitmap and puts the matching image on the mosaic bitmap.
        /// </summary>
        private void ReplaceSector(int xOrig, int xMos, int yOrig, int yMos, int sectorWidth, int sectorHeight)
        {
            semaphore.Wait();
            Bitmap sector = GetSector(xOrig, yOrig, sectorWidth, sectorHeight);
            FillSector(ref sector);
            lock (mosaicCanvas)
            {
                using (Graphics g = Graphics.FromImage(mosaicCanvas))
                {
                    g.DrawImage(sector, xMos, yMos);
                }                
            }
            Interlocked.Increment(ref progress);
            OnPropertyChanged("Progress");
            sector.Dispose();
            semaphore.Release();
        }

        /// <summary>
        /// <para>Fills sector with a matching image.</para>
        /// <para>If no matching image was found, paints a random image with the required color.</para>
        /// <para>If no images are availible, fills the sector with the required color.</para>
        /// </summary>
        private void FillSector(ref Bitmap sector)
        {
            Color color = sector.GetAverageColor();
            sector.Dispose();            
            var imageList = DBManager.GetImages(imageSources, color, colorError);
            while (imageList.Count > 0)
            {
                
                int i = rand.Next(0, imageList.Count - 1);
                if (usedImages.ContainsKey(imageList[i]))
                {
                    lock(usedImages)
                    {
                        sector = (Bitmap)usedImages[imageList[i]].Clone();
                    }
                    return;
                }
                else
                {
                    Bitmap image = null;
                    image = LoadImage(imageList[i]);
                    if(image == null)
                    {
                        imageList.RemoveAt(i);
                        continue;
                    }
                    bitmapPool.TryTake(out sector);
                    using (Graphics g = Graphics.FromImage(sector))
                    {
                        g.DrawImage(image, 0, 0, sector.Width, sector.Height);
                    }
                    lock(usedImages)
                    {
                        usedImages[imageList[i]] = (Bitmap)sector.Clone();
                    }
                    image.Dispose();
                    return;
                }
            }
            // if no matching images was found, use sector filled with random image
            Bitmap temp = GetRandomFilledSector();
            if (temp != null)
            {
                sector.Dispose();
                sector = temp;
                color = Color.FromArgb(128, color);
            }
            else
            {
                bitmapPool.TryTake(out sector);
            }
            using (Graphics g = Graphics.FromImage(sector))
            {
                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.FillRectangle(brush, 0, 0, sector.Width, sector.Height);
                }
            }
        }

        /// <summary>
        /// Returns the sector from the original bitmap.
        /// </summary>
        private Bitmap GetSector(int x, int y, int width, int height)
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

        /// <summary>
        /// Returns the bitmap that was loaded from the image URI
        /// </summary>
        private Bitmap LoadImage(Image image)
        {
            Bitmap bitmap = null;
            var type = DBManager.GetImageSourceType(image);
            try
            {
                if (type == ImageSource.Type.Directory)
                {
                    bitmap = new Bitmap(image.path);
                }
                else
                {
                    BitmapImage bi = new BitmapImage(new Uri(image.path));
                    bitmap = ImageConverter.BitmapImageToBitmap(bi);
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
        /// <para>Returns sector filled with random image.</para>
        /// <para>Returns null if no images availible.</para>
        /// </summary>
        private Bitmap GetRandomFilledSector()
        {
            Image image = DBManager.GetRandomImage(imageSources);
            if (image == null)
                return null;            
            if (usedImages.ContainsKey(image))
            {
                lock(usedImages)
                {
                    return (Bitmap)usedImages[image].Clone();
                }
            }
            Bitmap bitmap = null;
            bitmap = LoadImage(image);
            if(bitmap == null)
            {
                return GetRandomFilledSector(); 
            }            
            Bitmap sector;
            lock (originalBitmap)
            {
                sector = new Bitmap(sectorWidthMosaic, sectorHeightMosaic, originalBitmap.PixelFormat);
                sector.SetResolution(originalBitmap.HorizontalResolution, originalBitmap.VerticalResolution);
            }
            using (Graphics g = Graphics.FromImage(sector))
            {
                g.DrawImage(bitmap, 0, 0, sector.Width, sector.Height);
            }
            bitmap.Dispose();
            if (usedImages.ContainsKey(image) == false)
            {
                lock(usedImages)
                {
                    usedImages[image] = (Bitmap)sector.Clone();
                }
            }           
            return sector;
        }

        /// <summary>
        /// Fills BitmapPool with sectors of required size.
        /// </summary>
        private void SetupBitmapPool()
        {
            bitmapPool = new ConcurrentBag<Bitmap>();
            Bitmap b = new Bitmap(sectorWidthMosaic, sectorHeightMosaic, originalBitmap.PixelFormat);
            b.SetResolution(originalBitmap.HorizontalResolution, originalBitmap.VerticalResolution);
            for (int i = 0; i < SectorsCount; i++)
            {
                bitmapPool.Add((Bitmap)b.Clone());
            }
            b.Dispose();
        }

        /// <summary>
        /// Disposes the remaining sectors from BitmapPool.
        /// </summary>
        private void ClearBitmapPool()
        {
            Bitmap b;
            while(bitmapPool.Count > 0)
            {
                bitmapPool.TryTake(out b);
                b.Dispose();
            }
        }

        private static void SortByColorError(List<Image> imageList, Color color)
        {
            if (imageList.Count <= 1)
                return;
            imageList.Sort((img1, img2) => GetColorError(img1, color).CompareTo(GetColorError(img2, color)));
        }

        private static int GetColorError(Image image, Color color)
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
