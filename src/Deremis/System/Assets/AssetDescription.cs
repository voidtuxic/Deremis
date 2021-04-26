namespace Deremis.System.Assets
{
    public struct AssetDescription
    {
        public string name;
        public string path;
        public int type;
        public object options;

        public AssetDescription(string path, int type, object options = null)
        {
            this.path = path;
            this.name = path;
            this.type = type;
            this.options = options;
        }
    }
}