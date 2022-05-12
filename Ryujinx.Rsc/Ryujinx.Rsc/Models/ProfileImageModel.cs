namespace Ryujinx.Rsc.Models
{
    public class ProfileImageModel
    {
        public ProfileImageModel(string name, byte[] data)
        {
            Name = name;
            Data = data;
        }

        public string Name { get; set; }
        public byte[] Data { get; set; }
    }
}