using System;
using System.Reflection;
using System.Web.Mvc;

namespace Talifun.Web.Mvc
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method,
                   Inherited = true, AllowMultiple = false)]
    public sealed class HandleResourceNotFoundAttribute : FilterAttribute, IExceptionFilter
    {
        /// <summary>
        /// Handle resource not found error
        /// </summary>
        public HandleResourceNotFoundAttribute()
        {
            View = "ResourceNotFound";
        }

        /// <summary>
        /// Handle resource not found error
        /// </summary>
        /// <param name="view">The view to load on error</param>
        public HandleResourceNotFoundAttribute(string view)
        {
            View = view;
        }

        /// <summary>
        /// The view to display when there is an error
        /// </summary>
        public string View { get; set; }

        public void OnException(ExceptionContext filterContext)
        {
            var controller = filterContext.Controller as System.Web.Mvc.Controller;
            if (controller == null || filterContext.ExceptionHandled)
                return;

            var exception = filterContext.Exception;
            if (exception == null)
                return;

            // Action method exceptions will be wrapped in a
            // TargetInvocationException since they're invoked using 
            // reflection, so we have to unwrap it.
            if (exception is TargetInvocationException)
                exception = exception.InnerException;

            // If this is not a ResourceNotFoundException error, ignore it.
            if (!(exception is ResourceNotFoundException))
                return;

            filterContext.Result = new ViewResult()
            {
                TempData = controller.TempData,
                ViewName = View
            };

            filterContext.ExceptionHandled = true;
            filterContext.HttpContext.Response.Clear();
            filterContext.HttpContext.Response.StatusCode = 404;
        }
    }
}
