using ERPaperless.Abstractions.Infrastructure;
using ERPaperless.Models;

namespace ERPaperless.Infrastructure.Infrastructure
{
    public class DbContextFactory : IDbContextFactory
    {
        public dbAMCEntities Create()
        {
            return dbAMCEntities.Create();
        }
    }
}
