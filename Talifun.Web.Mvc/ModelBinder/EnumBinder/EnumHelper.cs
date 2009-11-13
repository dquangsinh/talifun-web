using System;
using System.Linq;

namespace Talifun.Web.Mvc
{
    public static class EnumHelpers
    {
        public static bool Contains(Type enumType, string value)
        {
            return Enum.GetNames(enumType).Contains(value, StringComparer.OrdinalIgnoreCase);
        }

        public static T GetEnumValue<T>(T defaultValue, string value)
        {
            T enumType = defaultValue;

            if ((!String.IsNullOrEmpty(value)) && (Contains(typeof(T), value)))
                enumType = (T)Enum.Parse(typeof(T), value, true);

            return enumType;
        }
    }
}
