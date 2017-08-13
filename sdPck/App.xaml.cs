using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

namespace sdPck
{
    public partial class App : Application
    {
        public string[] startup_param = null;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            startup_param = e.Args;
        }
    }
}
