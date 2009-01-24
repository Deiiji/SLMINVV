using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenJPEGNet;
namespace SLMIV
{
    public class ImageCache
    {
        private Dictionary<LLUUID, System.Drawing.Image> cache = new Dictionary<LLUUID, System.Drawing.Image>();

        public ImageCache()
        {

        }

        public bool ContainsImage(LLUUID imageID)
        {
            return cache.ContainsKey(imageID);
        }

        public void AddImage(LLUUID imageID, System.Drawing.Image image)
        {
            cache.Add(imageID, image);
        }

        public void RemoveImage(LLUUID imageID)
        {
            cache.Remove(imageID);
        }

        public System.Drawing.Image GetImage(LLUUID imageID)
        {
            return cache[imageID];
        }
    }

    public static class ImageHelper
    {
        public static System.Drawing.Image Decode(byte[] j2cdata)
        {
            return OpenJPEG.DecodeToImage(j2cdata);
        }
    }
}
