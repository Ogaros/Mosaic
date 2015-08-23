using Mosaic.Builder;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Mosaic
{  
    class MosaicBuilder : INotifyPropertyChanged, IDisposable
    {
        private const int _colorError = 15; // error for the average color. separate for each of the primary colors

        public event PropertyChangedEventHandler PropertyChanged;
        public int Progress { get { return _progress; } }
        public int SectorsCount { get; private set; }
        public BitmapImage Original { get; private set; }
        public BitmapImage Mosaic { get; private set; }
        public ErrorType ErrorStatus { get; private set; }
        
        private int _progress;
        private int _sectorWidthMosaic, _sectorWidthOriginal;
        private int _sectorHeightMosaic, _sectorHeightOriginal;
        private int _threadCount;     
        private float _verticalDpi, _horizontalDpi;
        private System.Drawing.Imaging.PixelFormat _pixelFormat;
        private Bitmap _originalBitmap;
        private Bitmap _mosaicBitmap;
        private BitmapPool _mosaicSectorPool; // holds premade sectors to use with original image
        private SemaphoreSlim _semaphore;
        private SectorFiller _sectorFiller;

        public void Dispose()
        {
            if (_semaphore != null)
                _semaphore.Dispose();
            if (_mosaicBitmap != null)
                _mosaicBitmap.Dispose();
            if (_mosaicSectorPool != null)
                _mosaicSectorPool.Dispose();
            if (_sectorFiller != null)
                _sectorFiller.Dispose();
        }

        public MosaicBuilder() 
        {
            SectorsCount = 1; // To make progress bar start empty
            _threadCount = Environment.ProcessorCount;
            _semaphore = new SemaphoreSlim(_threadCount, _threadCount);
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
            _originalBitmap = (Bitmap)bitmap.Clone();
            Original = bitmap.ToBitmapImage();
        }

        public void SetImage(BitmapImage bitmapImage)
        {
            _originalBitmap = bitmapImage.ToBitmap();
            Original = bitmapImage.CloneCurrentValue();
        }

        /// <summary>
        /// <para>Sets up vector sizes and adjusts amount of vectors in mosaic.</para>
        /// <para>Returns false if error occured and error status was changed.</para>
        /// </summary>
        private bool setupSectorParameters(int width, int height, ref int horCount, ref int verCount)
        {
            _verticalDpi = _originalBitmap.VerticalResolution;
            _horizontalDpi = _originalBitmap.HorizontalResolution;
            _pixelFormat = _originalBitmap.PixelFormat;

            // calculate size of the sectors that will be red from the original image with given parameters 
            // 1680x1050 image with 100 horizontal and 100 vertical sectors will amount to 16x10 sector size
            _sectorWidthOriginal = _originalBitmap.Width / horCount;
            _sectorHeightOriginal = _originalBitmap.Height / verCount;
            if (_sectorWidthOriginal == 0 || _sectorHeightOriginal == 0)
            {
                ErrorStatus = ErrorType.TooManySectors;
                return false;
            }

            // adjust the number of sectors to accomodate for the lost fractional part during sector size calculation 
            // 1680x1050 image with 100 horizontal and 100 vertical sectors lost 80 and 50 pixels that would amount to 5 extra horizontal and vertical sectors
            // this is done to avoid obvious image cropping
            horCount = _originalBitmap.Width / _sectorWidthOriginal;
            verCount = _originalBitmap.Height / _sectorHeightOriginal;
            SectorsCount = horCount * verCount;
            OnPropertyChanged("SectorsCount");           
 
            // calculate size of sectors that will be placed on the mosaic canvas
            // sector sizez are rounded up to avoid black bars on the mosaic if original sector size can't fill the whole canvas
            _sectorWidthMosaic = (int)Math.Ceiling((double)width / (double)horCount);
            _sectorHeightMosaic = (int)Math.Ceiling((double)height / (double)verCount);
            if (_sectorWidthMosaic == 0)
                _sectorWidthMosaic = 1;
            if (_sectorHeightMosaic == 0)
                _sectorHeightMosaic = 1;

            return true;
        }

        /// <summary>
        /// <para>Builds mosaic of required size with required amount of sectors using images from the list of sources.</para>
        /// <para>Result is put in the Mosaic parameter if the build process succeeds.</para>
        /// <para>If mosaic is successfully constructed, ErrorStatus is NoErrors.</para>
        /// </summary>
        public void BuildMosaic(int width, int height, int horCount, int verCount, List<ImageSource> imageSources)
        {
            ErrorStatus = ErrorType.NoErrors;
            _progress = 0;     
                
            if (!setupSectorParameters(width, height, ref horCount, ref verCount))
                return;
            
            using (_sectorFiller = new SectorFiller(imageSources, _colorError))
            using (_mosaicSectorPool = new BitmapPool(_threadCount, _sectorWidthMosaic, _sectorHeightMosaic, _horizontalDpi, _verticalDpi, _pixelFormat))
            using (_mosaicBitmap = new Bitmap(horCount * _sectorWidthMosaic, verCount * _sectorHeightMosaic, _pixelFormat))
            {
                _mosaicBitmap.SetResolution(_horizontalDpi, _verticalDpi);

                for (int xOrig = 0, xMos = 0, horSecCount = 0; horSecCount < horCount; xOrig += _sectorWidthOriginal, xMos += _sectorWidthMosaic, horSecCount++)
                {
                    for (int yOrig = 0, yMos = 0, verSecCount = 0; verSecCount < verCount; yOrig += _sectorHeightOriginal, yMos += _sectorHeightMosaic, verSecCount++)
                    {
                        _semaphore.Wait();
                        int _xOrig = xOrig, _xMos = xMos, _yOrig = yOrig, _yMos = yMos;
                        Task.Run(() => BuildSector(_xOrig, _xMos, _yOrig, _yMos));
                    }
                }

                while (_progress < SectorsCount)
                {
                    Thread.Sleep(500);
                }

                // Fit resulting bitmap into the desired resolution
                using (Bitmap mosaicBitmap = new Bitmap(width, height, _pixelFormat))
                {
                    mosaicBitmap.SetResolution(_horizontalDpi, _verticalDpi);
                    using (Graphics gr = Graphics.FromImage(mosaicBitmap))
                    {
                        gr.DrawImage(_mosaicBitmap, 0, 0, width, height);
                    }                    
                    Mosaic = mosaicBitmap.ToBitmapImage();
                    Mosaic.Freeze();                    
                }
            }
        }

        private void BuildSector(int xOrig, int xMos, int yOrig, int yMos)
        {
            // Get sector's color
            Color color;
            lock(_originalBitmap)
            {                
                color = _originalBitmap.GetAverageColor(xOrig, yOrig, _sectorWidthOriginal, _sectorHeightOriginal);
            }

            // Fill sector with appropriate image
            Bitmap mosaicSector = _mosaicSectorPool.GetBitmap();
            _sectorFiller.FillSector(color, ref mosaicSector);

            // Draw sector on mosaic
            lock (_mosaicBitmap)
            {
                DrawSector(mosaicSector, xMos, yMos);
            }
            _mosaicSectorPool.ReturnBitmap(mosaicSector);

            // Increment progress
            Interlocked.Increment(ref _progress);
            OnPropertyChanged("Progress");
            
            _semaphore.Release();
        }
        
        private void DrawSector(Bitmap sector, int x, int y)
        {
            using (Graphics g = Graphics.FromImage(_mosaicBitmap))
            {
                g.DrawImage(sector, x, y);
            }
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
