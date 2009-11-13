using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Talifun.Web.Mvc
{
    internal class VanillaTransform : RoutingTransformBase
    {
        private static readonly Regex wordStartingWithCapitalLetterRegularExpression = new Regex(@"([A-Z][a-z]+)", RegexOptions.Compiled);
        private static readonly Regex numberRegularExpression = new Regex(@"([.0-9][0-9]*[.]*[0-9]+)", RegexOptions.Compiled);
        private static readonly Regex multipleSpacesRegularExpression = new Regex(@"\s+", RegexOptions.Compiled);

        protected override string TransformUrl(List<string> filePath, string fileName, string fileExtension, NameValueCollection queryString, string bookMark)
        {
            for (var i = 0; i < filePath.Count; i++)
            {
                filePath[i] = TranslateToVanilla(filePath[i]);
            }

            fileName = TranslateToVanilla(fileName);

            return UrlHelper.UrlToString(filePath, fileName, fileExtension, queryString, bookMark);
        }

        protected override string ReverseTransformUrl(List<string> filePath, string fileName, string fileExtension, NameValueCollection queryString, string bookMark)
        {
            for (var i = 0; i < filePath.Count; i++)
            {
                filePath[i] = TranslateFromVanilla(filePath[i]);
            }

            fileName = TranslateFromVanilla(fileName);

            return UrlHelper.UrlToString(filePath, fileName, fileExtension, queryString, bookMark);
        }

        private static string TranslateFromVanilla(string token)
        {
            //Add space before Capital Letter
            token = wordStartingWithCapitalLetterRegularExpression.Replace(token, " $1 ");

            //Add space before number and after number
            token = numberRegularExpression.Replace(token, " $1 ");

            //replace multiple spaces with single space
            token = multipleSpacesRegularExpression.Replace(token, " ");

            //get rid of spaces at the beggining and end, replace all spaces with dashes, convert to lower case
            token = token.Trim().Replace(" /", "/").Replace(' ', '-').ToLower();

            return token;
        }

        private static string TranslateToVanilla(string token)
        {
            token = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(token);
            token = token.Replace("-", "");
            return token;
        }
    }
}
