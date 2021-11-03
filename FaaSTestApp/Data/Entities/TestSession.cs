namespace FaaSTestApp.Data.Entities
{
    public class TestSession : BaseEntity
    {
        public bool WasSynchronous { get; set; }
        public bool WasColdStartTested { get; set; }
    }
}
