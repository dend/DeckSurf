namespace piglet.SDK.Interfaces
{
    public interface ICommand
    {
        public string Name { get; }
        public string Description { get; }

        public void ExecuteOnActivation(int keyIndex, string arguments);
        public void ExecuteOnAction(int keyIndex, string arguments);
    }
}
