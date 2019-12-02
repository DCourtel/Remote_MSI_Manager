using System;
using System.Windows.Forms;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;

namespace RemoteMsiManager
{
    static class Program
    {
        /// <summary>
        /// Point d'entrée principal de l'application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            var countryCode = System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName;
            AppCenter.SetCountryCode(countryCode);
            AppCenter.Start(AppCenterAPI.ApiKey, typeof(Analytics), typeof(Crashes));
#if (DEBUG)
            //System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en");
#endif
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FrmRemoteMsiManager(args));
        }
    }
}
