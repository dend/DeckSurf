using System;

namespace piglet.SDK.Models
{
    public class ButtonPressEventArgs : EventArgs
    {
        public int Id { get; }
        public ButtonEventKind Kind { get; }

        public ButtonPressEventArgs(int id, ButtonEventKind kind)
        {
            Id = id;
            Kind = kind;
        }
    }
}
