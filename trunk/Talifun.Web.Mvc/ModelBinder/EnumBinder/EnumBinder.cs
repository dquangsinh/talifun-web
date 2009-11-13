using System.Web.Mvc;

namespace Talifun.Web.Mvc
{
    public class EnumBinder<T> : IModelBinder
    {
        private string ValueName { get; set; }
        private T DefaultValue { get; set; }
        public EnumBinder(string valueName, T defaultValue)
        {
            ValueName = valueName;
            DefaultValue = defaultValue;
        }

        #region IModelBinder Members

        public object BindModel(ControllerContext controllerContext, ModelBindingContext bindingContext)
        {
            return bindingContext.ValueProvider[ValueName] == null
                ? DefaultValue
                : EnumHelpers.GetEnumValue(DefaultValue, bindingContext.ValueProvider[ValueName].AttemptedValue);
        }

        #endregion
    }
}
