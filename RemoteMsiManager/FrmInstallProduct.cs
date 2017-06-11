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
    public partial class FrmInstallProduct : Form
    {
        private Computer _targetComputer;
        private Localization _localization = Localization.GetInstance();

        internal FrmInstallProduct(Computer targetComputer)
        {
            InitializeComponent();

            try
            {
                System.IO.FileInfo sourceFile = new System.IO.FileInfo(Properties.Settings.Default.NetworkInstallSource);

                if (System.IO.Directory.Exists(sourceFile.DirectoryName))
                {
                    this.txtBxLocalPackage.Text = sourceFile.DirectoryName;
                }
            }
            catch (Exception) { }

            this.txtBxTargetComputer.Text = targetComputer.ComputerName;
            this._targetComputer = targetComputer;
        }

        #region (Methods)

        /// <summary>
        /// Check whether or not, all user inputs are valids.
        /// </summary>
        /// <param name="fileChecked">Indicate whether or not the user have checked a new file</param>
        /// <param name="folderChecked">Indicate wether or not the user have checked a new folder</param>
        /// <returns>Return true if all user inputs are valids</returns>
        private void ValidateData()
        {
            bool _sourceFileExists = true;
            bool _additionalFilesExists = true;
            bool _additionalFoldersExists = true;

            try
            {
                _sourceFileExists = System.IO.File.Exists(this.txtBxLocalPackage.Text) && this.txtBxLocalPackage.Text.ToLower().EndsWith(".msi");

                foreach (string file in this.chkLstFiles.Items)
                {
                    if (!System.IO.File.Exists(file))
                    {
                        _additionalFilesExists = false;
                        break;
                    }
                }

                foreach (string folder in this.chklstFolders.Items)
                {
                    if (!System.IO.Directory.Exists(folder))
                    {
                        _additionalFoldersExists = false;
                        break;
                    }
                }
            }
            catch (Exception) { }

            this.btnInstall.Enabled = _sourceFileExists && _additionalFilesExists && _additionalFoldersExists;
            this.btnRemoveFiles.Enabled = (this.chkLstFiles.CheckedItems != null && this.chkLstFiles.CheckedItems.Count != 0);
            this.btnRemoveFolders.Enabled = (this.chklstFolders.CheckedItems != null && this.chklstFolders.CheckedItems.Count != 0);
        }

        private void DisplayStatus(string message)
        {
            this.txtBxStatus.Text = message;
            this.txtBxStatus.Refresh();
        }

        private void DisplayResult(UInt32 resultCode)
        {
            if(MsiProduct.IsSuccess(resultCode))
            {
                if(MsiProduct.IsRebootNeeded(resultCode))
                {
                    this.txtBxResult.Text = this._localization.GetLocalizedString("SuccessPendingReboot");
                }
                else
                {
                    this.txtBxResult.Text = this._localization.GetLocalizedString("Success");
                }
                this.txtBxResult.BackColor = Color.LightGreen;
            }
            else
            {
                this.txtBxResult.Text = this._localization.GetLocalizedString("Failed") + "(" + resultCode.ToString() + ") : " + MsiProduct.GetErrorMessage(resultCode);
                this.txtBxResult.BackColor = Color.Orange;
            }
            this.txtBxResult.Refresh();
        }

        /// <summary>
        /// Unable or disable bouton, textbox and checkedListbox of the UI
        /// </summary>
        /// <param name="isEnabled">true to enable the UI, false to disable the UI</param>
        private void LockUI(bool isEnabled)
        {
            this.btnBrowse.Enabled = isEnabled;
            this.btnInstall.Enabled = isEnabled;
            this.btnClose.Enabled = isEnabled;
            this.btnAddAdditionnalFiles.Enabled = isEnabled;
            this.btnRemoveFiles.Enabled = isEnabled;
            this.btnAddFolders.Enabled = isEnabled;
            this.btnRemoveFolders.Enabled = isEnabled;

            this.txtBxLocalPackage.Enabled = isEnabled;
            this.txtBxOptions.Enabled = isEnabled;

            this.chkLstFiles.Enabled = isEnabled;
            this.chklstFolders.Enabled = isEnabled;
        }

        #endregion (Methods)

        #region (Events)

        // Buttons

        /// <summary>
        /// Allow the user to browse the file system to select one and only one MSI file. The file must exists.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnBrowse_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog fileBrowser = new OpenFileDialog();
                fileBrowser.InitialDirectory = this.txtBxLocalPackage.Text;
                fileBrowser.Filter = "MSI Files|*.MSI";

                if (fileBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    this.txtBxLocalPackage.Text = fileBrowser.FileName;
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void btnAddAdditionnalFiles_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog fileBrowser = new OpenFileDialog();
                fileBrowser.InitialDirectory = this.txtBxLocalPackage.Text;
                fileBrowser.Filter = "All Files|*.*|MST Files|*.MST";
                fileBrowser.Multiselect = true;

                if (fileBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    foreach (string file in fileBrowser.FileNames)
                    {
                        if (!this.chkLstFiles.Items.Contains(file))
                        {
                            this.chkLstFiles.Items.Add(file);
                        }
                        else
                            MessageBox.Show(this._localization.GetLocalizedString("filealreadyincludes") + "\r\n" + file);
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void btnAddFolders_Click(object sender, EventArgs e)
        {
            try
            {
                FolderBrowserDialog folderBrowser = new FolderBrowserDialog();

                folderBrowser.SelectedPath = this.txtBxLocalPackage.Text;

                if (folderBrowser.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    if (!this.chklstFolders.Items.Contains(folderBrowser.SelectedPath))
                        this.chklstFolders.Items.Add(folderBrowser.SelectedPath);
                    else
                        MessageBox.Show(this._localization.GetLocalizedString("folderalreadyincludes") + "\r\n" + folderBrowser.SelectedPath);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void btnRemoveFiles_Click(object sender, EventArgs e)
        {
            try
            {
                while (this.chkLstFiles.CheckedItems.Count != 0)
                {
                    try
                    {
                        this.chkLstFiles.Items.Remove(this.chkLstFiles.CheckedItems[0]);
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            this.ValidateData();
        }

        private void btnRemoveFolders_Click(object sender, EventArgs e)
        {
            try
            {
                while (this.chklstFolders.CheckedItems.Count != 0)
                {
                    try
                    {
                        this.chklstFolders.Items.Remove(this.chklstFolders.CheckedItems[0]);
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            this.ValidateData();
        }

        private void btnInstall_Click(object sender, EventArgs e)
        {
            this.LockUI(false);
            txtBxResult.Text = String.Empty;
            txtBxResult.Refresh();
            string rootFolder = @"\\" + this._targetComputer.ComputerName + @"\C$\Windows";
            string subFolder = System.IO.Path.Combine(@"Temp\MsiManager", System.IO.Path.GetRandomFileName());

            try
            {
                List<string> additionalFiles = new List<string>();
                List<string> additionalFolders = new List<string>();
                System.IO.FileInfo mainFileInfo = new System.IO.FileInfo(this.txtBxLocalPackage.Text);

                foreach (var item in this.chkLstFiles.Items)
                {
                    additionalFiles.Add(item.ToString());
                }
                foreach (var item in this.chklstFolders.Items)
                {
                    additionalFolders.Add(item.ToString());
                }

                DisplayStatus(this._localization.GetLocalizedString("Copying"));
                this._targetComputer.CopySourceToRemoteComputer(rootFolder, subFolder, mainFileInfo.FullName, additionalFiles, additionalFolders);

                DisplayStatus(this._localization.GetLocalizedString("Installing"));
                UInt32 result = this._targetComputer.InstallProduct(System.IO.Path.Combine(rootFolder, subFolder, mainFileInfo.Name), this.txtBxOptions.Text);

                this.DisplayResult(result);
            }
            catch (Computer.CopyFailedException ex) 
            {
                this.txtBxResult.Text = ex.Message;
                this.txtBxResult.BackColor = Color.Orange;
            }
            catch (Exception ex) 
            {
                this.txtBxResult.Text = ex.Message;
                this.txtBxResult.BackColor = Color.Orange;            
            }
            try
            {
                DisplayStatus(this._localization.GetLocalizedString("DeletingTemporaryFiles"));
                System.IO.Directory.Delete(System.IO.Path.Combine(rootFolder, subFolder), true);
                NetUse.UnMount(rootFolder);
                DisplayStatus(String.Empty);
            }
            catch (Exception) { }
            this.LockUI(true);
        }

        /// <summary>
        /// Save the path to the main file and close the Form
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnClose_Click(object sender, EventArgs e)
        {
            try
            {
                if (System.IO.File.Exists(this.txtBxLocalPackage.Text))
                {
                    System.IO.FileInfo sourceFile = new System.IO.FileInfo(this.txtBxLocalPackage.Text);

                    if (System.IO.Directory.Exists(sourceFile.DirectoryName))
                    {
                        Properties.Settings.Default.NetworkInstallSource = sourceFile.DirectoryName;
                        Properties.Settings.Default.Save();
                    }
                }
            }
            catch (Exception) { }
            DialogResult = System.Windows.Forms.DialogResult.OK;
        }

        // ListBoxes

        private void chkLstFiles_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (e.NewValue == CheckState.Checked)
                this.btnRemoveFiles.Enabled = true;
            else
            {
                this.btnRemoveFiles.Enabled = (this.chkLstFiles.CheckedItems != null && this.chkLstFiles.CheckedItems.Count > 1);
            }
        }

        private void chklstFolders_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (e.NewValue == CheckState.Checked)
                this.btnRemoveFolders.Enabled = true;
            else
            {
                this.btnRemoveFolders.Enabled = (this.chklstFolders.CheckedItems != null && this.chklstFolders.CheckedItems.Count > 1);
            }
        }

        // TextBoxes

        private void txtBxLocation_TextChanged(object sender, EventArgs e)
        {
            this.ValidateData();
        }

        private void txtBxLocation_Leave(object sender, EventArgs e)
        {
            this.ValidateData();
        }

        #endregion (Events)
    }
}
