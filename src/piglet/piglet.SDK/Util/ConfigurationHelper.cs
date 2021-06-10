using piglet.SDK.Models;
using System;
using System.IO;
using System.Text.Json;

namespace piglet.SDK.Util
{
    public class ConfigurationHelper
    {
        const string ProfileFileName = "profile.json";

        public static ConfigurationProfile WriteToConfiguration(string profile, int deviceIndex, CommandMapping mapping)
        {
            try
            {
                var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var profileFolderPath = Path.Combine(localAppDataPath, "DenDev", "Piglet", "Profiles");
                if (!Directory.Exists(profileFolderPath))
                {
                    Directory.CreateDirectory(profileFolderPath);
                }

                var specificProfileFolderPath = Path.Combine(profileFolderPath, profile.ToLower());
                var profileFolder = Directory.CreateDirectory(specificProfileFolderPath);

                var profileFilePath = Path.Combine(specificProfileFolderPath, ProfileFileName);
                ConfigurationProfile configurationProfile;
                if (File.Exists(profileFilePath))
                {
                    configurationProfile = JsonSerializer.Deserialize<ConfigurationProfile>(File.ReadAllText(profileFilePath));
                    configurationProfile.ButtonMap.Add(mapping);
                    configurationProfile.DeviceIndex = deviceIndex;
                }
                else
                {
                    configurationProfile = new ConfigurationProfile
                    {
                        DeviceIndex = deviceIndex
                    };
                    configurationProfile.ButtonMap.Add(mapping);
                }

                File.WriteAllText(profileFilePath, JsonSerializer.Serialize(configurationProfile));

                return configurationProfile;
            }
            catch
            {
                return null;
            }
        }
    }
}
