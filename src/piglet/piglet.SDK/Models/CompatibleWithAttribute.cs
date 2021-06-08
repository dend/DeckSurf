using System;

namespace piglet.SDK.Models
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public class CompatibleWithAttribute : Attribute
    {
        private DeviceModel compatibleModel;
        private DeviceModel CompatibleModel { get => compatibleModel; set => compatibleModel = value; }

        public CompatibleWithAttribute(DeviceModel model)
        {
            CompatibleModel = model;
        }
    }
}
