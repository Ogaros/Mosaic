using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;

namespace Mosaic.Builder
{
    class BitmapPool : IDisposable
    {
        private ConcurrentBag<Bitmap> _pool = new ConcurrentBag<Bitmap>();
        private int _width, _height, _size;
        private float _horDpi, _verDpi;
        private PixelFormat _pixelFormat;

        public int Count { get { return _pool.Count; } }
        public int Capacity { get { return _size; } }
 
        public BitmapPool(int size, int width, int height, float horDpi, float verDpi, PixelFormat pixelFormat)
        {
            _size = size;
            _width = width;
            _height = height;
            _horDpi = horDpi;
            _verDpi = verDpi;
            _pixelFormat = pixelFormat;
            FillPool();
        }

        public Bitmap GetBitmap()
        {
            Bitmap bitmap;
            _pool.TryTake(out bitmap);
            return bitmap;
        }

        public void ReturnBitmap(Bitmap bitmap)
        {
            if (bitmap.Width != _width || bitmap.Height != _height ||
                bitmap.HorizontalResolution != _horDpi || bitmap.VerticalResolution != _verDpi ||
                bitmap.PixelFormat != _pixelFormat)
                    throw new ArgumentException("Trying to return unrelated bitmap to the pool");
            if (_pool.Count + 1 > _size)
                    throw new ArgumentException("BitmapPool is full");

            _pool.Add(bitmap);
        }

        public void Dispose()
        {
            Bitmap bitmap;
            while(_pool.IsEmpty == false)
            {
                _pool.TryTake(out bitmap);
                bitmap.Dispose();
            }
        }

        private void FillPool()
        {
            using (Bitmap bitmap = new Bitmap(_width, _height, _pixelFormat))
            {
                bitmap.SetResolution(_horDpi, _verDpi);
                for (int i = 0; i < _size; i++)
                {
                    _pool.Add((Bitmap)bitmap.Clone());
                }
            }
        }
    }
}
