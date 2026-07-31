using System;

namespace ERPaperless.Models
{
    public class DocumentRowViewModel
    {
        public long Id { get; set; }
        public string FileName { get; set; }
        public string DocumentType { get; set; }
        public string UploadedByName { get; set; }
        public DateTime UploadedOn { get; set; }
        public string DownloadUrl { get; set; }
    }
}
