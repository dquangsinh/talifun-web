using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Web.Routing;

namespace Talifun.Web.Mvc.HostMvc
{
    public class MvcApplicationHost : MarshalByRefObject
    {
        private const string DefaultDocumentUrl = "/default.aspx";
        private const string DefaultRequestFileName = "Test";
        private const string DefaultRequestUrl = "http://test.com";
        private const string DefaultRequestQuerystring = "";
        

        public override object InitializeLifetimeService()
        {
            // This tells the CLR not to surreptitiously 
            // destroy this object -- it's a singleton
            // and will live for the life of the appdomain
            return null;
        }

        public static MvcApplicationHost GetHostRelativeToAssemblyPath(string relativePath)
        {
            var asmFilePath = new Uri(typeof(MvcApplicationHost).Assembly.CodeBase).LocalPath;
            var asmPath = Path.GetDirectoryName(asmFilePath);
            var fullPath = Path.Combine(asmPath, relativePath);
            fullPath = Path.GetFullPath(fullPath);

            return GetHost(fullPath);
        }

        public static MvcApplicationHost GetHost(string webRootPath)
        {
            var host = (MvcApplicationHost)ApplicationHost.CreateApplicationHost(
                                        typeof(MvcApplicationHost),
                                        "/",
                                        webRootPath);

            // Run a bogus request through the pipeline to wake up ASP.NET and initialize everything
            host.InitASPNET();

            return host;
        }

        public void InitASPNET()
        {
            HttpRuntime.ProcessRequest(new SimpleWorkerRequest(DefaultDocumentUrl, "", new StringWriter()));
        }

        public string ExecuteMvcUrl(string url, string query)
        {
            var writer = new StringWriter();
            var request = new SimpleWorkerRequest(url, query, writer);
            var context = HttpContext.Current = new HttpContext(request);
            var contextBase = new HttpContextWrapper(context);
            var routeData = RouteTable.Routes.GetRouteData(contextBase);
            var routeHandler = routeData.RouteHandler;
            var requestContext = new RequestContext(contextBase, routeData);
            var httpHandler = routeHandler.GetHttpHandler(requestContext);
            httpHandler.ProcessRequest(context);
            context.Response.End();
            writer.Flush();
            return writer.GetStringBuilder().ToString();
        }

        /// <summary>
        /// Generate the output of view with the supplied model
        /// </summary>
        /// <param name="viewName">The name of the view to use</param>
        /// <param name="masterName">The name of the master template to use</param>
        /// <param name="controllerType">The type of controller to use</param>
        /// <param name="model">The model to use for the view</param>
        /// <param name="routeDataValues">The route data to pass to the view</param>
        /// <remarks>
        /// We are passing a methodInfo and not the model directly, as swopping the application domain leads to parameters
        /// being serialized and deserialzed. Which is bad for perfomance and your model may not be serializable
        /// </remarks>
        /// <returns></returns>
        private string CaptureViewOutput(string viewName, string masterName, Type controllerType, object model, IDictionary<string, string> routeDataValues, string requestFileName, string requestUrl, string requestQuerystring)
        {
            var writer = new StringWriter();
            var response = new HttpResponse(writer);

            var request = new HttpRequest(requestFileName, requestUrl, requestQuerystring);
            var httpContext = System.Web.HttpContext.Current = new HttpContext(request, response);
            var httpContextBase = new HttpContextWrapper(httpContext);

            var routeData = new RouteData();
            foreach (var key in routeDataValues.Keys)
            {
                routeData.Values[key] = routeDataValues[key];
            }

            var requestContext = new RequestContext(httpContextBase, routeData);

            var controller = (ControllerBase)controllerType.GetConstructor(Type.EmptyTypes).Invoke(null);
            var controllerContext = new ControllerContext(requestContext, controller);
            controller.ControllerContext = controllerContext;

            if (model != null)
            {
                controller.ViewData.Model = model;
            }

            var result = new ViewResult
            {
                ViewName = viewName,
                MasterName = masterName,
                ViewData = controller.ViewData,
                TempData = controller.TempData
            };

            result.ExecuteResult(controllerContext);

            System.Web.HttpContext.Current = null;

            return writer.ToString();
        }

