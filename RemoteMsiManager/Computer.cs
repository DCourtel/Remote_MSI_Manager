using System;
using System.Collections.Generic;
using System.IO;
using System.Management;

namespace RemoteMsiManager
{
    public class Computer
    {
        public enum ComputerLocations
        {
            Local,
            Remote
        }

        private System.Threading.Thread _retrieveProducsAsynchThread;
        private Localization _localization = Localization.GetInstance();

        /// <summary>
        /// Constructor for a local Computer
        /// </summary>
        /// <param name="computerName">Name of the computer</param>
        public Computer(string computerName)
        {
            this.Destroying = false;
            this._computerName = computerName;
            this._computerLocation = ComputerLocations.Local;
        }

        /// <summary>
        /// Constructor for a remote computer
        /// </summary>
        /// <param name="computerName">Name of the remote computer</param>
        /// <param name="userName">Name of the user used to query the remote computer. This user must have admin rights on the computer.</param>
        /// <param name="password">Password for the provided user.</param>
        public Computer(string computerName, string userName, string password)
        {
            this.Destroying = false;
            this._computerName = computerName;
            this._computerLocation = ComputerLocations.Remote;
            this._username = userName;
            this._password = password;
        }

        ~Computer()
        {
            try
            {
                if (_retrieveProducsAsynchThread != null)
                {
                    this.Destroying = true;
                    _retrieveProducsAsynchThread.Abort();
                    _retrieveProducsAsynchThread.Join(500);
                    _retrieveProducsAsynchThread = null;
                }
            }
            catch (Exception) { }
        }

        #region (Properties)

        private ComputerLocations _computerLocation = ComputerLocations.Remote;
        /// <summary>
        /// Gets or Sets if this instance target the local computer or a remote computer
        /// </summary>
        public ComputerLocations ComputerLocation
        {
            get { return this._computerLocation; }
            private set { this._computerLocation = value; }
        }

        private string _computerName = String.Empty;
        /// <summary>
        /// Gets the name of the computer
        /// </summary>
        public string ComputerName
        {
            get { return this._computerName; }
        }

        /// <summary>
        /// Gets or Sets if this class is enter in the process of Disposing. If true, we need to stop the Asynch thread.
        /// </summary>
        private bool Destroying { get; set; }

        private string _password = String.Empty;
        /// <summary>
        /// Gett or Sets the password associate to the user used to query the remote computer
        /// </summary>
        public string Password
        {
            private get { return this._password; }
            set { this._password = value; }
        }

        /// <summary>
        /// Gets or Sets if a Products retrieval is currently in progress.
        /// </summary>
        public bool ProductsRetrievalInProgress { get; private set; }

        private List<MsiProduct> _products = new List<MsiProduct>();
        /// <summary>
        /// Gets the list of MSI Products installed on the computer. Call the <see cref="RetrieveProductsAsynch"/> before, to populate the list.
        /// </summary>
        public List<MsiProduct> Products
        {
            get { return _products; }
        }

        private string _remoteUsername = String.Empty;
        /// <summary>
        /// Gets or Sets the name of the currently logged user. This property cannot be set to null or String.Empty
        /// </summary>
        public string RemoteUsername
        {
            get { return this._remoteUsername; }
            private set
            {
                if (!String.IsNullOrEmpty(value))
                { this._remoteUsername = value; }
            }
        }

        private string _username = String.Empty;
        /// <summary>
        /// Gets or Sets the username used to query the remote computer.
        /// </summary>
        public string Username
        {
            get { return this._username; }
            set { this._username = value; }
        }

        private Exception _lastErrorThrown = null;
        /// <summary>
        /// Gets or Sets the last Exception thrown. Null, if no error occurs.
        /// </summary>
        public Exception LastErrorThrown
        {
            get { return this._lastErrorThrown; }
            private set { this._lastErrorThrown = value; }
        }

        #endregion (Properties)

        #region (Methods)

