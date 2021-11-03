using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FaaSTestApp.Data.Entities
{
    public class TestRequest : BaseEntity
    {
        public DateTime SentAt { get; set; }
        public DateTime RespondedAt { get; set; }
        public double ResponseTimeInMs { get; set; }
        public int HttpResponseCode { get; set; }
        public virtual TestResult TestResult { get; set; }
        [ForeignKey(nameof(TestResult))]
        public long TestResultId { get; set; }
    }
}
