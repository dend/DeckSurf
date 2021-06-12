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

                // We have to make sure that the file both exists, and is not empty. If
                // the file is empty, then the deserialization will fail, and the function
                // will return NULL.
                if (File.Exists(profileFilePath) && new FileInfo(profileFilePath).Length != 0)
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
