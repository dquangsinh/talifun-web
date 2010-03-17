using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Permissions;
using System.Text;
using System.Web;
using System.Web.Caching;
using Talifun.Web.StaticFile.Config;

namespace Talifun.Web.StaticFile
{
    public static class StaticFileHelper
    {
        private const int BufferSize = 32768;
        private const long MAX_FILE_SIZE_TO_SERVE = int.MaxValue;

        internal const string MULTIPART_BOUNDARY = "<q1w2e3r4t5y6u7i8o9p0>";
        internal const string MULTIPART_CONTENTTYPE = "multipart/byteranges; boundary=" + MULTIPART_BOUNDARY;

        internal const string HTTP_METHOD_GET = "GET";
        internal const string HTTP_METHOD_HEAD = "HEAD";

        internal const string HTTP_HEADER_ACCEPT_RANGES = "Accept-Ranges";
        internal const string HTTP_HEADER_ACCEPT_RANGES_BYTES = "bytes";
        internal const string HTTP_HEADER_CONTENT_TYPE = "Content-Type";
        internal const string HTTP_HEADER_CONTENT_RANGE = "Content-Range";
        internal const string HTTP_HEADER_CONTENT_LENGTH = "Content-Length";
        internal const string HTTP_HEADER_CONTENT_DISPOSITION = "Content-Disposition";
        internal const string HTTP_HEADER_ENTITY_TAG = "ETag";
        internal const string HTTP_HEADER_EXPIRES = "Expires";
        internal const string HTTP_HEADER_LAST_MODIFIED = "Last-Modified";
        internal const string HTTP_HEADER_RANGE = "Range";
        internal const string HTTP_HEADER_IF_RANGE = "If-Range";
        internal const string HTTP_HEADER_IF_MATCH = "If-Match";
        internal const string HTTP_HEADER_IF_NONE_MATCH = "If-None-Match";
        internal const string HTTP_HEADER_IF_MODIFIED_SINCE = "If-Modified-Since";
        internal const string HTTP_HEADER_IF_UNMODIFIED_SINCE = "If-Unmodified-Since";
        internal const string HTTP_HEADER_UNLESS_MODIFIED_SINCE = "Unless-Modified-Since";

        internal const uint ERROR_THE_REMOTE_HOST_CLOSED_THE_CONNECTION = 0x80072746; //WSAECONNRESET (10054)

        private static readonly Dictionary<string, FileExtensionMatch> fileExtensionMatches = null;
        private static readonly FileExtensionMatch fileExtensionMatchDefault = null;

        internal static WebServerType WebServerType { get; set; }

        static StaticFileHelper()
        {
            WebServerType = CurrentStaticFileHandlerConfiguration.Current.WebServerType;
            
            fileExtensionMatches = new Dictionary<string, FileExtensionMatch>();

            var fileExtensionElements = CurrentStaticFileHandlerConfiguration.Current.FileExtensions;
            foreach (FileExtensionElement fileExtension in fileExtensionElements)
            {
                var extensions = fileExtension.Extension.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var extension in extensions)
                {
                    var key = extension.Trim().ToLower();
                    if (!key.StartsWith("."))
                    {
                        key = "." + key;
                    }

                    var fileExtensionElement = new FileExtensionMatch
                    {
                        Compress = fileExtension.Compress,
                        Extension = fileExtension.Extension,
                        MaxMemorySize = fileExtension.MaxMemorySize,
                        ServeFromMemory = fileExtension.ServeFromMemory,
                        EtagMethod = fileExtension.EtagMethod,
                        Expires = fileExtension.Expires,
                        MemorySlidingExpiration = fileExtension.MemorySlidingExpiration
                    };

                    fileExtensionMatches.Add(key, fileExtensionElement);
                }
            }

            var fileExtensionElementDefault = CurrentStaticFileHandlerConfiguration.Current.FileExtensionDefault;

            fileExtensionMatchDefault = new FileExtensionMatch
            {
                Compress = fileExtensionElementDefault.Compress,
                Extension = string.Empty,
                MaxMemorySize = fileExtensionElementDefault.MaxMemorySize,
                ServeFromMemory = fileExtensionElementDefault.ServeFromMemory,
                EtagMethod = fileExtensionElementDefault.EtagMethod,
                Expires = fileExtensionElementDefault.Expires,
                MemorySlidingExpiration = fileExtensionElementDefault.MemorySlidingExpiration
            };
        }

        /// <summary>
        /// Return the http worker request for the current request.
        /// </summary>
        /// <remarks>
        /// This is needed for when we manually create an HttpContext.
        /// </remarks>
        /// <param name="request"></param>
        /// <returns></returns>
        [ReflectionPermission(SecurityAction.Assert, RestrictedMemberAccess = true)]
        public static HttpWorkerRequest GetWorkerRequestViaReflection(HttpRequest request)
        {
            const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;

            // In Mono, the field has a different name.
            var wrField = request.GetType().GetField("_wr", bindingFlags) ?? request.GetType().GetField("worker_request", bindingFlags);

            if (wrField == null) return null;

            return (HttpWorkerRequest)wrField.GetValue(request);
        }

        /// <summary>
        /// Detect the web server being used to serve requests
        /// </summary>
        /// <param name="context">Http context</param>
        [SecurityPermission(SecurityAction.Assert, UnmanagedCode=true)]
        public static void DetectWebServerType(HttpContext context)
        {
            var provider = (IServiceProvider)context;
            var worker = (HttpWorkerRequest)provider.GetService(typeof(HttpWorkerRequest)) ?? GetWorkerRequestViaReflection(context.Request);

            if (worker != null)
            {
                var workerType = worker.GetType();
                if (workerType != null)
                {
                    switch (workerType.FullName)
                    {
                        case "System.Web.Hosting.ISAPIWorkerRequest":
                            //IIS 7 in Classic mode gets lumped in here too
                            WebServerType = WebServerType.IIS6;
                            break;
                        case "Microsoft.VisualStudio.WebHost.Request":
                        case "Cassini.Request":
                            WebServerType = WebServerType.Cassini;
                            break;
                        case "System.Web.Hosting.IIS7WorkerRequest":
                            WebServerType = WebServerType.IIS7;
                            break;
                        default:
                            WebServerType = WebServerType.Unknown;
                            break;
                    }

                    return;
                }
            }

            WebServerType = WebServerType.Unknown;
        }

        /// <summary>
        /// Append header to response
        /// </summary>
        /// <remarks>
        /// Seems like appendheader only works with IIS 7
        /// </remarks>
        /// <param name="response"></param>
        /// <param name="headerName"></param>
        /// <param name="headerValue"></param>
        public static void AppendHeader(HttpResponse response, string headerName, string headerValue)
        {
            switch (WebServerType)
            {
                case WebServerType.IIS7:
                    response.AppendHeader(headerName, headerValue);
                    break;
                default:
                    response.AddHeader(headerName, headerValue);
                    break;
            }
        }

