using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace FaaSTestApp.Data.Entities
{
    public class TestResult : BaseEntity
    {
        public string Endpoint { get; set; }
        public double AverageResponseTimeInMs { get; set; }
        public string HttpMethod { get; set; }
        public bool WasSuccessful { get; set; }
        public virtual TestSession TestSession { get; set; }
        [ForeignKey(nameof(TestSession))]
        public long TestSessionId { get; set; }
        public virtual ICollection<TestRequest> Requests { get; set; }

        public override string ToString()
        {
            return "Adres punktu obsługi żądań: " + Endpoint + '\n' +
                "Średni czas odpowiedzi na zapytanie: " + AverageResponseTimeInMs + '\n' +
                "Metoda HTTP: " + HttpMethod + '\n' +
                "Czy wszystkie odpowiedzi były pozytywne: " + (WasSuccessful ? "Tak" : "Nie") + '\n';
        }
    }
}
