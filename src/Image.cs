using System;
using System.Drawing;

namespace Mosaic
{
    internal class Image : IEquatable<Image>
    {
        public Image(String path, Color color, String hashcode)
        {
            this.path = path;
            this.color = color;
            this.hashcode = hashcode;
        }
        public bool Equals(Image other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return other.path == this.path;
        }
        public override int GetHashCode()
        {
            return path.GetHashCode();
        }
        public String path;
        public Color color;
        public String hashcode;
    }
}
