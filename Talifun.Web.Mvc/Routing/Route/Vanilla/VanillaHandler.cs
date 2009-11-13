using System.Web.Mvc;
using System.Web.Routing;

namespace Talifun.Web.Mvc
{
    public class VanillaHandler : MvcHandler
    {
        public VanillaHandler(RequestContext requestContext)
            : base(requestContext)
        {
        }
    }
}
