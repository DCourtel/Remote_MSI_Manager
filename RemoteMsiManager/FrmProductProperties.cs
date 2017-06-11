using System.Windows.Forms;

namespace RemoteMsiManager
{
    internal partial class FrmProductProperties : Form
    {
        internal FrmProductProperties(MsiProduct product)
        {
            InitializeComponent();
            this.propertyGrid1.SelectedObject = product;
        }
    }
}