        public static void ProcessRequest(HttpContext context)
        {
            if (HttpContext.Current == null)
            {
                HttpContext.Current = context;
            }

            var request = context.Request;
            var response = context.Response;

            var physicalFilePath = request.PhysicalPath;
            var file = new FileInfo(physicalFilePath);

            ProcessRequest(request, response, file);
        }

        public static void ProcessRequest(HttpRequest request, HttpResponse response, FileInfo file)
        {
            if (HttpContext.Current == null)
            {
                var httpWorkerRequest = GetWorkerRequestViaReflection(request);
                HttpContext.Current = new HttpContext(httpWorkerRequest);
            }

            if (WebServerType == WebServerType.NotSet)
            {
                DetectWebServerType(HttpContext.Current);
            }

            //We don't want to use up all the servers memory keeping a copy of the file, we just want to stream file to client
            response.BufferOutput = false;
      
            try
            {

                var physicalFilePath = file.FullName;
                var fileExtension = file.Extension.ToLower(); //in case url rewriting did something smart

                if (!ValidateHttpMethod(request))
                {
                    //If we are unable to parse url send 405 Method not allowed
                    SendMethodNotAllowed(response);
                    return;
                }

                if (physicalFilePath.EndsWith(".asp", StringComparison.InvariantCultureIgnoreCase) ||
                    physicalFilePath.EndsWith(".aspx", StringComparison.InvariantCultureIgnoreCase))
                {
                    //If we are unable to parse url send 403 Path Forbidden
                    SendForbidden(response);
                    return;
                }

                var compressionType = GetCompressionMode(request);

                FileExtensionMatch fileExtensionMatch = null;
                if (!fileExtensionMatches.TryGetValue(fileExtension, out fileExtensionMatch))
                {
                    fileExtensionMatch = fileExtensionMatchDefault;
                }

                // If this is a binary file like image, then we won't compress it.
                if (!fileExtensionMatch.Compress)
                    compressionType = ResponseCompressionType.None;

                // If it is a partial request we need to get bytes of orginal entity data, we will compress the byte ranges returned
                var entityStoredWithCompressionType = compressionType;
                var isRangeRequest = IsRangeRequest(request);
                if (isRangeRequest)
                {
                    entityStoredWithCompressionType = ResponseCompressionType.None;
                }

                // If the response bytes are already cached, then deliver the bytes directly from cache
                var cacheKey = typeof (StaticFileHelper) + ":" + entityStoredWithCompressionType + ":" +
                               physicalFilePath;

                FileHandlerCacheItem fileHandlerCacheItem = null;
                var cachedValue = HttpRuntime.Cache.Get(cacheKey);
                if (cachedValue != null)
                {
                    fileHandlerCacheItem = (FileHandlerCacheItem) cachedValue;
                }
                else
                {
                    //File does not exist
                    if (!file.Exists)
                    {
                        SendNotFoundResponseHeaders(response);
                        return;
                    }

                    //File too large to send
                    if (file.Length > MAX_FILE_SIZE_TO_SERVE)
                    {
                        SendRequestedEntityIsTooLargeResponseHeaders(response);
                        return;
                    }

                    var etag = string.Empty;
                    var lastModifiedFileTime = file.LastWriteTime.ToUniversalTime();
                    //When a browser sets the If-Modified-Since field to 13-1-2010 10:30:58, another DateTime instance is created, but this one has a Ticks value of 633989754580000000
                    //But the time from the file system is accurate to a tick. So it might be 633989754586086250.
                    var lastModified = new DateTime(lastModifiedFileTime.Year, lastModifiedFileTime.Month,
                                                    lastModifiedFileTime.Day, lastModifiedFileTime.Hour,
                                                    lastModifiedFileTime.Minute, lastModifiedFileTime.Second);
                    var contentType = MimeHelper.GetMimeType(file.Extension);
                    var contentLength = file.Length;

                    //ETAG is always calculated from uncompressed entity data
                    switch (fileExtensionMatch.EtagMethod)
                    {
                        case EtagMethodType.MD5:
                            etag = HashHelper.CalculateMd5Etag(file);
                            break;
                        case EtagMethodType.LastModified:
                            etag = lastModified.ToString();
                            break;
                        default:
                            throw new Exception("Unknown etag method generation");
                    }

                    fileHandlerCacheItem = new FileHandlerCacheItem
                                               {
                                                   EntityEtag = etag,
                                                   EntityLastModified = lastModified,
                                                   ContentLength = contentLength,
                                                   EntityContentType = contentType
                                               };

                    if (fileExtensionMatch.ServeFromMemory
                        && (contentLength <= fileExtensionMatch.MaxMemorySize))
                    {
                        // When not compressed, buffer is the size of the file but when compressed, 
                        // initial buffer size is one third of the file size. Assuming, compression 
                        // will give us less than 1/3rd of the size
                        using (var memoryStream = new MemoryStream(
                            entityStoredWithCompressionType == ResponseCompressionType.None
                                ?
                                    Convert.ToInt32(file.Length)
                                :
                                    Convert.ToInt32((double) file.Length/3)))
                        {
                            ReadEntityData(entityStoredWithCompressionType, file, memoryStream);
                            var entityData = memoryStream.ToArray();
                            var entityDataLength = entityData.LongLength;

                            fileHandlerCacheItem.EntityData = entityData;
                            fileHandlerCacheItem.ContentLength = entityDataLength;
                        }
                    }

                    //Put fileHandlerCacheItem into cache with 30 min sliding expiration, also if file changes then remove fileHandlerCacheItem from cache
                    HttpRuntime.Cache.Insert(
                        cacheKey,
                        fileHandlerCacheItem,
                        new CacheDependency(physicalFilePath),
                        Cache.NoAbsoluteExpiration,
                        fileExtensionMatch.MemorySlidingExpiration,
                        CacheItemPriority.BelowNormal,
                        null);
                }

                //Unable to parse request range header
                List<RangeItem> ranges = null;
                var requestRange = TryParseRequestRangeHeader(request, fileHandlerCacheItem.ContentLength, out ranges);
                if (requestRange.HasValue && !requestRange.Value)
                {
                    SendRequestedRangeNotSatisfiableResponseHeaders(response);
                    return;
                }

                //Check if cached response is valid and if it is send appropriate response headers
                var requestHandled = GenerateResponseHeaders(request, response, fileHandlerCacheItem.EntityLastModified,
                                                             fileHandlerCacheItem.EntityEtag);

                if (requestHandled)
                {
                    //Browser cache is ok so, just load from cache
                    return;
                }

                //How the entity should be cached on the client
                SetResponseCachable(response, fileHandlerCacheItem.EntityLastModified, fileHandlerCacheItem.EntityEtag,
                                    fileExtensionMatch.Expires);

                Stream entityDataStream = null;
                try
                {
                    if (fileHandlerCacheItem.EntityData != null)
                    {
                        entityDataStream = new MemoryStream(fileHandlerCacheItem.EntityData);
                    }
                    else
                    {
                        if (!file.Exists)
                        {
                            SendNotFoundResponseHeaders(response);
                            return;
                        }

                        //We are going to let the output filter do the necessary compression
                        entityStoredWithCompressionType = ResponseCompressionType.None;
                        entityDataStream = FileHelper.OpenFileStream(file, 5, FileMode.Open, FileAccess.Read,
                                                                     FileShare.Read);
                    }

                    if (response.StatusCode == (int) HttpStatusCode.PartialContent)
                    {
                        //Data is in uncompressed format
                        if (entityStoredWithCompressionType != ResponseCompressionType.None)
                        {
                            throw new Exception("Cannot do a partial response on compressed data");
                        }

                        //Send a partial response
                        SendPartialResponse(request, response, compressionType, entityDataStream, BufferSize,
                                            fileHandlerCacheItem.EntityContentType, fileHandlerCacheItem.ContentLength,
                                            ranges);
                    }
                    else
                    {
                        //Data is already compressed to the correct format

                        //Send a full response
                        SendFullResponse(request, response, compressionType, entityDataStream, BufferSize,
                                         entityStoredWithCompressionType, fileHandlerCacheItem.EntityContentType,
                                         fileHandlerCacheItem.ContentLength);
                    }
                }
                finally
                {
                    if (entityDataStream != null)
                    {
                        entityDataStream.Dispose();
                        entityDataStream = null;
                    }
                }

            }
            catch (HttpException httpException)
            {
                //Client disconnected half way through us sending data
                if (httpException.ErrorCode != ERROR_THE_REMOTE_HOST_CLOSED_THE_CONNECTION)
                    return;

                throw;
            }
        }

