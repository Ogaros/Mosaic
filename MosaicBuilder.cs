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
    class Image : IEquatable<Image>
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
        public Bitmap mosaicBitmap; //---------------------------------------------> Костыль.
        private Bitmap originalBitmap;
        private BitmapImage original;
        private BitmapImage mosaic;
        private List<Image> usedImages;
        private List<ImageSource> imageSources;
        private int s_WidthMosaic;
        private int s_HeightMosaic;
        private int error = 15; // error for the average color
        public ErrorType errorStatus { get; private set; }

        public MosaicBuilder(){}

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
            mosaicBitmap = (Bitmap)originalBitmap.Clone();
            original = ImageConverter.BitmapToBitmapImage(bitmap);
        }
        public void setImage(BitmapImage bitmapImage)
        {            
            originalBitmap = ImageConverter.BitmapImageToBitmap(bitmapImage);
            mosaicBitmap = (Bitmap)originalBitmap.Clone();
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

            int s_WidthOriginal = originalBitmap.Width / horSectors;                     
            int s_HeightOriginal = originalBitmap.Height / verSectors;
            if(s_WidthOriginal == 0 || s_HeightOriginal == 0)
            {
                errorStatus = ErrorType.TooManySectors;
                return;
            }
            horSectors = originalBitmap.Width / s_WidthOriginal;
            verSectors = originalBitmap.Height / s_HeightOriginal;            
            s_WidthMosaic = resolutionW > originalBitmap.Width ? (resolutionW / horSectors) + 1 : resolutionW / horSectors;
            s_HeightMosaic = resolutionH > originalBitmap.Height ? (resolutionH / verSectors) + 1 : resolutionH / verSectors;

            mosaicBitmap.Dispose();
            Bitmap mosaicBitmapTemp = new Bitmap(horSectors * s_WidthMosaic, verSectors * s_HeightMosaic, originalBitmap.PixelFormat);
            mosaicBitmapTemp.SetResolution(originalBitmap.HorizontalResolution, originalBitmap.VerticalResolution);

            for (int xOrig = 0, xMos = 0, horSecCount = 0; horSecCount < horSectors; xOrig += s_WidthOriginal, xMos += s_WidthMosaic, horSecCount++)
            {
                for (int yOrig = 0, yMos = 0, verSecCount = 0; verSecCount < verSectors; yOrig += s_HeightOriginal, yMos += s_HeightMosaic, verSecCount++)
                {
                    Bitmap sector = getSector(xOrig, yOrig, s_WidthOriginal, s_HeightOriginal);                    

                    sector = fillSector(sector);

                    Graphics g = Graphics.FromImage(mosaicBitmapTemp);
                    g.DrawImage(sector, xMos, yMos);
                    g.Dispose();

                    ++progress;
                    OnPropertyChanged("progress");
                    sector.Dispose();
                }
                
            }
            mosaicBitmap = new Bitmap(resolutionW, resolutionH, originalBitmap.PixelFormat);
            mosaicBitmap.SetResolution(originalBitmap.HorizontalResolution, originalBitmap.VerticalResolution);
            Graphics gr = Graphics.FromImage(mosaicBitmap);
            gr.DrawImage(mosaicBitmapTemp, 0, 0, resolutionW, resolutionH);
            gr.Dispose();
            mosaicBitmapTemp.Dispose();
            mosaic = ImageConverter.BitmapToBitmapImage(mosaicBitmap);
            mosaic.Freeze();

            usedImages.Clear();
        }

        private Bitmap fillSector(Bitmap sector)
        {
            Color color = ImageIndexer.getAverageColor(sector);
            sector.Dispose();
            sector = new Bitmap(s_WidthMosaic, s_HeightMosaic, originalBitmap.PixelFormat);
            sector.SetResolution(originalBitmap.HorizontalResolution, originalBitmap.VerticalResolution);
            var imageList = DBManager.getImages(imageSources, color, error);            
            if(imageList.Count == 0)
            {                
                Graphics g = Graphics.FromImage(sector);
                SolidBrush brush = new SolidBrush(color);
                g.FillRectangle(brush, 0, 0, sector.Width, sector.Height);
                g.Dispose();
                brush.Dispose();
            }
            else
            {
                int i = getClosestImageIndex(imageList, color);
                Bitmap image = null;
                
                if(usedImages.Contains(imageList[i]))
                {
                    image = usedImages.Find(x => x.path == imageList[i].path).bitmap;
                }
                else
                {
                    Bitmap tempImage;
                    var type = DBManager.getImageSourceType(imageList[i].path);
                    if(type == ImageSource.Type.Directory)
                    {
                        tempImage = new Bitmap(imageList[i].path);
                    }
                    else
                    {
                        BitmapImage bi = new BitmapImage(new Uri(imageList[i].path));
                        tempImage = ImageConverter.BitmapImageToBitmap(bi);
                    }
                    image = new Bitmap(tempImage, sector.Width, sector.Height);                    
                    usedImages.Add(imageList[i]);
                    usedImages[usedImages.Count - 1].bitmap = image;
                    tempImage.Dispose();
                }
                
                Graphics g = Graphics.FromImage(sector);
                g.DrawImage(image, 0, 0, sector.Width, sector.Height);
                g.Dispose();
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

        private int getClosestImageIndex(List<Image> imageList, Color color)
        {
            if (imageList.Count == 1)
                return 0;
            int diffRating = error * 3, index = 0;
            for(int i = 0; i < imageList.Count; i++)
            {
                int currentDiffRating = 0;
                currentDiffRating += Math.Abs(imageList[i].color.R - color.R);
                currentDiffRating += Math.Abs(imageList[i].color.G - color.G);
                currentDiffRating += Math.Abs(imageList[i].color.B - color.B);
                if(currentDiffRating < diffRating)
                {
                    diffRating = currentDiffRating;
                    index = i;
                }
            }
            return index;
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
