using System;

namespace Talifun.Web.StaticFile
{
    public class FileHandlerCacheItem
    {
        public byte[] EntityData { get; set; }
        public long ContentLength;
        public string EntityContentType;
        public string EntityEtag;
        public DateTime EntityLastModified;
    }
}