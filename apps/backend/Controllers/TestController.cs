using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using backend.Models;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly IMongoCollection<TestModel> _testCollection;

        public TestController(IMongoDatabase database)
        {
            _testCollection = database.GetCollection<TestModel>("testCollection");
        }

        // ✅ Add test data
        [HttpPost("add")]
        public IActionResult AddTestData([FromBody] TestModel data)
        {
            _testCollection.InsertOne(data);
            return Ok(new { message = "Data added successfully!", inserted = data });
        }

        // ✅ Get all data
        [HttpGet("all")]
        public IActionResult GetAllData()
        {
            var items = _testCollection.Find(_ => true).ToList();
            return Ok(items);
        }
    }
}