        /// <summary>
        /// Generate the output of view using a model generated by the supplied method
        /// </summary>
        /// <param name="viewName">The name of the view to use</param>
        /// <param name="masterName">The name of the master template to use</param>
        /// <param name="controllerType">The type of controller to use</param>
        /// <param name="modelMethod">The method used to generate the model</param>
        /// <param name="modelMethodParameters">Parameters to pass to model generator</param>
        /// <param name="routeDataValues">The route data to pass to the view</param>
        /// <param name="requestFileName">The physical path of the file requested</param>
        /// <param name="requestUrl">The url the file was requested on</param>
        /// <param name="requestQuerystring">The querystring for the url requested</param>
        /// <returns></returns>
        public string CaptureViewOutput(string viewName, string masterName, Type controllerType, MethodInfo modelMethod, object[] modelMethodParameters, IDictionary<string, string> routeDataValues, string requestFileName, string requestUrl, string requestQuerystring)
        {
            var model = modelMethod.Invoke(null, modelMethodParameters);
            return CaptureViewOutput(viewName, masterName, controllerType, model, routeDataValues, requestFileName, requestUrl, requestQuerystring);
        }

        /// <summary>
        /// Generate the output of view using a model generated by the supplied method
        /// </summary>
        /// <param name="viewName">The name of the view to use</param>
        /// <param name="masterName">The name of the master template to use</param>
        /// <param name="controllerType">The type of controller to use</param>
        /// <param name="modelMethod">The method used to generate the model</param>
        /// <param name="modelMethodParameters">Parameters to pass to model generator</param>
        /// <param name="routeDataValues">The route data to pass to the view</param>
        /// <returns></returns>
        public string CaptureViewOutput(string viewName, string masterName, Type controllerType, MethodInfo modelMethod, object[] modelMethodParameters, IDictionary<string, string> routeDataValues)
        {
            return CaptureViewOutput(viewName, masterName, controllerType, modelMethod, modelMethodParameters, routeDataValues, DefaultRequestFileName, DefaultRequestUrl, DefaultRequestQuerystring);
        }

        /// <summary>
        /// Generate the output of multiple views using a model generated by the supplied method. Model is reused
        /// between views.
        /// </summary>
        /// <param name="views">The views and master views</param>
        /// <param name="controllerType">The type of controller to use</param>
        /// <param name="modelMethod">The method used to generate the model</param>
        /// <param name="modelMethodParameters">Parameters to pass to model generator</param>
        /// <param name="routeDataValues">The route data to pass to the view</param>
        /// <param name="requestFileName">The physical path of the file requested</param>
        /// <param name="requestUrl">The url the file was requested on</param>
        /// <param name="requestQuerystring">The querystring for the url requested</param>
        /// <remarks>
        /// The model is only generated once and is reused for all the specified views.
        /// </remarks>
        /// <returns></returns>
        public List<string> CaptureViewOutputs(Dictionary<string, string> views, Type controllerType, MethodInfo modelMethod, object[] modelMethodParameters, IDictionary<string, string> routeDataValues, string requestFileName, string requestUrl, string requestQuerystring)
        {
            var outputs = new List<string>(views.Count);

            var model = modelMethod.Invoke(null, modelMethodParameters);

            foreach (var view in views)
            {
                var output = CaptureViewOutput(view.Key, view.Value, controllerType, model, routeDataValues, DefaultRequestFileName, DefaultRequestUrl, DefaultRequestQuerystring);
                outputs.Add(output);
            }

            return outputs;
        }

        /// <summary>
        /// Generate the output of multiple views using a model generated by the supplied method. Model is reused
        /// between views.
        /// </summary>
        /// <param name="views">The views and master views</param>
        /// <param name="controllerType">The type of controller to use</param>
        /// <param name="modelMethod">The method used to generate the model</param>
        /// <param name="modelMethodParameters">Parameters to pass to model generator</param>
        /// <param name="routeDataValues">The route data to pass to the view</param>
        /// <remarks>
        /// The model is only generated once and is reused for all the specified views.
        /// </remarks>
        /// <returns></returns>
        public List<string> CaptureViewOutputs(Dictionary<string, string> views, Type controllerType, MethodInfo modelMethod, object[] modelMethodParameters, IDictionary<string, string> routeDataValues)
        {
            return CaptureViewOutputs(views, controllerType, modelMethod, modelMethodParameters, routeDataValues, DefaultRequestFileName, DefaultRequestUrl, DefaultRequestQuerystring);
        }

        public void Shutdown()
        {
            HttpRuntime.UnloadAppDomain();
        }
    }
}