        internal static void SetContentEncoding(HttpResponse response, ResponseCompressionType responseCompressionType)
        {
            if (responseCompressionType != ResponseCompressionType.None)
            {
                AppendHeader(response, "Content-Encoding", responseCompressionType.ToString().ToLower());
            }
        }

        /// <summary>
        /// Make the response cachable.
        /// </summary>
        /// <param name="response">An HTTP response.</param>
        /// <param name="lastModified">The last modified date of the entity.</param>
        /// <param name="etag">The etag of the entity.</param>
        /// <param name="maxAge">The time the entity should live before browser will recheck the freshness of the entity.</param>
        internal static void SetResponseCachable(HttpResponse response, DateTime lastModified, string etag, TimeSpan maxAge)
        {
            //Set the expires header for HTTP 1.0 cliets
            response.Cache.SetExpires(DateTime.Now.Add(maxAge));

            //Proxy and browser can cache response
            response.Cache.SetCacheability(HttpCacheability.Public);

            //Proxy cache should check with orginal server once cache has expired
            response.Cache.AppendCacheExtension("must-revalidate, proxy-revalidate");

            //The date the entity was last modified
            response.Cache.SetLastModified(lastModified);

            //The unique identifier for the entity
            response.Cache.SetETag(etag);

            //How often the browser should check that it has the latest version
            response.Cache.SetMaxAge(maxAge);

            // Tell the client software that we accept Range request
            AppendHeader(response, HTTP_HEADER_ACCEPT_RANGES, HTTP_HEADER_ACCEPT_RANGES_BYTES);
        }

        #region Responses

        /// <summary>
        /// Sends an HTTP 200 "OK" response to the request referenced by the supplied context.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponse" /> of the current HTTP request.</param>
        internal static void SendOKResponseHeaders(HttpResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.OK;
            response.StatusDescription = "OK";
        }

        /// <summary>
        /// Sends an HTTP 206 "Partial content" response to the request referenced by the supplied context.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponse" /> of the current HTTP request.</param>
        internal static void SendPartialContentResponseHeaders(HttpResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.PartialContent;
            response.StatusDescription = "Partial content";
        }

        /// <summary>
        /// Sends an HTTP 304 "Not Modified" response to the request referenced by the supplied context.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponse" /> of the current HTTP request.</param>
        internal static void SendNotModifiedResponseHeaders(HttpResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.NotModified;
            response.StatusDescription = "Not Modified";
        }

        /// <summary>
        /// Sends an HTTP 404 "Not Found" response to the request referenced by the supplied context.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponse" /> of the current HTTP request.</param>
        internal static void SendNotFoundResponseHeaders(HttpResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
            response.StatusDescription = "Not Found";
        }

        /// <summary>
        /// Sends an HTTP 412 "Precondition Failed" response to the request referenced by the supplied context.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponse" /> of the current HTTP request.</param>
        internal static void SendPreconditionFailedResponseHeaders(HttpResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.PreconditionFailed;
            response.StatusDescription = "Precondition Failed";
        }

        /// <summary>
        /// Sends an HTTP 413 "Requested Entity Is Too Large" response to the request referenced by the supplied context.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponse" /> of the current HTTP request.</param>
        internal static void SendRequestedEntityIsTooLargeResponseHeaders(HttpResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
            response.StatusDescription = "Requested Entity Is Too Large";
        }

        /// <summary>
        /// Sends an HTTP 416 "Requested Range Not Satisfiable" response to the request referenced by the supplied context.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponse" /> of the current HTTP request.</param>
        internal static void SendRequestedRangeNotSatisfiableResponseHeaders(HttpResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
            response.StatusDescription = "Requested Range Not Satisfiable";
        }

        /// <summary>
        /// Sends an HTTP 501 "Not Implemented" response to the request referenced by the supplied context.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponse" /> of the current HTTP request.</param>
        internal static void SendNotImplementedResponseHeaders(HttpResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.NotImplemented;
            response.StatusDescription = "Not Implemented";
        }

        /// <summary>
        /// Sends an HTTP 405 "Method Not Allowed" response to the request referenced by the supplied context.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponse" /> of the current HTTP request.</param>
        internal static void SendMethodNotAllowed(HttpResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            response.StatusDescription = "Method Not Allowed";
        }

        /// <summary>
        /// Sends an HTTP 403 "Path Forbidden" response to the request referenced by the supplied context.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponse" /> of the current HTTP request.</param>
        internal static void SendForbidden(HttpResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.Forbidden;
            response.StatusDescription = "Path Forbidden";
        }

        #endregion

        #region Header Methods
        /// <summary>
        /// Get the value for a header in the http request. 
        /// </summary>
        /// <param name="request">An HTTP request.</param>
        /// <param name="header">The header to get the value for.</param>
        /// <param name="defaultValue">The value to return should the header not exist in the request.</param>
        /// <returns>If the header exists return the header value; else return the default value specified.</returns>
        private static string RetrieveHeader(HttpRequest request, string header, string defaultValue)
        {
            var result = request.Headers[header];

            if (String.IsNullOrEmpty(result))
            {
                return defaultValue;
            }

            return result.Replace("\"", "");
        }

        private static ResponseCompressionType GetCompressionMode(HttpRequest request)
        {
            string acceptEncoding = request.Headers["Accept-Encoding"];
            if (string.IsNullOrEmpty(acceptEncoding)) return ResponseCompressionType.None;

            acceptEncoding = acceptEncoding.ToUpperInvariant();

            if (acceptEncoding.Contains("GZIP"))
                return ResponseCompressionType.GZip;
            else if (acceptEncoding.Contains("DEFLATE"))
                return ResponseCompressionType.Deflate;
            else
                return ResponseCompressionType.None;
        }

