namespace Microsoft.Vault.Explorer.Model.Files.Tags
{
    public class TagValues
    {
        public string tagvalue;

        public override string ToString()
        {
            return this.tagvalue;
        }

        public TagValues(string tag)
        {
            this.tagvalue = tag;
        }

        public TagValues()
        {
        }
    }
}