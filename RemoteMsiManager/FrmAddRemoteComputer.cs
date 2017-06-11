using System;
using System.Windows.Forms;

namespace RemoteMsiManager
{
    public partial class FrmAddRemoteComputer : Form
    {
        private Timer _chrono = new Timer();
        private Localization _localization = Localization.GetInstance();

        public FrmAddRemoteComputer()
        {
            InitializeComponent();

            try
            {
                this.txtBxUsername.Text = Properties.Settings.Default.AdminUser;
                this._chrono.Interval = 2000;
                this._chrono.Tick += _chrono_Tick;
            }
            catch (Exception) { }
        }

        public FrmAddRemoteComputer(string computerName, string username)
            : this()
        {
            this.txtBxComputerName.Enabled = false;
            this.ComputerName = computerName;
            this.Username = username;
            if (!String.IsNullOrEmpty(this.txtBxUsername.Text))
            { this.txtBxPassword.Select(); }
            else
            { this.txtBxUsername.Select(); }
        }

        #region (internal properties)

        /// <summary>
        /// Name or IP address of the computer
        /// </summary>
        internal string ComputerName
        {
            get { return this.txtBxComputerName.Text; }
            set { this.txtBxComputerName.Text = value; }
        }

        /// <summary>
        /// Username used to query the remote computer
        /// </summary>
        internal string Username
        {
            get { return this.txtBxUsername.Text; }
            set { this.txtBxUsername.Text = value; }
        }

        /// <summary>
        /// Password used with the provided <see cref="Username"/> to query the remote computer
        /// </summary>
        internal string Password
        {
            get { return this.txtBxPassword.Text; }
            set { this.txtBxPassword.Text = value; }
        }

        #endregion (internal properties)

        #region (private methods)

        private void ValidateData()
        {
            this.btnOk.Enabled = !String.IsNullOrEmpty(this.txtBxComputerName.Text); // && !String.IsNullOrEmpty(this.txtBxUsername.Text) && !String.IsNullOrEmpty(this.txtBxPassword.Text);
        }

        private void PingRemoteComputer()
        {
            try
            {
                this.btnPing.Image = Properties.Resources.Orange16x16;
                this.toolTip1.SetToolTip(this.btnPing, this._localization.GetLocalizedString("Testing"));
                if (!String.IsNullOrEmpty(this.txtBxComputerName.Text))
                {
                    System.Net.NetworkInformation.Ping ping = new System.Net.NetworkInformation.Ping();
                    ping.PingCompleted += ping_PingCompleted;
                    ping.SendAsync(this.txtBxComputerName.Text, null);
                }
            }
            catch (Exception)
            {
                this.btnPing.Image = Properties.Resources.Red16x16;
                this.toolTip1.SetToolTip(this.btnPing, this._localization.GetLocalizedString("CantPing"));
            }
        }

        private void ping_PingCompleted(object sender, System.Net.NetworkInformation.PingCompletedEventArgs e)
        {
            try
            {
                if (e.Reply != null && e.Reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    this.btnPing.Image = Properties.Resources.Green16x16;
                    this.toolTip1.SetToolTip(this.btnPing, this._localization.GetLocalizedString("PingOK"));
                }
                else
                {
                    this.btnPing.Image = Properties.Resources.Red16x16;
                    this.toolTip1.SetToolTip(this.btnPing, this._localization.GetLocalizedString("CantPing"));
                }
            }
            catch (Exception) { }
        }

        #endregion (private methods)

        #region (events)

        // Buttons

        private void btnTestCredential_Click(object sender, EventArgs e)
        {
            Computer tempComputer = new Computer(this.ComputerName, this.Username, this.Password);
            this.btnOk.Enabled = false;
            this.btnCancel.Enabled = false;
            this.btnTestCredential.Enabled = false;
            this.Refresh();

            if (tempComputer.IsCredentialOk())
            {
                this.btnTestCredential.BackColor = System.Drawing.Color.LightGreen;            
            }
            else
            {
                this.btnTestCredential.BackColor = System.Drawing.Color.OrangeRed;
            }
            this.btnOk.Enabled = true;
            this.btnCancel.Enabled = true;
            this.btnTestCredential.Enabled = true;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
                try
                {
                    Properties.Settings.Default.AdminUser = this.txtBxUsername.Text;
                    Properties.Settings.Default.Save();
                }
                catch (Exception) { }

                this.DialogResult = System.Windows.Forms.DialogResult.OK;
        }

        private void btnShowPassword_MouseDown(object sender, MouseEventArgs e)
        {
            this.txtBxPassword.UseSystemPasswordChar = false;
        }

        private void btnShowPassword_MouseUp(object sender, MouseEventArgs e)
        {
            this.txtBxPassword.UseSystemPasswordChar = true;
        }

        private void btnPing_Click(object sender, EventArgs e)
        {
            this.PingRemoteComputer();
        }

        // Textboxes

        private void txtBxes_TextChanged(object sender, EventArgs e)
        {
            this.btnTestCredential.BackColor = System.Drawing.SystemColors.Control;

            if (String.IsNullOrEmpty(this.txtBxComputerName.Text))
            {
                this.btnPing.Image = Properties.Resources.Red16x16;
                this.toolTip1.SetToolTip(this.btnPing, this._localization.GetLocalizedString("CantPing"));
            }
            else
            {
                if (!this._chrono.Enabled)
                    this._chrono.Start();
                else
                {
                    this._chrono.Stop();
                    this._chrono.Start();
                }
            }

            this.ValidateData();
        }

        private void txtBxComputerName_Leave(object sender, EventArgs e)
        {
            if (!String.IsNullOrEmpty(this.txtBxComputerName.Text))
            {
                this.PingRemoteComputer();
            }
        }

        // Timer

        private void _chrono_Tick(object sender, EventArgs e)
        {
            if (!String.IsNullOrEmpty(this.txtBxComputerName.Text))
            {
                this._chrono.Stop();
                this.PingRemoteComputer();
            }
        }

        #endregion (events)
    }
}
