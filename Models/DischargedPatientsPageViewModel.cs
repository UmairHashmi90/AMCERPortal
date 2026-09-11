using System.Collections.Generic;

namespace ERPaperless.Models
{
    public class DischargedPatientsPageViewModel
    {
        public IList<LocationCardViewModel> Patients { get; set; }
        public string Search { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }

        public int FromItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
        public int ToItem => TotalCount == 0 ? 0 : System.Math.Min(Page * PageSize, TotalCount);
        public bool HasPrevious => Page > 1;
        public bool HasNext => Page < TotalPages;
    }
}
