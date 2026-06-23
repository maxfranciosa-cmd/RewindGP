using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace AMS2ChEd.Extensions
{
    public static class PictureUrlLoaderExtension
    {
        public static bool LoadPhoto(this System.Windows.Controls.Image imageControl, string photoUrl, UIElement placeholder = null)
        {
            BitmapImage bitmapImage;
            var loaded = TryLoadBitmap(photoUrl, out bitmapImage);
            imageControl.Source = bitmapImage;

            if (placeholder != null)
            {
                placeholder.Visibility = loaded ? Visibility.Collapsed : Visibility.Visible;
            }

            return loaded;
        }

        private static string ResolvePhotoPath(this string photoUrl)
        {
            if (string.IsNullOrEmpty(photoUrl) || Uri.IsWellFormedUriString(photoUrl, UriKind.Absolute))
                return photoUrl;

            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, photoUrl));
        }

        public static BitmapImage LoadBitmap(string photoUrl)
        {
            if (!string.IsNullOrEmpty(photoUrl))
            {
                string resolvedPath = ResolvePhotoPath(photoUrl);

                if (File.Exists(resolvedPath))
                {
                    return LoadBitmap(new Uri(resolvedPath, UriKind.Absolute));
                }
                else if (Uri.IsWellFormedUriString(photoUrl, UriKind.Absolute))
                {
                    return LoadBitmap(new Uri(photoUrl, UriKind.Absolute));
                }
            }
            return null;
        }

        public static bool TryLoadBitmap(string photoUrl,out BitmapImage bitmap)
        {
            bitmap = null;
            try
            {
                if (!string.IsNullOrEmpty(photoUrl))
                {
                    string resolvedPath = ResolvePhotoPath(photoUrl);

                    if (File.Exists(resolvedPath))
                    {
                        bitmap = LoadBitmap(new Uri(resolvedPath, UriKind.Absolute));
                        return true;
                    }
                    else if (Uri.IsWellFormedUriString(photoUrl, UriKind.Absolute))
                    {
                        bitmap = LoadBitmap(new Uri(photoUrl, UriKind.Absolute));
                        return true;
                    }  
                }
                return false;
            }
            catch { return false; }
        }

        private static BitmapImage LoadBitmap(Uri uri)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            return bitmap;
        }
    }
}
