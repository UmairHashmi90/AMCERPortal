using ERPaperless.Services;

namespace ERPaperless.Models
{
    public partial class dbAMCEntities
    {
        public dbAMCEntities(string entityConnectionString)
            : base(entityConnectionString)
        {
        }

        public static dbAMCEntities Create()
        {
            return new dbAMCEntities(DBHelper.GetEntityConnectionString());
        }
    }
}
