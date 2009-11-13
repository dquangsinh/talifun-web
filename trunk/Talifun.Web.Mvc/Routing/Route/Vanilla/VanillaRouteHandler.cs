using System.Web;
using System.Web.Routing;

namespace Talifun.Web.Mvc
{
    public class VanillaRouteHandler : IRouteHandler
    {
        public IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            var handler = new VanillaHandler(requestContext);
            return handler;
        }
    }
}
