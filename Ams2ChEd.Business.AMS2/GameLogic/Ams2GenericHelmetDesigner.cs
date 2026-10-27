using Ams2ChEd.Business.AMS2.UI;
using AMS2ChEd.Business.GameLogic.Contracts;

namespace AMS2ChEd.Business.AMS2.GameLogic
{
    public class Ams2GenericHelmetDesigner : IGenericHelmetDesigner
    {
        public void ShowDesigner(object ownerWindow)
        {
            new GenericHelmetDesignerWindow { Owner = ownerWindow as System.Windows.Window }.ShowDialog();
        }
    }
}
