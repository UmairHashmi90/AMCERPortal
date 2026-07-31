using ERPaperless.Models;

namespace ERPaperless.Abstractions.Infrastructure
{
    public interface IDbContextFactory
    {
        dbAMCEntities Create();
    }
}
