using System;
using System.IO;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using Talifun.Web.StaticFile;

namespace Talifun.Web.Mvc
{
    public class StaticFileActionResult : ActionResult
    {
        private static readonly FieldInfo httpRequestFieldInfo = typeof(HttpRequestWrapper).GetField("_httpRequest", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo httpResponseFieldInfo = typeof(HttpResponseWrapper).GetField("_httpResponse", BindingFlags.Instance | BindingFlags.NonPublic);

        public StaticFileActionResult(FileInfo file)
        {
            File = file;
            if (file == null)
            {
                throw new ArgumentException("file to download must not be null");
            }
        }

        public FileInfo File { get; private set; }

        public override void ExecuteResult(ControllerContext context)
        {
            var request = (HttpRequest)httpRequestFieldInfo.GetValue(context.HttpContext.Request);
            var response = (HttpResponse)httpResponseFieldInfo.GetValue(context.HttpContext.Response);

            StaticFileHelper.ProcessRequest(request, response, File);
        }
    }
}
