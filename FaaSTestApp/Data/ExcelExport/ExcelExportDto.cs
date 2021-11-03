using System;

namespace FaaSTestApp.Data.ExcelExport
{
    public class ExcelExportDto
    {
        public DateTime SentDate { get; set; }
        public string Endpoint { get; set; }
        public string HttpMethod { get; set; }
        public int HttpResponseCode { get; set; }
        public double ResponseTimeInMs { get; set; }
        public bool WasSynchronous { get; set; }
        public bool WasColdStartTested { get; set; }
        public long TestSessionId { get; set; }
    }
}
