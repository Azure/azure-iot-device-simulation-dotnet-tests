using System.Net;
using Helpers;
using Helpers.Http;
using Xunit;

namespace StorageAdapter
{
    public class StatusTest
    {
        private readonly IHttpClient httpClient;

        public StatusTest()
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
            var request = new HttpRequest(Constants.STORAGE_ADAPTER_ADDRESS + "/status");
            var response = this.httpClient.GetAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