        private static bool IsRangeRequest(HttpRequest request)
        {
            var requestHeaderRange = RetrieveHeader(request, HTTP_HEADER_RANGE, string.Empty);
            return !string.IsNullOrEmpty(requestHeaderRange);
        }

        /// <summary>
        /// Checks the If-Modified header if it was sent with the request.
        /// </summary>
        /// <param name="request">An HTTP request.</param>
        /// <param name="lastModified">The last modified date for the file requested.</param>
        /// <returns>
        /// Returns Null, if no header was sent or unable to parse incoming date; 
        /// Returns True, if the file was modified since the indicated date (RFC 1123 format); 
        /// returns False, if the file was not modified since the indicated date.
        /// </returns>
        private static bool? CheckIfModifiedSince(HttpRequest request, DateTime lastModified)
        {
            var requestHeaderIfModifiedSince = RetrieveHeader(request, HTTP_HEADER_IF_MODIFIED_SINCE, string.Empty);

            if (string.IsNullOrEmpty(requestHeaderIfModifiedSince))
            {
                return null;
            }

            DateTime incomingLastModified;

            if (!DateTime.TryParse(requestHeaderIfModifiedSince, out incomingLastModified))
            {
                return null;
            }

            return (lastModified > incomingLastModified.ToUniversalTime());
        }

        /// <summary>
        /// Checks the If-Unmodified header, if it was sent with the request.
        /// </summary>
        /// <param name="request">An HTTP request.</param>
        /// <param name="lastModified">The last modified date for the file requested.</param>
        /// <returns>
        /// Returns Null, if no header was sent or unable to parse incoming date;
        /// Returns True, if the file has not been modified since the indicated date (RFC 1123 format);
        /// Returns False, if the file has been modified since the indicated date or .
        /// </returns>
        private static bool? CheckIfUnmodifiedSince(HttpRequest request, DateTime lastModified)
        {
            string requestHeaderIfUnmodifiedSince = RetrieveHeader(request, HTTP_HEADER_IF_UNMODIFIED_SINCE, string.Empty);

            if (string.IsNullOrEmpty(requestHeaderIfUnmodifiedSince))
            {
                return null;
            }

            DateTime incomingLastModified;

            if (!DateTime.TryParse(requestHeaderIfUnmodifiedSince, out incomingLastModified))
            {
                return null;
            }

            return (lastModified <= incomingLastModified.ToUniversalTime());
        }

        /// <summary>
        /// Checks the Unless-Modified-Since header, if it was sent with the request.
        /// </summary>
        /// <param name="request">An HTTP request.</param>
        /// <param name="lastModified">The last modified date for the file requested.</param>
        /// <returns>
        /// Returns Null, if no header was sent or unable to parse incoming date;
        /// Returns True, if the file has not been modified since the indicated date (RFC 1123 format);
        /// Returns False, if the file has been modified since the indicated date.
        /// </returns>
        private static bool? CheckUnlessModifiedSince(HttpRequest request, DateTime lastModified)
        {
            string requestHeaderUnlessModifiedSince = RetrieveHeader(request, HTTP_HEADER_UNLESS_MODIFIED_SINCE,
                                                                     string.Empty);

            if (string.IsNullOrEmpty(requestHeaderUnlessModifiedSince))
            {
                return null;
            }

            DateTime incomingLastModified;

            if (!DateTime.TryParse(requestHeaderUnlessModifiedSince, out incomingLastModified))
            {
                return null;
            }

            return (lastModified <= incomingLastModified.ToUniversalTime());
        }

        /// <summary>
        /// Checks the If-Range header if it was sent with the request.
        /// </summary>
        /// <param name="request">An HTTP request.</param>
        /// <param name="entityTag">The entity tag for the file requested.</param>
        /// <param name="lastModified">The last modified date for the file requested.</param>
        /// <returns>
        /// Returns Null, if no header was sent or no range header was sent; 
        /// Returns True, if the header value matches the file's entity tag or if the file was 
        /// modified since the indicated date (RFC 1123 format);
        /// returns False, if the header values does not match the file's entity tag or if the file was 
        /// modified since the indicated date (RFC 1123 format);
        /// </returns>
        private static bool? CheckIfRange(HttpRequest request, string entityTag, DateTime lastModified)
        {
            var requestHeaderRange = RetrieveHeader(request, HTTP_HEADER_RANGE, string.Empty);
            if (string.IsNullOrEmpty(requestHeaderRange))
            {
                //The If-Range header SHOULD only be used together with a Range header, 
                //and MUST be ignored if the request does not include a Range header, 
                //or if the server does not support the sub-range operation. 
                return null;
            }

            var requestHeaderIfRange = RetrieveHeader(request, HTTP_HEADER_IF_RANGE, string.Empty);
            if (string.IsNullOrEmpty(requestHeaderIfRange))
            {
                return null;
            }

            DateTime incomingLastModified;
            //Might be a date
            if (DateTime.TryParse(requestHeaderIfRange, out incomingLastModified))
            {
                return (lastModified <= incomingLastModified.ToUniversalTime());
            }
            //Its not a date so assume its an entity tag
            return (requestHeaderIfRange == entityTag);
        }

