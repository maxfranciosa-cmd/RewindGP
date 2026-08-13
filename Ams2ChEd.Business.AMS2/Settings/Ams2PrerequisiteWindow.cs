using Ams2ChEd.Business.AMS2.UI;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Updater;
using System.Windows;

namespace Ams2ChEd.Business.AMS2.Settings
{
    public class Ams2PrerequisiteWindow : IPrerequisiteWindow
    {
        private const string SEEN_PREFERENCE_KEY = "SeenAms2PrerequisitePrompt";

        private readonly ICurrentVersionCheckStore _preferencesStore;

        public Ams2PrerequisiteWindow(ICurrentVersionCheckStore preferencesStore)
        {
            _preferencesStore = preferencesStore;
        }

        public void ShowIfNeeded(object ownerWindow)
        {
            if (_preferencesStore.GetString(SEEN_PREFERENCE_KEY) == "true")
            {
                return;
            }

            var window = new PrerequisiteSetupWindow();
            if (ownerWindow is Window owner)
            {
                window.Owner = owner;
            }

            window.ShowDialog();

            if (window.DontShowAgain)
            {
                _preferencesStore.SetString(SEEN_PREFERENCE_KEY, "true");
            }
        }
    }
}
