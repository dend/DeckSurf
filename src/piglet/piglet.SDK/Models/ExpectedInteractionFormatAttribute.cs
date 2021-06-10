using System;

namespace piglet.SDK.Models
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ExpectedInteractionFormatAttribute : Attribute
    {
        private CommandType commandType;
        private CommandType CommandType { get => commandType; set => commandType = value; }

        public ExpectedInteractionFormatAttribute(CommandType type)
        {
            CommandType = type;
        }
    }
}
