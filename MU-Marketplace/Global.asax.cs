using System.Data.Entity;
using System.Web.Mvc;
using System.Web.Routing;
using MU_Marketplace.Infrastructure;
using MU_Marketplace.Models;

namespace MU_Marketplace
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            // Disable EF's __MigrationHistory check so EF6 works directly with MUMarketplaceDB
            Database.SetInitializer<ApplicationDbContext>(null);
            DatabaseInitializer.EnsureDatabaseAndSeed();
        }
    }
}