        /// <summary>
        /// Checks the If-Match header if it was sent with the request.
        /// </summary>
        /// <param name="request">An HTTP request.</param>
        /// <param name="entityTag">The entity tag for the file requested.</param>
        /// <returns>
        /// Returns Null, if no header was sent;
        /// Returns True, if one of the header values matches the file's entity tag; 
        /// Returns False, if none of the header values matches the file's entity tag
        /// header was sent.
        /// </returns>
        private static bool? CheckIfMatch(HttpRequest request, string entityTag)
        {
            var requestHeaderIfMatch = RetrieveHeader(request, HTTP_HEADER_IF_MATCH, string.Empty);

            if (string.IsNullOrEmpty(requestHeaderIfMatch))
            {
                return null;
            }

            //Can use this to only return etag information
            if (requestHeaderIfMatch == "*")
            {
                return false;
            }

            var entityIds = requestHeaderIfMatch.Replace("bytes=", "").Split(",".ToCharArray());

            // Loop through all entity IDs, finding one 
            // which matches the current file's etag will
            // be enough to satisfy the If-Match
            foreach (var entityId in entityIds)
            {
                if (entityId.Trim() == entityTag)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks the If-None-Match header if it was sent with the request.
        /// </summary>
        /// <param name="request">An HTTP request.</param>
        /// <param name="entityTag">The entity tag for the file requested.</param>
        /// <returns>
        /// Returns Null, if no header was sent;
        /// Returns False, if one of the header values matches the file's entity tag, or if "*" was sent 
        /// Returns True, if it does not match the file;
        /// </returns>
        private static bool? CheckIfNoneMatch(HttpRequest request, string entityTag)
        {
            var requestHeaderIfNoneMatch = RetrieveHeader(request, HTTP_HEADER_IF_NONE_MATCH, String.Empty);

            if (string.IsNullOrEmpty(requestHeaderIfNoneMatch))
            {
                return null;
            }

            //Can use this to only return etag information
            if (requestHeaderIfNoneMatch == "*")
            {
                return false;
            }

            //One or more Match IDs where sent by the client software...
            var entityIds = requestHeaderIfNoneMatch.Replace("bytes=", "").Split(",".ToCharArray());

            foreach (var entityId in entityIds)
            {
                if (entityId.Trim() == entityTag)
                {
                    return false;
                }
            }

            return true;
        }


        /// <summary>
        /// Parses the Range Header from the http request.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="contentLength">The length of the content to serve.</param>
        /// <param name="ranges">A list of ranges</param>
        /// <returns>
        /// Returns Null, if there is no Range Header in the http request
        /// Return False, if there are unsatisfiable Range Headers in the http request (416 Requested Range Not Satisfiable)
        /// Returns True, if there are Range Headers in the http request
        /// </returns>
        internal static bool? TryParseRequestRangeHeader(HttpRequest request, long contentLength, out List<RangeItem> ranges)
        {
            var requestHeaderRange = RetrieveHeader(request, HTTP_HEADER_RANGE, String.Empty);

            ranges = new List<RangeItem>();

            if (string.IsNullOrEmpty(requestHeaderRange))
            {
                return null;
            }

            var rangesString = requestHeaderRange.Replace("bytes=", "").Split(",".ToCharArray());

            // Check each found Range request for consistency
            for (int i = 0; i < rangesString.Length; i++)
            {
                // Split this range request by the dash character, 
                // currentRange[0] contains the requested begin-value,
                // currentRange[1] contains the requested end-value...
                var currentRangeString = rangesString[i].Split("-".ToCharArray());
                var currentRange = new RangeItem();

                // Determine the end of the requested range
                if (string.IsNullOrEmpty(currentRangeString[1]))
                {
                    // No end was specified, take the entire range
                    currentRange.EndRange = contentLength - 1;
                }
                else
                {
                    // An end was specified...
                    int endRangeValue;
                    if (!int.TryParse(currentRangeString[1], out endRangeValue))
                    {
                        return false;
                    }

                    currentRange.EndRange = endRangeValue;
                }

                // Determine the begin of the requested range
                if (string.IsNullOrEmpty(currentRangeString[0]))
                {
                    // No begin was specified, which means that
                    // the end value indicated to return the last n
                    // bytes of the file:

                    // Calculate the begin
                    currentRange.StartRange = contentLength - 1 - currentRange.EndRange;
                    // ... to the end of the file...
                    currentRange.EndRange = contentLength - 1;
                }
                else
                {
                    // A normal begin value was indicated...
                    int beginRangeValue;
                    if (!int.TryParse(currentRangeString[0], out beginRangeValue))
                    {
                        return false;
                    }

                    currentRange.StartRange = beginRangeValue;
                }

                // Check if the requested range values are valid, 
                // return False if they are not.

                // Note:
                // Do not clean invalid values up by fitting them into
                // valid parameters using Math.Min and Math.Max, because
                // some download clients (like Go!Zilla) might send invalid 
                // (e.g. too large) range requests to determine the file limits!

                // Begin and end must not exceed the file size
                if ((currentRange.StartRange > (contentLength - 1)) | (currentRange.EndRange > (contentLength - 1)))
                {
                    return false;
                }

                // Begin and end cannot be < 0
                if ((currentRange.StartRange < 0) | (currentRange.EndRange < 0))
                {
                    return false;
                }

                // End must be larger or equal to begin value
                if (currentRange.EndRange < currentRange.StartRange)
                {
                    // The requested Range is invalid...
                    return false;
                }

                //We reached here so its a valid range, so add it to the list of ranges
                ranges.Add(currentRange);
            }
            return true;
        }

        #endregion

        #region Validation Methods
        /// <summary>
        /// Determine whether the http method is supported. Currently we only support get and head methods.
        /// </summary>
        /// <param name="request">An HTTP request.</param>
        /// <returns>True if http method is supported; false if it is not</returns>
        internal static bool ValidateHttpMethod(HttpRequest request)
        {
            return (request.HttpMethod == HTTP_METHOD_GET || request.HttpMethod == HTTP_METHOD_HEAD);
        }

        /// <summary>
        /// Process the request if it is a satisfiable cached response.
        /// </summary>
        /// <param name="request">An HTTP request.</param>
        /// <param name="response">An HTTP response.</param>
        /// <param name="lastModified">The last modified date of the entity.</param>
        /// <param name="etag">The etag of the entity.</param>
        /// <returns>
        /// Returns True, if the request has been handled
        /// Returns False, if the request must be send body data
        /// </returns>
        /// <remarks>
        /// When the browser has a satisfiable cached response, the appropriate header is also set
        /// so there is no need to continue the processing of the entity.
        /// </remarks>
        internal static bool GenerateResponseHeaders(HttpRequest request, HttpResponse response, DateTime lastModified, string etag)
        {
            lastModified = lastModified.ToUniversalTime();

            //Always assume we going to send whole entity
            var responseCode = (int)HttpStatusCode.OK;

            var requestHeaderRange = RetrieveHeader(request, HTTP_HEADER_RANGE, string.Empty);
            if (!string.IsNullOrEmpty(requestHeaderRange))
            {
                //It is a partial request
                responseCode = (int)HttpStatusCode.PartialContent;
            }

            var ifRange = CheckIfRange(request, etag, lastModified);
            if (ifRange.HasValue && !ifRange.Value)
            {
                SendOKResponseHeaders(response);
                return false;
            }

            bool? ifNoneMatch = null;
            bool? ifMatch = null;

            bool? unlessModifiedSince = null;
            bool? ifUnmodifiedSince = null;
            bool? ifModifiedSince = null;

            if (((responseCode >= 200 && responseCode <= 299 || responseCode == 304)))
            {
                //If there no matches then we do not want a cached response
                ifNoneMatch = CheckIfNoneMatch(request, etag);
                if (ifNoneMatch.HasValue)
                {
                    if (ifNoneMatch.Value && responseCode == 304)
                    {
                        responseCode = (int)HttpStatusCode.OK;
                    }
                    else
                    {
                        //If the request would, without the If-None-Match header field, result in 
                        //anything other than a 2xx or 304 status, then the If-None-Match header MUST be ignored.
                        responseCode = (int) HttpStatusCode.NotModified;
                    }
                }
            }

            if (((responseCode >= 200 && responseCode <= 299 || responseCode == 304)))
            {
                ifMatch = CheckIfMatch(request, etag);
                if (ifMatch.HasValue && !ifMatch.Value)
                {
                    //If none of the entity tags match, or if "*" is given and no current 
                    //entity exists, the server MUST NOT perform the requested method, and 
                    //MUST return a 412 (Precondition Failed) response

                    //If the request would, without the If-Match header field, result in 
                    //anything other than a 2xx or 412 status, then the If-Match header MUST be ignored.
                    responseCode = (int)HttpStatusCode.PreconditionFailed;
                }
            }

            if (
                !(ifNoneMatch.HasValue && ifNoneMatch.Value) ||
                !(ifMatch.HasValue && !ifMatch.Value))
            {
                //Only use weakly typed etags headers if strong ones are valid

                if (((responseCode >= 200 && responseCode <= 299 || responseCode == 304)))
                {
                    unlessModifiedSince = CheckUnlessModifiedSince(request, lastModified);
                    if (unlessModifiedSince.HasValue && !unlessModifiedSince.Value)
                    {
                        //If the requested variant has been modified since the specified time, 
                        //the server MUST NOT perform the requested operation, and MUST return 
                        //a 412 (Precondition Failed). Otherwise header is ignored.

                        //If the request normally (i.e., without the If-Unmodified-Since header) 
                        //would result in anything other than a 2xx or 412 status, 
                        //the If-Unmodified-Since header SHOULD be ignored.
                        responseCode = (int) HttpStatusCode.PreconditionFailed;
                    }
                }

                if (((responseCode >= 200 && responseCode <= 299 || responseCode == 304)))
                {
                    ifUnmodifiedSince = CheckIfUnmodifiedSince(request, lastModified);
                    if (ifUnmodifiedSince.HasValue && !ifUnmodifiedSince.Value)
                    {
                        //If the requested variant has been modified since the specified time, 
                        //the server MUST NOT perform the requested operation, and MUST return 
                        //a 412 (Precondition Failed). Otherwise header is ignored. 

                        //If the request normally (i.e., without the If-Unmodified-Since header) 
                        //would result in anything other than a 2xx or 412 status, 
                        //the If-Unmodified-Since header SHOULD be ignored.
                        responseCode = (int) HttpStatusCode.PreconditionFailed;
                    }
                }

                if (((responseCode >= 200 && responseCode <= 299 || responseCode == 304)))
                {
                    ifModifiedSince = CheckIfModifiedSince(request, lastModified);
                    if (ifModifiedSince.HasValue)
                    {
                        if (ifModifiedSince.Value && responseCode == 304)
                        {
                            //ifNoneMatch must be ignored if ifModifiedSince does not match so return entire entity
                            responseCode = (int)HttpStatusCode.OK;
                        }
                        else
                        {
                            responseCode = (int)HttpStatusCode.NotModified;    
                        }
                    }
                }
            }

            switch (responseCode)
            {
                case (int) HttpStatusCode.NotModified:
                    SendNotModifiedResponseHeaders(response);
                    return true;
                case (int) HttpStatusCode.PreconditionFailed:
                    SendPreconditionFailedResponseHeaders(response);
                    return true;
                case (int) HttpStatusCode.PartialContent:
                    SendPartialContentResponseHeaders(response);
                    return false;
                default:
                    SendOKResponseHeaders(response);
                    return false;
            }
        }

        #endregion

        #region Response

        /// <summary>
        /// Sends a file to the browser. 
        /// </summary>
        /// <remarks>
        /// This is the best way to transmit a file as it uses the native TransmitFile method.
        /// </remarks>
        /// <param name="request">An HTTP request.</param>
        /// <param name="response">An HTTP response.</param>
        /// <param name="compressionType">The compression type that request wants it sent back in</param>
        /// <param name="filename">File to send</param>
        public static void SendFullResponse(HttpRequest request, HttpResponse response, ResponseCompressionType compressionType, string filename)
        {
            var fileInfo = new FileInfo(filename);
            var contentType = MimeHelper.GetMimeType(fileInfo.Extension);
            var contentLength = fileInfo.Length;
            SendFullResponse(request, response, compressionType, filename, contentType, contentLength);
        }

        /// <summary>
        /// Sends a file to the browser. 
        /// </summary>
        /// <remarks>
        /// This is the best way to transmit a file as it uses the native TransmitFile method.
        /// </remarks>
        /// <param name="request">An HTTP request.</param>
        /// <param name="response">An HTTP response.</param>
        /// <param name="compressionType">The compression type that request wants it sent back in</param>
        /// <param name="fileInfo">File to send</param>
        public static void SendFullResponse(HttpRequest request, HttpResponse response, ResponseCompressionType compressionType, FileInfo fileInfo)
        {
            var contentType = MimeHelper.GetMimeType(fileInfo.Extension);
            var contentLength = fileInfo.Length;
            SendFullResponse(request, response, compressionType, fileInfo.FullName, contentType, contentLength);
        }

        /// <summary>
        /// Sends a file to the browser. 
        /// </summary>
        /// <remarks>
        /// This is the best way to transmit a file as it uses the native TransmitFile method.
        /// </remarks>
        /// <param name="request">An HTTP request.</param>
        /// <param name="response">An HTTP response.</param>
        /// <param name="compressionType">The compression type that request wants it sent back in</param>
        /// <param name="fileInfo">File to send</param>
        /// <param name="contentType">The mime type of the entity</param>
        /// <param name="contentLength">The length of the entity</param>
        public static void SendFullResponse(HttpRequest request, HttpResponse response, ResponseCompressionType compressionType, FileInfo fileInfo, string contentType, long contentLength)
        {
            SendFullResponse(request, response, compressionType, fileInfo.FullName, contentType, contentLength);
        }

        /// <summary>
        /// Sends a file to the browser. 
        /// </summary>
        /// <remarks>
        /// This is the best way to transmit a file as it uses the native TransmitFile method.
        /// </remarks>
        /// <param name="request">An HTTP request.</param>
        /// <param name="response">An HTTP response.</param>
        /// <param name="compressionType">The compression type that request wants it sent back in</param>
        /// <param name="filename">File to send</param>
        /// <param name="contentType">The mime type of the entity</param>
        /// <param name="contentLength">The length of the entity</param>
        public static void SendFullResponse(HttpRequest request, HttpResponse response, ResponseCompressionType compressionType, string filename, string contentType, long contentLength)
        {
            //How should data be compressed
            if (compressionType == ResponseCompressionType.None)
            {
                //TODO: Is this necessary - Will head still work without this set?
                AppendHeader(response, HTTP_HEADER_CONTENT_LENGTH, contentLength.ToString());
            }
            else if (compressionType == ResponseCompressionType.GZip)
            {
                SetContentEncoding(response, compressionType);
                response.Filter = new GZipStream(response.Filter, CompressionMode.Compress);
            }
            else if (compressionType == ResponseCompressionType.Deflate)
            {
                SetContentEncoding(response, compressionType);
                response.Filter = new DeflateStream(response.Filter, CompressionMode.Compress);
            }

            response.ContentType = contentType;

            if (request.HttpMethod == HTTP_METHOD_HEAD)
            {
                return;
            }

            response.TransmitFile(filename);
        }

        /// <summary>
        /// Sends a stream to the browser
        /// </summary>
        /// <param name="request">An HTTP request.</param>
        /// <param name="response">An HTTP response.</param>
        /// <param name="compressionType">The compression type that request wants it sent back in</param>
        /// <param name="stream">A stream of the entity</param>
        /// <param name="bufferSize">The buffer size to use for the stream</param>
        /// <param name="entityStoredWithCompressionType">The compression type the entity is currently stored in</param>
        /// <param name="contentType">The mime type of the entity</param>
        /// <param name="contentLength">The length of the entity</param>
        public static void SendFullResponse(HttpRequest request, HttpResponse response, ResponseCompressionType compressionType, Stream stream, int bufferSize, ResponseCompressionType entityStoredWithCompressionType, string contentType, long contentLength)
        {
            //How should data be compressed
            if (entityStoredWithCompressionType == compressionType)
            {
                //We have the entity stored in the correct compression format so just stream it
                SetContentEncoding(response, compressionType);

                //TODO: Is this necessary - Will head still work without this set?
                AppendHeader(response, HTTP_HEADER_CONTENT_LENGTH, contentLength.ToString());
            }
            else if (entityStoredWithCompressionType == ResponseCompressionType.None)
            {
                if (compressionType == ResponseCompressionType.None)
                {
                    //TODO: Is this necessary - Will head still work without this set?
                    AppendHeader(response, HTTP_HEADER_CONTENT_LENGTH, contentLength.ToString());
                }
                else if (compressionType == ResponseCompressionType.GZip)
                {
                    SetContentEncoding(response, compressionType);
                    response.Filter = new GZipStream(response.Filter, CompressionMode.Compress);
                }
                else if (compressionType == ResponseCompressionType.Deflate)
                {
                    SetContentEncoding(response, compressionType);
                    response.Filter = new DeflateStream(response.Filter, CompressionMode.Compress);
                }
            }
            else
            {
                throw new NotImplementedException("Need to decode in memory, and then stream it");
            }
            response.ContentType = contentType;

            if (request.HttpMethod == HTTP_METHOD_HEAD)
            {
                return;
            }

            TransmitFile(response, stream, bufferSize);
        }

        /// <summary>
        /// Sends ranges of a stream to the browser
        /// </summary>
        /// <param name="request">An HTTP request.</param>
        /// <param name="response">An HTTP response.</param>
        /// <param name="compressionType">The compression type that request wants it sent back in</param>
        /// <param name="filename">File to send</param>
        /// <param name="ranges">The ranges that must be sent to the browser</param>
        public static void SendPartialResponse(HttpRequest request, HttpResponse response, ResponseCompressionType compressionType, string filename, List<RangeItem> ranges)
        {
            var fileInfo = new FileInfo(filename);
            SendPartialResponse(request, response, compressionType, fileInfo, ranges);
        }

        /// <summary>
        /// Sends ranges of a stream to the browser
        /// </summary>
        /// <param name="request">An HTTP request.</param>
        /// <param name="response">An HTTP response.</param>
        /// <param name="compressionType">The compression type that request wants it sent back in</param>
        /// <param name="fileInfo">File to send</param>
        /// <param name="ranges">The ranges that must be sent to the browser</param>
        public static void SendPartialResponse(HttpRequest request, HttpResponse response, ResponseCompressionType compressionType, FileInfo fileInfo, List<RangeItem> ranges)
        {
            var contentType = MimeHelper.GetMimeType(fileInfo.Extension);
            var contentLength = fileInfo.Length;

            SendPartialResponse(request, response, compressionType, fileInfo, contentType, contentLength, ranges);
        }

        /// <summary>
        /// Sends ranges of a stream to the browser
        /// </summary>
        /// <param name="request">An HTTP request.</param>
        /// <param name="response">An HTTP response.</param>
        /// <param name="compressionType">The compression type that request wants it sent back in</param>
        /// <param name="filename">File to send</param>
        /// <param name="contentType">The mime type of the entity</param>
        /// <param name="contentLength">The length of the entity</param>
        /// <param name="ranges">The ranges that must be sent to the browser</param>
        public static void SendPartialResponse(HttpRequest request, HttpResponse response, ResponseCompressionType compressionType, string filename, string contentType, long contentLength, List<RangeItem> ranges)
        {
            SendPartialResponse(request, response, compressionType, new FileInfo(filename), contentType, contentLength, ranges);
        }

        /// <summary>
        /// Sends ranges of a stream to the browser
        /// </summary>
        /// <param name="request">An HTTP request.</param>
        /// <param name="response">An HTTP response.</param>
        /// <param name="compressionType">The compression type that request wants it sent back in</param>
        /// <param name="fileInfo">File to send</param>
        /// <param name="contentType">The mime type of the entity</param>
        /// <param name="contentLength">The length of the entity</param>
        /// <param name="ranges">The ranges that must be sent to the browser</param>
        public static void SendPartialResponse(HttpRequest request, HttpResponse response, ResponseCompressionType compressionType, FileInfo fileInfo, string contentType, long contentLength, List<RangeItem> ranges)
        {
            using (var stream = FileHelper.OpenFileStream(fileInfo, 5, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                SendPartialResponse(request, response, compressionType, stream, BufferSize, contentType, contentLength, ranges);
            }
        }

        /// <summary>
        /// Sends ranges of a stream to the browser
        /// </summary>
        /// <remarks>
        /// Partial response can only be done on an uncompressed stream
        /// </remarks>
        /// <param name="request">An HTTP request.</param>
        /// <param name="response">An HTTP response.</param>
        /// <param name="compressionType">The compression type that request wants it sent back in</param>
        /// <param name="stream">A stream of the entity</param>
        /// <param name="bufferSize">The buffer size to use for the stream</param>
        /// <param name="contentType">The mime type of the entity</param>
        /// <param name="contentLength">The length of the entity</param>
        /// <param name="ranges">The ranges that must be sent to the browser</param>
        public static void SendPartialResponse(HttpRequest request, HttpResponse response, ResponseCompressionType compressionType, Stream stream, int bufferSize, string contentType, long contentLength, List<RangeItem> ranges)
        {
            if (compressionType == ResponseCompressionType.None)
            {
                if (ranges.Count == 1)
                {
                    var startRange = ranges[0].StartRange;
                    var endRange = ranges[0].EndRange;
                    AppendHeader(response, HTTP_HEADER_CONTENT_LENGTH, (endRange - startRange + 1).ToString());
                }
                else
                {
                    var partialContentLength = GetMultipartPartialRequestLength(ranges, contentType, contentLength);
                    AppendHeader(response, HTTP_HEADER_CONTENT_LENGTH, partialContentLength.ToString());
                }
            }
            else if (compressionType == ResponseCompressionType.GZip)
            {
                SetContentEncoding(response, compressionType);
                response.Filter = new GZipStream(response.Filter, CompressionMode.Compress);
            }
            else if (compressionType == ResponseCompressionType.Deflate)
            {
                SetContentEncoding(response, compressionType);
                response.Filter = new DeflateStream(response.Filter, CompressionMode.Compress);
            }

            if (ranges.Count == 1)
            {
                var startRange = ranges[0].StartRange;
                var endRange = ranges[0].EndRange;

                response.ContentType = contentType;
                AppendHeader(response, HTTP_HEADER_CONTENT_RANGE, "bytes " + startRange + "-" + endRange + "/" + contentLength);

                if (request.HttpMethod == HTTP_METHOD_HEAD)
                {
                    return;
                }

                TransmitFile(response, stream, bufferSize, contentType, contentLength, startRange, endRange);
            }
            else
            {
                response.ContentType = MULTIPART_CONTENTTYPE;

                if (request.HttpMethod == HTTP_METHOD_HEAD)
                {
                    return;
                }

                TransmitMultiPartFile(response, stream, bufferSize, contentType, contentLength, ranges);
            }
        }

        /// <summary>
        /// Transmit stream to browser
        /// </summary>
        /// <param name="response">The <see cref="HttpResponse" /> of the current HTTP request.</param>
        /// <param name="stream">A <see cref="FileInfo" /> we are going to transmit to the browser.</param>
        /// <param name="bufferSize">The buffer size to use when transmitting file to browser.</param>
        private static void TransmitFile(HttpResponse response, Stream stream, int bufferSize)
        {
            //TODO : Do we need to seek origin
            //stream.Seek(0, SeekOrigin.Begin);

            var buffer = new byte[bufferSize];
            var readCount = 0;
            while ((readCount = stream.Read(buffer, 0, bufferSize)) > 0)
            {
                if (!response.IsClientConnected) break;
                response.OutputStream.Write(buffer, 0, readCount);
            }
        }

        /// <summary>
        /// Transmit stream range to browser
        /// </summary>
        /// <param name="response">The <see cref="HttpResponse" /> of the current HTTP request.</param>
        /// <param name="stream">A <see cref="FileInfo" /> we are going to transmit to the browser.</param>
        /// <param name="bufferSize">The buffer size to use when transmitting file to browser.</param>
        /// <param name="contentType">The content type of the entity.</param>
        /// <param name="contentLength">The length of the entity.</param>
        /// <param name="startRange">Start range</param>
        /// <param name="endRange">End range</param>
        private static void TransmitFile(HttpResponse response, Stream stream, long bufferSize, string contentType, long contentLength, long startRange, long endRange)
        {
            stream.Seek(startRange, SeekOrigin.Begin);

            var bytesToRead = endRange - startRange + 1;
            var buffer = new byte[bufferSize];
            while (bytesToRead > 0)
            {
                if (!response.IsClientConnected) return;

                var lengthOfReadChunk = stream.Read(buffer, 0, (int)Math.Min(bufferSize, bytesToRead));

                // Write the data to the current output stream.
                response.OutputStream.Write(buffer, 0, lengthOfReadChunk);

                // Reduce BytesToRead
                bytesToRead -= lengthOfReadChunk;
            }
        }

        /// <summary>
        /// Generate a multiple part header for multipart byte range request
        /// </summary>
        /// <param name="contentType"></param>
        /// <param name="contentLength"></param>
        /// <param name="startRange"></param>
        /// <param name="endRange"></param>
        /// <returns></returns>
        private static string GenerateMultiPartHeader(string contentType, long contentLength, long startRange, long endRange)
        {
            var multiPartHeader = new StringBuilder();

            multiPartHeader.AppendLine();
            multiPartHeader.AppendLine("--" + MULTIPART_BOUNDARY);
            multiPartHeader.AppendLine(HTTP_HEADER_CONTENT_TYPE + ": " + contentType);
            multiPartHeader.AppendLine(HTTP_HEADER_CONTENT_RANGE + ": bytes " +
                                      startRange + "-" +
                                      endRange + "/" +
                                      contentLength);
            multiPartHeader.AppendLine();

            return multiPartHeader.ToString();
        }

        private static string GenerateMultiPartFooter()
        {
            var multiPartFooter = new StringBuilder();
            multiPartFooter.AppendLine();
            multiPartFooter.AppendLine("--" + MULTIPART_BOUNDARY+"--");
            return multiPartFooter.ToString();
        }

        /// <summary>
        /// Calculate the content length of a partial response
        /// </summary>
        /// <param name="ranges">The ranges that must be sent to the browser</param>
        /// <param name="contentType">The mime type of the entity</param>
        /// <param name="contentLength">The length of the entity</param>
        /// <returns></returns>
        private static long GetMultipartPartialRequestLength(IEnumerable<RangeItem> ranges, string contentType, long contentLength)
        {
            var partialContentLength = 0L;

            foreach (var range in ranges)
            {
                var multiPartHeader = GenerateMultiPartHeader(contentType, contentLength, range.StartRange, range.EndRange);
                partialContentLength += multiPartHeader.Length;
                partialContentLength += range.EndRange - range.StartRange + 1;
            }
            var multiPartFooter = GenerateMultiPartFooter();
            partialContentLength += multiPartFooter.Length;
            return partialContentLength;
        }

        /// <summary>
        /// Transmit stream ranges to browser
        /// </summary>
        /// <param name="response">The <see cref="HttpResponse" /> of the current HTTP request.</param>
        /// <param name="stream">A <see cref="FileInfo" /> we are going to transmit to the browser.</param>
        /// <param name="bufferSize">The buffer size to use when transmitting file to browser.</param>
        /// <param name="contentType">The content type of the entity.</param>
        /// <param name="contentLength">The length of the entity.</param>
        /// <param name="ranges">A list of ranges to send to the browser.</param>
        private static void TransmitMultiPartFile(HttpResponse response, Stream stream, int bufferSize, string contentType, long contentLength, IEnumerable<RangeItem> ranges)
        {
            foreach (var range in ranges)
            {
                if (!response.IsClientConnected) return;

                var multiPartHeader = GenerateMultiPartHeader(contentType, contentLength, range.StartRange, range.EndRange);
                response.Output.Write(multiPartHeader);
                TransmitFile(response, stream, bufferSize, contentType, contentLength, range.StartRange, range.EndRange);
            }
            if (!response.IsClientConnected) return;
            var multiPartFooter = GenerateMultiPartFooter();
            response.Output.Write(multiPartFooter);
        }


        #endregion

        #region Utils

        private static void ReadEntityData(ResponseCompressionType compressionType, FileInfo file, Stream stream)
        {
            using (var outputStream = (compressionType == ResponseCompressionType.None ? stream : (compressionType == ResponseCompressionType.GZip ? (Stream)new GZipStream(stream, CompressionMode.Compress, true) : (Stream)new DeflateStream(stream, CompressionMode.Compress))))
            {
                // We can compress and cache this file
                using (var fs = FileHelper.OpenFileStream(file, 5, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var bufferSize = Convert.ToInt32(Math.Min(file.Length, BufferSize));
                    var buffer = new byte[bufferSize];

                    int bytesRead;
                    while ((bytesRead = fs.Read(buffer, 0, bufferSize)) > 0)
                    {
                        outputStream.Write(buffer, 0, bytesRead);
                    }
                }

                outputStream.Flush();
            }
        }
        #endregion
    }
}