        /// <summary>
        /// Check if credential for this computer are valid by trying to connect to C:\Windows.
        /// </summary>
        /// <returns>True if the connection succeed, otherwise, false.</returns>
        internal bool IsCredentialOk()
        {
            string rootFolder = @"\\" + this.ComputerName + @"\C$\Windows";

            try
            {
                return NetUse.Mount(string.Empty, rootFolder, this.Username, this.Password);
            }
            catch (Exception) { }

            return false;
        }

        private void CopyFiles(List<string> sourceFiles, string destinationFolder)
        {
            foreach (string file in sourceFiles)
            {
                FileInfo fileInfo = new FileInfo(file);
                File.Copy(file, Path.Combine(destinationFolder, fileInfo.Name), true);
            }
        }

        private void CopyFolders(string sourceFolder, string destinationFolder)
        {
            DirectoryInfo sourceFolderInfo = new DirectoryInfo(sourceFolder);

            // Copy all files
            foreach (FileInfo file in sourceFolderInfo.GetFiles())
            {
                File.Copy(file.FullName, Path.Combine(destinationFolder, file.Name));
            }
            // Create subfolders and copy inner files
            foreach (DirectoryInfo subfolder in sourceFolderInfo.GetDirectories())
            {
                Directory.CreateDirectory(Path.Combine(destinationFolder, subfolder.Name));
                this.CopyFolders(subfolder.FullName, Path.Combine(destinationFolder, subfolder.Name));
            }
        }

