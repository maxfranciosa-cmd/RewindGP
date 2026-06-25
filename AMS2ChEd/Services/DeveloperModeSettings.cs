namespace AMS2ChEd.Services
{
    public class DeveloperModeSettings
    {
        public bool IsEnabled { get; }

        public DeveloperModeSettings(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }
    }
}
