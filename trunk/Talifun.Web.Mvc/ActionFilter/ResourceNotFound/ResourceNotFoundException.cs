using System;

namespace Talifun.Web.Mvc
{
    public sealed class ResourceNotFoundException : Exception
    {
        public ResourceNotFoundException() { }

        public ResourceNotFoundException(Type resourceType, object resourceId) :
            base(string.Format("{0} {1} could not be found.",
                resourceType.Name, resourceId))
        {
            ResourceType = resourceType;
            ResourceId = resourceId;
        }

        public Type ResourceType { get; private set; }
        public object ResourceId { get; private set; }
    }
}
