namespace Microsoft.Vault.Explorer.Model.PropObjects
{
    using System;
    using System.ComponentModel;
    using System.Drawing.Design;
    using System.Security.Cryptography.X509Certificates;

    public class CertificateUIEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.Modal;

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            X509Certificate2UI.DisplayCertificate((X509Certificate2)value);
            return value;
        }
    }
}