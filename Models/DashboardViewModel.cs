using System.Collections.Generic;

namespace ERPaperless.Models
{
    public class DashboardViewModel
    {
        public List<KpiCardViewModel> Cards { get; set; } = new List<KpiCardViewModel>();
    }
}
