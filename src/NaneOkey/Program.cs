using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using NaneOkey.UI;

namespace NaneOkey
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedAssembly;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                try
                {
                    var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup-error.txt");
                    File.WriteAllText(logPath, ex.ToString());
                }
                catch
                {
                }

                MessageBox.Show(ex.ToString(), "Nane Okey Başlatma Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static Assembly ResolveEmbeddedAssembly(object sender, ResolveEventArgs args)
        {
            var requestedName = new AssemblyName(args.Name).Name;
            if (!string.Equals(requestedName, "Photon-DotNet", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Photon-DotNet.dll"))
            {
                if (stream == null)
                {
                    return null;
                }

                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                return Assembly.Load(bytes);
            }
        }
    }
}
