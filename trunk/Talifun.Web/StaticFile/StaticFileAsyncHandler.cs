using System;
using System.Web;

namespace Talifun.Web.StaticFile
{
    /// <summary>
    /// An asynchronous http handler to serve static content in an efficient way i.e. cached, compressed and resumable
    /// </summary>
    public class StaticFileAsyncHandler : IHttpAsyncHandler
    {
        private HttpContextProcessorDelegate httpContextProcessorDelegate;

        #region IHttpAsyncHandler Members

        public IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback cb, object extraData)
        {
            httpContextProcessorDelegate = ProcessRequest;
            return httpContextProcessorDelegate.BeginInvoke(context, cb, extraData);
        }

        public void EndProcessRequest(IAsyncResult result)
        {
            httpContextProcessorDelegate.EndInvoke(result);
        }

        #endregion

        #region IHttpHandler Members

        public bool IsReusable
        {
            get { return true; }
        }

        public void ProcessRequest(HttpContext context)
        {
            StaticFileHelper.ProcessRequest(context);
        }

        #endregion
    }
}