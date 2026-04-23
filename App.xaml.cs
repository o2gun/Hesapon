using System.Windows;
using ConstruxERP.Repositories;

namespace ConstruxERP
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Initialize SQLite database and run migrations
            DatabaseContext.Initialize();
        }
    }
}