        internal void CopySourceToRemoteComputer(string rootFolder, string subfolder, string mainFile, List<string> additionalFiles, List<string> additionalFolders)
        {
            try
            {
                string fullPath = Path.Combine(rootFolder, subfolder);

                if (NetUse.Mount(string.Empty, rootFolder, this.Username, this.Password))
                {
                    if (!Directory.Exists(fullPath))
                    {
                        Directory.CreateDirectory(fullPath);
                    }
                    // Copy of the main file
                    FileInfo mainFileInfo = new FileInfo(mainFile);
                    File.Copy(mainFile, Path.Combine(fullPath, mainFileInfo.Name), true);
                    // Copy of the additional files
                    this.CopyFiles(additionalFiles, fullPath);
                    // Copy of the additional folders
                    foreach (string folder in additionalFolders)
                    {
                        this.CopyFolders(folder, fullPath);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new CopyFailedException(ex.Message);
            }
        }

        /// <summary>
        /// Update the RemoteUsername property with the login of the current user.
        /// </summary>
        /// <exception cref="UnauthorizedAccessException">Thrown when credentials are not valid.</exception>
        /// <exception cref="Exception">Thrown in every other case.</exception>
        internal void GetCurrentLogonUsername()
        {
            if (this.ComputerLocation == ComputerLocations.Local)
            { this.RemoteUsername = Environment.UserName; }
            else
            {
                ManagementObjectCollection results = this.GetWmiQueryResult("SELECT * FROM Win32_ComputerSystem");

                foreach (ManagementObject result in results)
                {
                    string username = String.Empty;
                    try
                    {
                        username = result["UserName"].ToString();
                    }
                    catch (Exception) { }
                    this.RemoteUsername = String.IsNullOrEmpty(username) ? "#Nobody#" : username;
                }
            }
        }

        /// <summary>
        /// Remove leading and trailing curly braces from an IdentifyingNumber
        /// </summary>
        /// <param name="identifyingNumber">An IdentifyingNumber as it is provided by the «IdentifyingNumber» property of the Win32_Product WMI Class</param>
        /// <returns>A GUID without leading and trailing curly braces</returns>
        public static string GetFormattedIdentifyingNumber(string identifyingNumber)
        {
            return identifyingNumber.Substring(1, 36);
        }

        /// <summary>
        /// Retrieves MSI Installed Products by querying the Win32_Product WMI class. Fill the <see cref="_products"/> field.
        /// </summary>
        /// <exception cref="UnauthorizedAccessException">Thrown when credentials are not valid.</exception>
        /// <exception cref="Exception">Thrown in every other case.</exception>
        private void GetInstalledProducts()
        {
            if (!this.Destroying)
            {
                this.ProductsRetrievalInProgress = true;
                this._products.Clear();

                try
                {
                    this.LastErrorThrown = null;
                    ManagementObjectCollection results = this.GetWmiQueryResult("Select * from Win32_Product");

                    foreach (ManagementObject result in results)
                    {
                        if (this.Destroying)
                            break;
                        try
                        {
                            MsiProduct product = new MsiProduct(GetFormattedIdentifyingNumber(result["identifyingNumber"].ToString()), result["Name"].ToString(), result["Version"].ToString());

                            this._products.Add(product);

                            product.Caption = GetProperty("Caption", result);
                            product.Description = GetProperty("Description", result);
                            product.HelpLink = GetProperty("HelpLink", result);
                            product.InstallLocation = GetProperty("InstallLocation", result);
                            product.InstallSource = GetProperty("InstallSource", result);
                            product.Language = GetProperty("Language", result);
                            product.LocalPackage = GetProperty("LocalPackage", result);
                            product.PackageCache = GetProperty("PackageCache", result);
                            product.PackageCode = GetProperty("PackageCode", result);
                            product.PackageName = GetProperty("PackageName", result);
                            product.ProductID = GetProperty("ProductID", result);
                            product.RegOwner = GetProperty("RegOwner", result);
                            product.Transform = GetProperty("Transform", result);
                            product.UrlInfoAbout = GetProperty("UrlInfoAbout", result);
                            product.UrlUpdateInfo = GetProperty("UrlUpdateInfo", result);
                            product.Vendor = GetProperty("Vendor", result);
                            product.InstallDate = GetProperty("InstallDate", result);

                            string assignmentType = GetProperty("AssignmentType", result);
                            switch (assignmentType)
                            {
                                case "0":
                                    product.AssignmentType = MsiProduct.Assignment_Type.User;
                                    break;
                                case "1":
                                    product.AssignmentType = MsiProduct.Assignment_Type.Computer;
                                    break;
                                default:
                                    product.AssignmentType = MsiProduct.Assignment_Type.Unknown;
                                    break;
                            }
                            string installState = GetProperty("InstallState", result);
                            switch (installState)
                            {
                                case "-6":
                                    product.InstallState = MsiProduct.Install_State.Bad_Configuration;
                                    break;
                                case "-2":
                                    product.InstallState = MsiProduct.Install_State.Invalid_Argument;
                                    break;
                                case "-1":
                                    product.InstallState = MsiProduct.Install_State.Unknown_Package;
                                    break;
                                case "1":
                                    product.InstallState = MsiProduct.Install_State.Advertised;
                                    break;
                                case "2":
                                    product.InstallState = MsiProduct.Install_State.Absent;
                                    break;
                                case "5":
                                    product.InstallState = MsiProduct.Install_State.Installed;
                                    break;
                                default:
                                    product.InstallState = MsiProduct.Install_State.Unknown;
                                    break;
                            }
                        }
                        catch (Exception) { }
                    }
                }
                catch (Exception ex)
                {
                    this.LastErrorThrown = ex;
                }
            }

            this.ProductsRetrievalInProgress = false;
            if (ProductsRetrieved != null && !this.Destroying)
                ProductsRetrieved(this);
        }

        private string GetProperty(string propertyName, ManagementObject software)
        {
            string property = String.Empty;

            try
            {
                Object value = null;
                value = software[propertyName];
                if (value != null)
                    property = value.ToString();
            }
            catch (Exception) { }

            return property;
        }

        private ManagementObjectCollection GetWmiQueryResult(string query)
        {
            ConnectionOptions connectionOptions = new ConnectionOptions();
            if (this.ComputerLocation == ComputerLocations.Remote && !String.IsNullOrEmpty(this.Username))
            {
                connectionOptions.Username = this.Username;
                connectionOptions.Password = this.Password;
            }

            ManagementPath path = new ManagementPath(@"\\" + this.ComputerName + @"\root\cimv2");
            ManagementScope scope = new ManagementScope(path, connectionOptions);

            ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, new SelectQuery(query));
            return searcher.Get();
        }

        /// <summary>
        /// Intall a MSI Product on the local or remote computer. Use the «Install» method of the Win32_Product Wmi class.
        /// </summary>
        /// <param name="packageLocation">Full path to the MSI File</param>
        /// <param name="options">Options to provide to MsiExec</param>
        /// <returns>Returns the exit code of MsiExec</returns>
        /// <exception cref="Exception"></exception>
        internal UInt32 InstallProduct(string packageLocation, string options)
        {
            UInt32 result = int.MaxValue;

            ConnectionOptions connectionOptions = new ConnectionOptions();
            if (this.ComputerLocation == ComputerLocations.Remote && !String.IsNullOrEmpty(this.Username))
            {
                connectionOptions.Username = this.Username;
                connectionOptions.Password = this.Password;
            }

            ManagementPath path = new ManagementPath(@"\\" + this.ComputerName + @"\root\cimv2");
            ManagementScope scope = new ManagementScope(path, connectionOptions);

            ManagementClass classInstance = new ManagementClass(scope, new ManagementPath("Win32_Product"), null);
            ManagementBaseObject inParams = classInstance.GetMethodParameters("Install");
            inParams["AllUsers"] = true;
            if (!String.IsNullOrWhiteSpace(options))
            { inParams["Options"] = options; }
            inParams["PackageLocation"] = packageLocation;
            ManagementBaseObject outParams = classInstance.InvokeMethod("Install", inParams, null);
            string exitCode = outParams["ReturnValue"].ToString();
            result = UInt32.Parse(exitCode);

            return result;
        }

        private void RemoveProduct(string productCode)
        {
            foreach (MsiProduct currentProduct in this.Products)
            {
                if (String.Compare(currentProduct.IdentifyingNumber, productCode, true) == 0)
                {
                    this.Products.Remove(currentProduct);
                    break;
                }
            }
        }

        /// <summary>
        /// Query the computer for installed MSI products. This method works asynchonously. Subscribe to <see cref="ProductsRetrieved"/> event to be notify when the retrieval ends.
        /// </summary>
        internal void RetrieveProductsAsynch()
        {
            _retrieveProducsAsynchThread = new System.Threading.Thread(new System.Threading.ThreadStart(this.GetInstalledProducts));
            _retrieveProducsAsynchThread.IsBackground = true;
            _retrieveProducsAsynchThread.Start();
        }

        /// <summary>
        /// Uninstall the MSI Product from this computer. Use the «Uninstall» method of the Win32_Product Wmi class.
        /// </summary>
        /// <param name="productCode">The MSI Product code of the MSI Product.</param>
        /// <returns>The MSI result code of the operation</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when credentials are not valid.</exception>
        /// <exception cref="Exception">Thrown in every other case.</exception>
        internal UInt32 UninstallProduct(string productCode)
        {
            UInt32 result = int.MaxValue;

            ManagementObjectCollection softwares = this.GetWmiQueryResult("Select * from Win32_Product where identifyingNumber like '{" + productCode + "}'");

            foreach (ManagementObject software in softwares)
            {
                result = (UInt32)software.InvokeMethod("Uninstall", null);
                if (MsiProduct.IsSuccess(result))
                { this.RemoveProduct(productCode); }
            }

            return result;
        }

        #endregion (Methods)

        #region (Event Delegates)

        public delegate void ProductsRetrievedAsynchEventHandler(Computer computer);
        public event ProductsRetrievedAsynchEventHandler ProductsRetrieved;

        #endregion (Event Delegates)

        internal class CopyFailedException : Exception
        {
            private Localization _localization = Localization.GetInstance();

            internal CopyFailedException(string errorMessage)
            {
                this.ErrorMessage = errorMessage;
            }

            private string ErrorMessage { get; set; }

            public override string Message
            {
                get { return this._localization.GetLocalizedString("CopyFailed") + this.ErrorMessage; }
            }
        }
    }
}
