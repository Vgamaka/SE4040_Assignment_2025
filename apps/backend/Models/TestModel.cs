namespace backend.Models
{
    public class TestModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();  // Auto-generate a unique ID
        public string Name { get; set; }
        public int Age { get; set; }
    }
}
