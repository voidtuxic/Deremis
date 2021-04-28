namespace Deremis.Platform.Assets
{
    public struct AssetDescription
    {
        public string name;
        public string path;
        public object options;

        public AssetDescription(string path, object options = null)
        {
            this.path = path;
            this.name = path;
            this.options = options;
        }
    }
}