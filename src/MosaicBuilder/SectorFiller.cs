using System;
using System.Collections.Generic;
using System.Drawing;

namespace Mosaic.Builder
{
    class SectorFiller : IDisposable
    {
        private List<ImageSource> _imageSources;
        private Dictionary<Image, Bitmap> _usedSectors; // this list holds sectors that was used already to avoid reopening(or redownloading) them.
        private int _colorError;
        private Random _rand; 

        public SectorFiller(List<ImageSource> imageSources, int colorError)
        {
            _imageSources = imageSources;
            _colorError = colorError;
            _usedSectors = new Dictionary<Image, Bitmap>();
            _rand = new Random();
        }

        public void FillSector(Color color, ref Bitmap mosaicSector)
        {
            var imageList = DBManager.GetImages(_imageSources, color, _colorError);
            while (imageList.Count > 0)
            {
                int i = _rand.Next(0, imageList.Count - 1);
                if (_usedSectors.ContainsKey(imageList[i]))
                {
                    mosaicSector.Dispose();
                    lock (_usedSectors)
                    {
                        mosaicSector = (Bitmap)_usedSectors[imageList[i]].Clone();
                    }
                    return;
                }
                else
                {
                    using (Bitmap image = ImageLoader.LoadImageBitmap(imageList[i]))
                    {
                        if (image == null)
                        {
                            imageList.RemoveAt(i);
                            continue;
                        }
                        using (Graphics g = Graphics.FromImage(mosaicSector))
                        {
                            g.DrawImage(image, 0, 0, mosaicSector.Width, mosaicSector.Height);
                        }
                        if (_usedSectors.ContainsKey(imageList[i]) == false)
                        {
                            lock (_usedSectors)
                            {
                                _usedSectors[imageList[i]] = (Bitmap)mosaicSector.Clone();
                            }
                        }
                    }                    
                    return;
                }
            }
            // if no matching images was found, use sector filled with random image            
            if (FillWithRandomImage(ref mosaicSector))
                color = Color.FromArgb(128, color);
            using (Graphics g = Graphics.FromImage(mosaicSector))
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillRectangle(brush, 0, 0, mosaicSector.Width, mosaicSector.Height);
            }
        }

        private bool FillWithRandomImage(ref Bitmap mosaicSector)
        {
            Image image = DBManager.GetRandomImage(_imageSources);
            if (image == null)
                return false;
            if (_usedSectors.ContainsKey(image))
            {
                mosaicSector.Dispose();
                lock (_usedSectors)
                {
                    mosaicSector = (Bitmap)_usedSectors[image].Clone();
                    return true;
                }
            }
            using (Bitmap bitmap = ImageLoader.LoadImageBitmap(image))
            {
                if (bitmap == null)
                {
                    return FillWithRandomImage(ref mosaicSector);
                }
                using (Graphics g = Graphics.FromImage(mosaicSector))
                {
                    g.DrawImage(bitmap, 0, 0, mosaicSector.Width, mosaicSector.Height);
                }
                if (_usedSectors.ContainsKey(image) == false)
                {
                    lock (_usedSectors)
                    {
                        _usedSectors[image] = (Bitmap)mosaicSector.Clone();
                    }
                }
            }
            return true;
        }

        public void Dispose()
        {
            if (_usedSectors != null)
            {
                foreach (var bitmap in _usedSectors.Values)
                {
                    bitmap.Dispose();                    
                }
                _usedSectors.Clear();
            }
        }
    }
}
