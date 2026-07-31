using System.Collections.Generic;

namespace ERPaperless.Models
{
    public class LovRadioListViewModel
    {
        public string Label { get; set; }
        public string LabelCss { get; set; }
        public string GroupName { get; set; }
        public string GroupId { get; set; }
        public bool Disabled { get; set; }
        public int? SelectedValue { get; set; }
        public IEnumerable<ERLovOptionViewModel> Options { get; set; }
    }
}
