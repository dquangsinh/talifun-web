using System.Web.Routing;

namespace Talifun.Web.Mvc
{
    internal class BoundUrl
    {
        public string Url { get; set; }
        public RouteValueDictionary Values { get; set; }
    }
}
