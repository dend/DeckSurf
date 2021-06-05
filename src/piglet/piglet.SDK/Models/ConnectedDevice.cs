namespace piglet.SDK.Models
{
    public class ConnectedDevice
    {
        public int VID { get; set; }
        public int PID { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }

        public ConnectedDevice()
        {
        }

        public ConnectedDevice(int vid, int pid, string path, string name)
        {
            VID = vid;
            PID = pid;
            Path = path;
            Name = name;
        }
    }
}
