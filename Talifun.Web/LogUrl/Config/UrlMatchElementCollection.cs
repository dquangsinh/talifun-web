﻿using System.Configuration;
using Talifun.Web.Configuration;

namespace Talifun.Web.LogUrl.Config
{
    /// <summary>
    /// Represents a configuration element containing a collection of <see cref="UrlMatchElement" /> configuration elements.
    /// </summary>
    [ConfigurationCollection(typeof(UrlMatchElement))]
    public sealed class UrlMatchElementCollection : CurrentConfigurationElementCollection<UrlMatchElement>
    {
        public UrlMatchElementCollection()
        {
            AddElementName = "urlMatch";
        }
    }
}