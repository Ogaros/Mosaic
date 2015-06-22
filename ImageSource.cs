using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mosaic
{
    public class ImageSource
    {
        public enum Type : int { Directory = 1, ImgurGallery = 2, ImgurAlbum = 3}
        public ImageSource(String name, String path, Type type, int imageCount, bool isUsed = false)
        {
            this.name = name;
            this.path = path;
            this.type = type;
            this.imageCount = imageCount;
            this.isUsed = isUsed;
        }
        public String name { get; set; }
        public String path { get; set; }
        public int imageCount { get; set; }
        public Type type { get; set; }
        public bool isUsed { get; set; }
    }    

}
