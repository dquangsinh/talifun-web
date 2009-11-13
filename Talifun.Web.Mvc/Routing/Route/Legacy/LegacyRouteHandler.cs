using System.Web;
using System.Web.Routing;

namespace Talifun.Web.Mvc
{
    /// <summary>
    /// The legacy route handler, used for getting the HttpHandler for the request
    /// </summary>
    public class LegacyRouteHandler : IRouteHandler
    {
        public IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            return new LegacyHandler(requestContext);
        }
    }
}
