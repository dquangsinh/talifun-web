using System.Web.Mvc;

namespace Talifun.Web.Mvc
{
    public interface IMasterModelGenerator<TMasterModel> where TMasterModel : class
    {
        TMasterModel GetMasterModel(ViewContext viewContext);
    }
}
