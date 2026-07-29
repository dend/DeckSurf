using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;

namespace DeckSurf.App.Services
{
    /// <summary>
    /// Wraps the SDK's profile persistence in <c>%LOCALAPPDATA%\Den.Dev\DeckSurf\Profiles</c>.
    /// </summary>
    public sealed class ProfileService
    {
        public IReadOnlyList<string> ListProfiles() => ConfigurationHelper.ListProfiles();

        public ConfigurationProfile? GetProfile(string name) => ConfigurationHelper.GetProfile(name);

        public void SaveProfile(string name, ConfigurationProfile profile) => ConfigurationHelper.SaveProfile(name, profile);

        public bool DeleteProfile(string name) => ConfigurationHelper.DeleteProfile(name);

        public string ProfilesRootPath => ConfigurationHelper.GetProfilesRootPath();
    }
}
