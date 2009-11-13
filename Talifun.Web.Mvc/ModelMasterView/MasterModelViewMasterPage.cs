using System.Web.Mvc;

namespace Talifun.Web.Mvc
{
    public class MasterModelViewMasterPage<TMasterModelGenerator, TMasterModel, TViewModel> : ViewMasterPage<TViewModel>
        where TMasterModelGenerator : IMasterModelGenerator<TMasterModel>, new()
        where TMasterModel : class
        where TViewModel : class
    {
        private TMasterModel masterModel;
        public TMasterModel MasterModel
        {
            get
            {
                if (masterModel == null)
                {
                    masterModel = new TMasterModelGenerator().GetMasterModel(ViewContext);
                }
                return masterModel;
            }
        }
    }
}
