using System.Net;
using Helpers.Http;
using Xunit;

namespace DeviceSimulation
{
    public class SimulationTests
    {
        private readonly IHttpClient httpClient;
        private const string DS_ADDRESS = "http://localhost:9003/v1";

        public SimulationTests()
        {
            this.httpClient = new HttpClient();
        }

        /// <summary>
        /// Integration test using a real HTTP instance.
        /// Test that the service starts normally and returns ok status
        /// </summary>
        [Fact]
        public void Should_Return_OK_Status()
        {
            // Act
            var request = new HttpRequest(DS_ADDRESS + "/status");
            request.AddHeader("X-Foo", "Bar");
            var response = this.httpClient.GetAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
