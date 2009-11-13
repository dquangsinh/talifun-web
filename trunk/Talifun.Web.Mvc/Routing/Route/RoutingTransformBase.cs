using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;
using System.Web.Caching;

namespace Talifun.Web.Mvc
{
    internal abstract class RoutingTransformBase
    {
        protected const string DefaultDocument = "default.aspx";
        private const string RefreshCacheKey = "RefreshCacheKey";
        private static readonly string[] CacheDependancies = new string[] { RefreshCacheKey };
        private static readonly string[] FileDependancies = null;

        private static readonly TimeSpan SlidingExpiration = new TimeSpan(0, 30, 0);

        static RoutingTransformBase()
        {
            RefreshCache();
        }

        /// <summary>
        /// Rewrite url
        /// </summary>
        /// <param name="filePath">A list of all directories making up the path of the url</param>
        /// <param name="fileName">The file of the url</param>
        /// <param name="fileExtension">The file extension of the file in the url</param>
        /// <param name="queryString">Querystring of the url</param>
        /// <param name="bookMark">Bookmark of the url</param>
        /// <returns>The string value of the rewritten url</returns>
        protected abstract string TransformUrl(List<string> filePath, string fileName, string fileExtension, NameValueCollection queryString, string bookMark);

        /// <summary>
        /// Reverse rewriting of url
        /// </summary>
        /// <param name="filePath">A list of all directories making up the path of the url</param>
        /// <param name="fileName">The file of the url</param>
        /// <param name="fileExtension">The file extension of the file in the url</param>
        /// <param name="queryString">Querystring of the url</param>
        /// <param name="bookMark">Bookmark of the url</param>
        /// <returns>The string value of the url before being rewritten</returns>
        protected abstract string ReverseTransformUrl(List<string> filePath, string fileName, string fileExtension, NameValueCollection queryString, string bookMark);

        public static void RefreshCache()
        {
            HttpRuntime.Cache.Insert(
                RefreshCacheKey,
                System.DateTime.Now,
                null,
                System.Web.Caching.Cache.NoAbsoluteExpiration,
                SlidingExpiration,
                System.Web.Caching.CacheItemPriority.High,
                null);
        }

        public string ApplyTransform(string input)
        {
            List<string> filePath = null;
            string fileName = null;
            string fileExtension = null;
            NameValueCollection queryString = null;
            string bookMark = null;

            if (!UrlHelper.TryParseUrl(input, out filePath, out fileName, out fileExtension, out queryString, out bookMark))
            {
                return input;
            }

            string cacheKey = "T" + String.Join("/", filePath.ToArray());
            if (!string.IsNullOrEmpty(cacheKey))
            {
                cacheKey = "/" + cacheKey;
            }

            cacheKey += fileName + fileExtension;
            object cachedValue = HttpRuntime.Cache.Get(cacheKey);

            if (cachedValue != null)
            {
                //return (string)cachedValue;
            }

            string transformedUrl = TransformUrl(filePath, fileName, fileExtension, queryString, bookMark);

            HttpRuntime.Cache.Insert(
                cacheKey,
                transformedUrl,
                new CacheDependency(FileDependancies, CacheDependancies),
                System.Web.Caching.Cache.NoAbsoluteExpiration,
                SlidingExpiration,
                System.Web.Caching.CacheItemPriority.Normal,
                null);

            return transformedUrl;
        }

        public string ApplyReverseTransform(string input)
        {
            List<string> filePath = null;
            string fileName = null;
            string fileExtension = null;
            NameValueCollection queryString = null;
            string bookMark = null;

            if (!UrlHelper.TryParseUrl(input, out filePath, out fileName, out fileExtension, out queryString, out bookMark))
            {
                return input;
            }

            string cacheKey = "RT" + String.Join("/", filePath.ToArray());
            if (!string.IsNullOrEmpty(cacheKey))
            {
                cacheKey = "/" + cacheKey;
            }

            cacheKey += fileName + fileExtension;
            object cachedValue = HttpRuntime.Cache.Get(cacheKey);

            if (cachedValue != null)
            {
                //return (string)cachedValue;
            }

            string transformedUrl = ReverseTransformUrl(filePath, fileName, fileExtension, queryString, bookMark);

            HttpRuntime.Cache.Insert(
                cacheKey,
                transformedUrl,
                new CacheDependency(FileDependancies, CacheDependancies),
                System.Web.Caching.Cache.NoAbsoluteExpiration,
                SlidingExpiration,
                System.Web.Caching.CacheItemPriority.Normal,
                null);

            return transformedUrl;
        }
    }
}
