using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace RemoteMsiManager
{
    internal partial class FrmUninstallProduct : Form
    {
        private DataGridViewCellStyle _successStyle = new DataGridViewCellStyle();
        private DataGridViewCellStyle _successRebootStyle = new DataGridViewCellStyle();
        private DataGridViewCellStyle _failedStyle = new DataGridViewCellStyle();
        private List<MsiProduct> _uninstalledProduct = new List<MsiProduct>();
        private Localization _localization = Localization.GetInstance();

        internal FrmUninstallProduct(Computer targetComputer, List<MsiProduct> targetProducts)
        {
            InitializeComponent();

            _successStyle.BackColor = Color.LawnGreen;
            _successRebootStyle.BackColor = Color.Orange;
            _failedStyle.BackColor = Color.Red;

            this.TargetComputer = targetComputer;
            this.txtBxComputer.Text = targetComputer.ComputerName;
            foreach (MsiProduct msiProduct in targetProducts)
            {
                int index = this.dgvProducts.Rows.Add();
                this.dgvProducts.Rows[index].Cells["Product"].Value = msiProduct;
                this.dgvProducts.Rows[index].Cells["ProductName"].Value = msiProduct.Name;
                this.dgvProducts.Rows[index].Cells["Version"].Value = msiProduct.Version;
                this.dgvProducts.Rows[index].Cells["InstallDate"].Value = MsiProduct.GetFormattedInstallDate(msiProduct.InstallDate);
                this.dgvProducts.Rows[index].Cells["Result"].Value = String.Empty;
                this.dgvProducts.Rows[index].Cells["ResultCode"].Value = String.Empty;
            }
        }

        private Computer TargetComputer { get; set; }

        internal List<MsiProduct> UninstalledProducts { get { return this._uninstalledProduct; } }

        private void UninstallProducts()
        {
            foreach (DataGridViewRow row in this.dgvProducts.Rows)
            {
                try
                {
                    UInt32 resultCode = 0;
                    if (!UInt32.TryParse(row.Cells["ResultCode"].Value.ToString(), out resultCode) || !MsiProduct.IsSuccess(resultCode))
                    {
                        MsiProduct currentProduct = (MsiProduct)row.Cells["Product"].Value;
                        row.Cells["Result"].Value = this._localization.GetLocalizedString("InProgress");
                        UInt32 result = this.TargetComputer.UninstallProduct(currentProduct.IdentifyingNumber);
                        this.ReportUninstallResult(result, row, currentProduct);
                    }
                }
                catch (Exception ex)
                {
                    this.txtBxMessage.Text = this._localization.GetLocalizedString("Failed") + " : " + ex.Message;
                    this.txtBxMessage.BackColor = Color.Orange;
                }
            }
        }

        private void ReportUninstallResult(UInt32 resultCode, DataGridViewRow row, MsiProduct uninstalledProduct)
        {
            row.Cells["ResultCode"].Value = resultCode;

            if (MsiProduct.IsSuccess(resultCode))
            {
                this.txtBxMessage.BackColor = Color.LightGreen;
                if (MsiProduct.IsRebootNeeded(resultCode))
                {
                    row.Cells["Result"].Value = this._localization.GetLocalizedString("SuccessPendingReboot");
                    row.Cells["Result"].Style = _successRebootStyle;
                    this.txtBxMessage.Text = this._localization.GetLocalizedString("SuccessPendingReboot");
                    if (!this.UninstalledProducts.Contains(uninstalledProduct))
                    { this.UninstalledProducts.Add(uninstalledProduct); }
                }
                else
                {
                    row.Cells["Result"].Value = this._localization.GetLocalizedString("Success");
                    row.Cells["Result"].Style = _successStyle;
                    this.txtBxMessage.Text = this._localization.GetLocalizedString("Success");
                    if (!this.UninstalledProducts.Contains(uninstalledProduct))
                    { this.UninstalledProducts.Add(uninstalledProduct); }
                }
            }
            else
            {
                this.txtBxMessage.BackColor = Color.Orange;
                row.Cells["Result"].Value = this._localization.GetLocalizedString("Failed") + "(" + resultCode + ")";
                row.Cells["Result"].Style = _failedStyle;
                this.txtBxMessage.Text = MsiProduct.GetErrorMessage(resultCode);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            DialogResult = System.Windows.Forms.DialogResult.OK;
        }

        private void btnUninstall_Click(object sender, EventArgs e)
        {
            this.btnClose.Enabled = false;
            this.btnUninstall.Enabled = false;
            this.UninstallProducts();
            this.btnClose.Enabled = true;
            this.btnUninstall.Enabled = true;
        }

        private void dgvProducts_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex >= 0)
                {
                    MsiProduct selectedProduct = (MsiProduct)this.dgvProducts.Rows[e.RowIndex].Cells["Product"].Value;
                    FrmProductProperties properties = new FrmProductProperties(selectedProduct);
                    properties.ShowDialog();
                }
            }
            catch (Exception) { }
        }

        private void dgvProducts_SelectionChanged(object sender, EventArgs e)
        {
            this.txtBxMessage.Text = String.Empty;
            if (this.dgvProducts.SelectedRows != null && this.dgvProducts.SelectedRows.Count == 1)
            {
                UInt32 errorCode = 0;
                if (UInt32.TryParse(this.dgvProducts.SelectedRows[0].Cells["ResultCode"].Value.ToString(), out errorCode))
                {
                    this.txtBxMessage.Text = MsiProduct.GetErrorMessage(errorCode);
                }
            }
        }
    }
}
