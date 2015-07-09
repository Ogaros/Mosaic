using System;

namespace Mosaic
{
    internal sealed class ImageSource
    {
        public enum Type : int { Directory = 1, ImgurGallery = 2, ImgurAlbum = 3}
        public ImageSource(String name, String path, Type type, int imageCount)
        {
            this.name = name;
            this.path = path;
            this.type = type;
            this.imageCount = imageCount;
            this.isUsed = false;
            if (type == Type.ImgurAlbum || type == Type.ImgurGallery)
            {
                id = path.Substring(path.LastIndexOf('/'));
            }
            else
                id = "";
        }
        public ImageSource(String name, String path, Type type, int imageCount, bool isUsed) : this(name, path, type, imageCount)
        {
            this.isUsed = isUsed;
        }
        public String name { get; set; }
        public String path { get; set; }
        public String id { get; private set; }
        public int imageCount { get; set; }
        public Type type { get; set; }
        public bool isUsed { get; set; }        
    }    

}
