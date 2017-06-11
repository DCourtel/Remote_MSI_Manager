using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace RemoteMsiManager
{
    public partial class FrmRemoteMsiManager : Form
    {
        private string _password = String.Empty;
        private DataGridViewCellStyle _errorCell = new DataGridViewCellStyle();

        public FrmRemoteMsiManager(String[] args)
        {
            InitializeComponent();

            this._errorCell.BackColor = Color.Goldenrod;
            this._errorCell.SelectionBackColor = Color.Goldenrod;
            this.SetUIFromArgs(args);
            this.SetVersion();
        }

        #region (Methods)

        private void SetVersion()
        {
            System.Version currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Text += " - V" + currentVersion.ToString();
        }

        private void SetUIFromArgs(string[] args)
        {
            string argComputers = String.Empty;
            string argUsername = String.Empty;
            string argPassword = String.Empty;

            try
            {
                foreach (string arg in args)
                {
                    string command = arg.Substring(0, 3).ToLower();
                    string value = arg.Substring(3);
                    switch (command)
                    {
                        case "-i=":
                        case "/i=":
                        case "-i:":
                        case "/i:":
                            this.txtBxPattern.Text = value;
                            break;
                        case "-x=":
                        case "/x=":
                        case "-x:":
                        case "/x:":
                            this.txtBxExceptions.Text = value;
                            break;
                        case "-c=":
                        case "/c=":
                        case "-c:":
                        case "/c:":
                            argComputers = value;
                            break;
                        case "-u=":
                        case "/u=":
                        case "-u:":
                        case "/u:":
                            argUsername = value;
                            break;
                        case "-p=":
                        case "/p=":
                        case "-p:":
                        case "/p:":
                            argPassword = value;
                            break;
                    }
                }
                if (!String.IsNullOrEmpty(argComputers))
                {
                    this.AddComputers(argComputers, argUsername, argPassword);
                }
            }
            catch (Exception) { }
        }

        private void AddComputers(string computers, string username, string password)
        {
            foreach (string computer in this.GetComputerList(computers))
            {
                try
                {
                    this.AddComputer(new Computer(computer, username, password));
                }
                catch (Exception) { }
            }
        }

        private List<String> GetComputerList(string computers)
        {
            List<String> computerList = new List<string>();

            try
            {
                string[] computerArray = computers.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string computer in computerArray)
                {
                    computerList.Add(computer);
                }
            }
            catch (Exception) { }

            return computerList;
        }

        private void DisplayProductForComputer(Computer computer)
        {
            Action displayProductsAction = () =>
                {
                    try
                    {
                        this.dgvProducts.Rows.Clear();
                        if (!computer.ProductsRetrievalInProgress)
                        {
                            List<DataGridViewRow> rows = new List<DataGridViewRow>();

                            if (String.IsNullOrEmpty(this.txtBxPattern.Text))
                                this.txtBxPattern.Text = "%";

                            List<MsiProduct> displayedProducts = this.FilterInstalledProducts(computer.Products, this.txtBxPattern.Text, this.txtBxExceptions.Text);
                            foreach (MsiProduct product in displayedProducts)
                            {
                                DataGridViewRow row = new DataGridViewRow();
                                row.CreateCells(this.dgvProducts, new Object[] { product, product.IdentifyingNumber, product.Name, product.Version, MsiProduct.GetFormattedInstallDate(product.InstallDate) });
                                rows.Add(row);
                            }
                            this.dgvProducts.Rows.AddRange(rows.ToArray());
                            this.dgvProducts.ClearSelection();
                            this.UpdateProductCount(computer, displayedProducts.Count);
                            if (this.dgvProducts.SortedColumn != null)
                            { this.dgvProducts.Sort(this.dgvProducts.SortedColumn, this.dgvProducts.SortOrder == SortOrder.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending); }
                            else
                            { this.dgvProducts.Sort(this.dgvProducts.Columns["ProductName"], ListSortDirection.Ascending); }
                        }
                    }
                    catch (Exception) { }
                };
            this.Invoke(displayProductsAction);
        }

        private string GetAllSelectedMsiProductCodes()
        {
            string result = String.Empty;

            foreach (DataGridViewRow row in this.dgvProducts.SelectedRows)
            {
                result += row.Cells["identifyingNumber"].Value.ToString() + ";";
            }
            result = result.Substring(0, result.Length - 1);

            return result;
        }

        private void AddComputer(Computer computerToAdd)
        {
            int index = this.dgvComputers.Rows.Add();
            computerToAdd.ProductsRetrieved += this.computer_ProductsRetrieved;

            try
            {
                computerToAdd.GetCurrentLogonUsername();
            }
            catch (Exception ex)
            {
                this.dgvComputers.Rows[index].Cells["UserName"].Style = this._errorCell;
                this.dgvComputers.Rows[index].Cells["UserName"].ToolTipText = ex.Message;
            }
            computerToAdd.RetrieveProductsAsynch();
            this.dgvComputers.Rows[index].Cells["Computer"].Value = computerToAdd;
            this.dgvComputers.Rows[index].Cells["ComputerName"].Value = computerToAdd.ComputerName;
            this.dgvComputers.Rows[index].Cells["UserName"].Value = computerToAdd.RemoteUsername;

            if (this.dgvComputers.Rows[index].Selected)
            {
                this.btnQueryComputer.Cursor = Cursors.No;
                this.btnQueryComputer.Enabled = true;
                this.btnQueryComputer.Image = Properties.Resources.HourglassAnimated;
            }
        }

        private List<MsiProduct> FilterInstalledProducts(List<MsiProduct> allInstalledProducts, string pattern, string exception)
        {
            List<MsiProduct> productsToDisplay = new List<MsiProduct>();
            List<string> productToFind = MsiProduct.SplitMsiProductCodes(pattern);
            List<string> exceptions = MsiProduct.SplitMsiProductCodes(exception);

            foreach (MsiProduct installedProduct in allInstalledProducts)
            {
                foreach (string product in productToFind)
                {
                    if (MsiProduct.PatternMatchMsiCode(installedProduct.IdentifyingNumber, product))
                    {
                        bool displayIt = true;
                        foreach (string currentException in exceptions)
                        {
                            if (MsiProduct.PatternMatchMsiCode(installedProduct.IdentifyingNumber, currentException))
                            {
                                displayIt = false;
                                break;
                            }
                        }
                        if (displayIt)
                        {
                            productsToDisplay.Add(installedProduct);
                            break;
                        }
                    }
                }
            }

            return productsToDisplay;
        }

        private void RemoveUninstalledProducts(List<MsiProduct> uninstalledProducts)
        {
            Computer targetComputer = (Computer)this.dgvComputers.SelectedRows[0].Cells["Computer"].Value;

            foreach (MsiProduct uninstalledProduct in uninstalledProducts)
            {
                try
                {
                    this.RemoveProduct(uninstalledProduct);
                }
                catch (Exception) { }
            }
            this.UpdateProductCount(targetComputer, this.dgvProducts.Rows.Count);
        }

        private void RemoveProduct(MsiProduct productToRemove)
        {
            DataGridViewRow rowToRemove = null;

            foreach (DataGridViewRow row in this.dgvProducts.Rows)
            {
                if ((row.Cells["Product"].Value as MsiProduct) == productToRemove)
                {
                    rowToRemove = row;
                    break;
                }
            }
            if (rowToRemove != null)
                this.dgvProducts.Rows.Remove(rowToRemove);
        }

        private void ShowProductDetails(int rowIndex)
        {
            try
            {
                MsiProduct selectedProduct = (MsiProduct)this.dgvProducts.Rows[rowIndex].Cells["Product"].Value;
                FrmProductProperties properties = new FrmProductProperties(selectedProduct);
                properties.ShowDialog();
            }
            catch (Exception) { }
        }

        private void UnintallSelectedProducts()
        {
            try
            {
                if (this.dgvComputers.SelectedRows != null && this.dgvComputers.SelectedRows.Count == 1)
                {
                    Computer targetComputer = (Computer)this.dgvComputers.SelectedRows[0].Cells["Computer"].Value;

                    if (this.dgvProducts.SelectedRows != null && this.dgvProducts.SelectedRows.Count > 0)
                    {
                        List<MsiProduct> productToUninstall = new List<MsiProduct>();

                        foreach (DataGridViewRow row in this.dgvProducts.SelectedRows)
                        {
                            productToUninstall.Add((MsiProduct)row.Cells["Product"].Value);
                        }
                        FrmUninstallProduct frmUninstall = new FrmUninstallProduct(targetComputer, productToUninstall);
                        frmUninstall.ShowDialog();
                        this.RemoveUninstalledProducts(frmUninstall.UninstalledProducts);
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void UpdateProductCount(Computer computer, int displayedProductCount)
        {
            try
            {
                foreach (DataGridViewRow row in this.dgvComputers.Rows)
                {
                    Computer tempComputer = (Computer)row.Cells["Computer"].Value;

                    if (tempComputer == computer)
                    {
                        row.Cells["Total"].Value = tempComputer.Products.Count;
                        if (tempComputer.LastErrorThrown != null)
                        {
                            row.Cells["Total"].Style = this._errorCell;
                            row.Cells["Total"].ToolTipText = tempComputer.LastErrorThrown.Message;
                        }
                        else
                        {
                            row.Cells["Total"].Style = row.InheritedStyle;
                            row.Cells["Total"].ToolTipText = String.Empty;
                        }
                        row.Cells["Displayed"].Value = displayedProductCount;
                        break;
                    }
                }
            }
            catch (Exception) { }
        }

        #endregion (Methods)

        #region (Events)

        // Button

        private void btnQueryComputer_Click(object sender, EventArgs e)
        {
            try
            {
                Computer computerToQuery = null;

                if (this.dgvComputers.SelectedRows != null && this.dgvComputers.SelectedRows.Count == 1)
                {
                    computerToQuery = (Computer)this.dgvComputers.SelectedRows[0].Cells["Computer"].Value;
                    this.dgvComputers.SelectedRows[0].Cells["Total"].Value = String.Empty;
                    this.dgvComputers.SelectedRows[0].Cells["Displayed"].Value = String.Empty;
                }
                try
                {
                    if (computerToQuery != null && !computerToQuery.ProductsRetrievalInProgress)
                    {
                        this.btnQueryComputer.Image = Properties.Resources.HourglassAnimated;
                        this.btnQueryComputer.Cursor = Cursors.No;
                        this.dgvProducts.Rows.Clear();
                        computerToQuery.ProductsRetrieved += computer_ProductsRetrieved;
                        computerToQuery.RetrieveProductsAsynch();
                    }
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void btnUninstall_Click(object sender, EventArgs e)
        {
            this.UnintallSelectedProducts();
        }

        private void btnInstall_Click(object sender, EventArgs e)
        {
            FrmInstallProduct frmInstallProduct = new FrmInstallProduct((Computer)this.dgvComputers.SelectedRows[0].Cells["Computer"].Value);
            frmInstallProduct.ShowDialog();
        }

        private void btnAddRemoteComputer_Click(object sender, EventArgs e)
        {
            FrmAddRemoteComputer addRemoteComputer = new FrmAddRemoteComputer();
            addRemoteComputer.Password = this._password;

            if (addRemoteComputer.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.AddComputer(new Computer(addRemoteComputer.ComputerName, addRemoteComputer.Username, addRemoteComputer.Password));
                this._password = addRemoteComputer.Password;
            }
        }

        private void btnRemoveComputers_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.dgvComputers.SelectedRows != null && this.dgvComputers.SelectedRows.Count != 0)
                {
                    DataGridViewRow[] selectedRows = new DataGridViewRow[this.dgvComputers.SelectedRows.Count];
                    this.dgvComputers.SelectedRows.CopyTo(selectedRows, 0);
                    foreach (DataGridViewRow row in selectedRows)
                    {
                        if ((row.Cells["Computer"].Value as Computer).ComputerLocation == RemoteMsiManager.Computer.ComputerLocations.Remote)
                        { this.dgvComputers.Rows.Remove(row); }
                    }
                }
            }
            catch (Exception) { }
        }

        private void btnQuit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnDeletePatternFilter_Click(object sender, EventArgs e)
        {
            this.txtBxPattern.Text = "%";
        }

        private void btnDeleteExceptionFilter_Click(object sender, EventArgs e)
        {
            this.txtBxExceptions.Text = String.Empty;
        }

        private void btnRefreshFilter_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.dgvComputers.SelectedRows != null && this.dgvComputers.SelectedRows.Count == 1)
                {
                    Computer selectedComputer = (Computer)this.dgvComputers.SelectedRows[0].Cells["Computer"].Value;
                    this.DisplayProductForComputer(selectedComputer);
                }
            }
            catch (Exception) { }
        }

        // DataGridView Computers

        private void computer_ProductsRetrieved(Computer computer)
        {
            computer.ProductsRetrieved -= computer_ProductsRetrieved;
            if (this.dgvComputers.SelectedRows != null && this.dgvComputers.SelectedRows.Count == 1 && (this.dgvComputers.SelectedRows[0].Cells["Computer"].Value as Computer) == computer)
            {
                this.DisplayProductForComputer(computer);

                Action btnAction = () =>
                {
                    this.btnQueryComputer.Image = Properties.Resources.Search24x24;
                    this.btnQueryComputer.Cursor = Cursors.Default;
                    this.btnQueryComputer.Enabled = true;
                };
                this.Invoke(btnAction);
            }
            else
            { this.UpdateProductCount(computer, 0); }
        }

        private void dgvComputers_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex != -1 && this.dgvComputers.SelectedRows != null && this.dgvComputers.SelectedRows.Count == 1)
            {
                Computer selectedComputer = (Computer)this.dgvComputers.SelectedRows[0].Cells["Computer"].Value;
                if (selectedComputer.ComputerLocation == RemoteMsiManager.Computer.ComputerLocations.Remote)
                {
                    FrmAddRemoteComputer frmAddComputer = new FrmAddRemoteComputer(selectedComputer.ComputerName, selectedComputer.Username);
                    if (frmAddComputer.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        selectedComputer.Username = frmAddComputer.Username;
                        selectedComputer.Password = frmAddComputer.Password;
                    } 
                }
            }
        }

        private void dgvComputers_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                if (this.dgvComputers.SelectedRows != null)
                {
                    if (this.dgvComputers.SelectedRows.Count == 1)
                    {
                        if (this.dgvComputers.SelectedRows[0].Cells["Computer"].Value != null)
                        {
                            Computer selectedComputer = (Computer)this.dgvComputers.SelectedRows[0].Cells["Computer"].Value;
                            this.btnRemoveComputers.Enabled = selectedComputer.ComputerLocation == RemoteMsiManager.Computer.ComputerLocations.Remote;
                            this.btnInstallProduct.Enabled = true;

                            if (selectedComputer.ProductsRetrievalInProgress)
                            {
                                this.btnQueryComputer.Cursor = Cursors.No;
                                this.btnQueryComputer.Image = Properties.Resources.HourglassAnimated;
                            }
                            else
                            {
                                this.btnQueryComputer.Cursor = Cursors.Default;
                                this.btnQueryComputer.Image = Properties.Resources.Search24x24;
                                this.btnQueryComputer.Enabled = true;
                            }
                            this.DisplayProductForComputer(selectedComputer);
                        }
                        else
                            this.btnRemoveComputers.Enabled = false;
                    }
                    else
                    {
                        this.dgvProducts.Rows.Clear();
                        this.btnQueryComputer.Cursor = Cursors.No;
                        this.btnRemoveComputers.Enabled = (this.dgvComputers.SelectedRows.Count > 0);
                        this.btnInstallProduct.Enabled = false;
                    }
                }
                else
                {
                    this.dgvProducts.Rows.Clear();
                    this.btnQueryComputer.Cursor = Cursors.No;
                    this.btnRemoveComputers.Enabled = false;
                }
            }
            catch (Exception) { }
        }

        // DataGridView Products

        private void dgvProducts_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                this.btnUninstall.Enabled = (this.dgvProducts.SelectedRows != null && this.dgvProducts.SelectedRows.Count > 0);
            }
            catch (Exception) { }
        }

        private void dgvProducts_SortCompare(object sender, DataGridViewSortCompareEventArgs e)
        {
            if (e.Column == this.dgvProducts.Columns["InstallDate"])
            {
                string date1 = e.CellValue1.ToString();
                string date2 = e.CellValue2.ToString();
                date1 = date1.Substring(6) + date1.Substring(3, 2) + date1.Substring(0, 2);
                date2 = date2.Substring(6) + date2.Substring(3, 2) + date2.Substring(0, 2);
                e.SortResult = String.Compare(date1, date2);
                e.Handled = true;
            }
            else if (e.Column == this.dgvProducts.Columns["Version"])
            {
                string version1 = e.CellValue1.ToString();
                string version2 = e.CellValue2.ToString();

                version1 = MsiProduct.GetConcatenatedVersion(version1);
                version2 = MsiProduct.GetConcatenatedVersion(version2);
                e.SortResult = String.Compare(version1, version2);
                e.Handled = true;
            }
            else
            {
                e.Handled = false;
            }
        }

        private void dgvProducts_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right && e.RowIndex >= 0)
            {
                if (this.dgvProducts.SelectedRows != null)
                {
                    if (this.dgvProducts.SelectedRows.Count == 0 || !this.dgvProducts.SelectedRows.Contains(this.dgvProducts.Rows[e.RowIndex]))
                    {
                        if ((ModifierKeys & Keys.Control) != Keys.Control)
                        { this.dgvProducts.ClearSelection(); }
                        this.dgvProducts.Rows[e.RowIndex].Selected = true;
                    }

                    int selectionCount = this.dgvProducts.SelectedRows.Count;
                    for (int i = 0; i < this.ctxMnuProducts.Items.Count; i++)
                    {
                        this.ctxMnuProducts.Items[i].Enabled = true;
                    }
                    if (selectionCount > 1)
                    {
                        this.ctxMnuProducts.Items[0].Enabled = false;
                        this.ctxMnuProducts.Items[1].Enabled = false;
                        this.ctxMnuProducts.Items[2].Enabled = false;
                        this.ctxMnuProducts.Items[3].Enabled = false;
                    }

                    this.ctxMnuProducts.Show(Cursor.Position);
                }
            }
        }

        private void dgvProducts_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (this.dgvProducts.SelectedRows != null && this.dgvProducts.SelectedRows.Count == 1)
                { this.ShowProductDetails(this.dgvProducts.SelectedRows[0].Index); }
            }
            catch (Exception) { }
        }

        // Form

        private void FrmMain_Shown(object sender, EventArgs e)
        {
            this.AddComputer(new Computer(Environment.MachineName));
        }

        // ToolStripItem Click

        private void tlStrShowDetails_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.dgvProducts.SelectedRows != null && this.dgvProducts.SelectedRows.Count == 1)
                { this.ShowProductDetails(this.dgvProducts.SelectedRows[0].Index); }
            }
            catch (Exception) { }
        }

        private void tlStrSetAsPattern_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.dgvProducts.SelectedRows != null && this.dgvProducts.SelectedRows.Count > 0)
                {
                    this.txtBxPattern.Text = this.GetAllSelectedMsiProductCodes();
                }
            }
            catch (Exception) { }
        }

        private void tlStrSetAsException_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.dgvProducts.SelectedRows != null && this.dgvProducts.SelectedRows.Count > 0)
                {
                    this.txtBxExceptions.Text = this.GetAllSelectedMsiProductCodes();
                }
            }
            catch (Exception) { }
        }

        private void tlStrUninstallProducts_Click(object sender, EventArgs e)
        {
            this.UnintallSelectedProducts();
        }

        private void tlStrCopyEntireLine_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.dgvProducts.SelectedRows != null && this.dgvProducts.SelectedRows.Count == 1)
                {
                    string content = String.Empty;

                    DataGridViewRow selectedRow = this.dgvProducts.SelectedRows[0];
                    foreach (DataGridViewCell cell in selectedRow.Cells)
                    {
                        if (cell.Visible)
                        { content += cell.Value.ToString() + ";"; }
                    }
                    content = content.Substring(0, content.Length - 1);
                    Clipboard.Clear();
                    Clipboard.SetText(content);
                }
            }
            catch (Exception) { }
        }

        private void tlStrCopyMsiProductCode_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.dgvProducts.SelectedRows != null && this.dgvProducts.SelectedRows.Count == 1)
                {
                    Clipboard.Clear();
                    Clipboard.SetText(this.dgvProducts.SelectedRows[0].Cells["identifyingNumber"].Value.ToString());
                }
            }
            catch (Exception) { }
        }

        // TextBox Change

        private void txtBxPattern_TextChanged(object sender, EventArgs e)
        {
            int carretPosition = this.txtBxPattern.SelectionStart;
            int textLength = this.txtBxPattern.TextLength;
            this.txtBxPattern.Text = MsiProduct.RemoveUnvantedCharacters(this.txtBxPattern.Text);
            carretPosition -= textLength - this.txtBxPattern.TextLength;
            this.txtBxPattern.SelectionStart = System.Math.Max(0, System.Math.Min(carretPosition, this.txtBxPattern.TextLength));
        }

        private void txtBxExceptions_TextChanged(object sender, EventArgs e)
        {
            int carretPosition = this.txtBxExceptions.SelectionStart;
            int textLength = this.txtBxExceptions.TextLength;
            this.txtBxExceptions.Text = MsiProduct.RemoveUnvantedCharacters(this.txtBxExceptions.Text);
            carretPosition -= textLength - this.txtBxExceptions.TextLength;
            this.txtBxExceptions.SelectionStart = System.Math.Max(0, System.Math.Min(carretPosition, this.txtBxExceptions.TextLength));
        }

        #endregion (Events)
    }
}
