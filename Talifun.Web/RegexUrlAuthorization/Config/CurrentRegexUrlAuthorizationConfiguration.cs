using Talifun.Web.Configuration;

namespace Talifun.Web.RegexUrlAuthorization.Config
{
    /// <summary>
    /// Provides easy access to the current application configuration.
    /// </summary>
    public static class CurrentRegexUrlAuthorizationConfiguration
    {
        /// <summary>
        /// Gets the static instance of <see cref="RegexUrlAuthorizationSection" /> representing the current application configuration.
        /// </summary>
        public static RegexUrlAuthorizationSection Current
        {
            get { return CurrentConfigurationManager.GetSection<RegexUrlAuthorizationSection>(); }
        }
    }
}
