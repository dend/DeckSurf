using DeckSurf.SDK.Models;

namespace DeckSurf.App.Helpers
{
    /// <summary>
    /// Static button-grid layouts per device model, for rendering the editor grid
    /// when the profile's device is not currently connected.
    /// </summary>
    public static class DeviceLayouts
    {
        public static (int Columns, int Rows) GetGrid(DeviceModel model) => model switch
        {
            DeviceModel.XL or DeviceModel.XL2022 => (8, 4),
            DeviceModel.Mini or DeviceModel.Mini2022 => (3, 2),
            DeviceModel.Neo or DeviceModel.Plus => (4, 2),
            DeviceModel.Original or DeviceModel.Original2019 or DeviceModel.MK2 => (5, 3),
            _ => (5, 3),
        };

        public static bool HasScreen(DeviceModel model) => model is DeviceModel.Plus or DeviceModel.Neo;

        public static bool HasKnobs(DeviceModel model) => model is DeviceModel.Plus;
    }
}
