using Talifun.Web.Configuration;

namespace Talifun.Web.Crusher.Config
{
    /// <summary>
    /// Current crusher module configuration.
    /// </summary>
    public static class CurrentCrusherConfiguration
    {
        /// <summary>
        /// Gets the static instance of <see cref="CrusherSection" /> representing the current application configuration.
        /// </summary>
        public static CrusherSection Current
        {
            get { return CurrentConfigurationManager.GetSection<CrusherSection>(); }
        }
    }
}