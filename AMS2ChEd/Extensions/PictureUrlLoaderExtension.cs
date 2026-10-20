using AMS2ChEd.Business.Storage;
using AMS2ChEd.Business.Storage.Concrete;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media.Imaging;

namespace AMS2ChEd.Extensions
{
    public static class PictureUrlLoaderExtension
    {
        // pictures linked by URL are downloaded once and read from disk afterwards (RemoteImageCache)
        private static readonly RemoteImageCache ImageCache = new(AppPaths.ImageCacheFolder, CreateHttpClient());

        // decoded pictures of this session, by URL, so a photo shown in several windows is decoded
        // once; only pictures read from the disk cache (frozen, so they can be shared)
        private static readonly ConcurrentDictionary<string, BitmapImage> SessionBitmaps = new();
        private const int MaxSessionBitmaps = 300;

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
                    return LoadRemoteBitmap(photoUrl);
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
                        bitmap = LoadRemoteBitmap(photoUrl);
                        return true;
                    }
                }
                return false;
            }
            catch { return false; }
        }

        /// <summary>
        /// A picture linked by URL: the cached copy when there is one. Otherwise it's shown straight
        /// from the URL as before, while a copy is downloaded into the cache in the background for
        /// next time - the window never waits for the cache.
        /// </summary>
        private static BitmapImage LoadRemoteBitmap(string url)
        {
            if (!RemoteImageCache.IsRemoteUrl(url))
                return LoadBitmap(new Uri(url, UriKind.Absolute));

            if (SessionBitmaps.TryGetValue(url, out var known))
                return known;

            var cachedPath = ImageCache.TryGetCachedPath(url);
            if (cachedPath != null)
            {
                try
                {
                    var bitmap = LoadBitmap(new Uri(cachedPath, UriKind.Absolute));
                    bitmap.Freeze();
                    if (SessionBitmaps.Count >= MaxSessionBitmaps)
                        SessionBitmaps.Clear();
                    SessionBitmaps[url] = bitmap;
                    return bitmap;
                }
                catch (Exception ex) when (ex is NotSupportedException or IOException or InvalidOperationException or FileFormatException or UnauthorizedAccessException)
                {
                    // a cached file that can't be decoded (not a picture after all, or damaged):
                    // drop it and fall back to the URL, which downloads it again below
                    try { File.Delete(cachedPath); } catch (Exception deleteEx) when (deleteEx is IOException or UnauthorizedAccessException) { }
                }
            }

            _ = ImageCache.DownloadAsync(url);
            return LoadBitmap(new Uri(url, UriKind.Absolute));
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

        private static HttpClient CreateHttpClient()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            // some sites refuse requests without one
            http.DefaultRequestHeaders.UserAgent.ParseAdd("RewindGP");
            return http;
        }
    }
}
