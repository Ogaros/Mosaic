﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mosaic
{
    internal class ImgurGallery
    {
        public String title { get; set; }
        public List<ImgurImage> images { get; set; }
        public int images_count { get; set; }
    }

    internal class ImgurImage
    {
        public String link { get; set; }
    }
}
