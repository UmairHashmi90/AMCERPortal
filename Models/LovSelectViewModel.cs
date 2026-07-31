using System.Collections.Generic;

namespace ERPaperless.Models
{
    public class LovSelectViewModel
    {
        public string Label { get; set; }
        public string LabelCss { get; set; }
        public string FieldId { get; set; }
        public string FieldName { get; set; }
        public string SelectCss { get; set; }
        public string Placeholder { get; set; }
        public bool Disabled { get; set; }
        public int? SelectedValue { get; set; }
        public IEnumerable<ERLovOptionViewModel> Options { get; set; }
    }
}
