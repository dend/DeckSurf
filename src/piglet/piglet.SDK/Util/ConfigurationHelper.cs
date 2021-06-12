using piglet.SDK.Models;
using System;
using System.IO;
using System.Text.Json;

namespace piglet.SDK.Util
{
    public class ConfigurationHelper
    {
        const string ProfileFileName = "profile.json";

        public static string GetProfilePath(string name)
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(new string[] { localAppDataPath, "DenDev", "Piglet", "Profiles", name, ProfileFileName});
        }

        public static ConfigurationProfile WriteToConfiguration(string profile, int deviceIndex, CommandMapping mapping)
        {
            try
            {
                var path = GetProfilePath(profile);

                // In case the profile does not exist, let's make sure that we create
                // the full path. If it already exists, this does nothing.
                (new FileInfo(path)).Directory.Create();

                ConfigurationProfile configurationProfile;

                // We have to make sure that the file both exists, and is not empty. If
                // the file is empty, then the deserialization will fail, and the function
                // will return NULL.
                if (File.Exists(path) && new FileInfo(path).Length != 0)
                {
                    configurationProfile = JsonSerializer.Deserialize<ConfigurationProfile>(File.ReadAllText(path));
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

                File.WriteAllText(path, JsonSerializer.Serialize(configurationProfile));

                return configurationProfile;
            }
            catch
            {
                return null;
            }
        }
    }
}
