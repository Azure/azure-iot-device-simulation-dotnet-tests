using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Helpers;
using Helpers.Http;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DeviceSimulation
{
    public class SimulationTests
    {
        private readonly IHttpClient httpClient;
        private readonly RegistryManager registry;
        private readonly string IOTHUB_CONNECTION_STRING;
        public SimulationTests()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("settings.json")
                .AddEnvironmentVariables()
                .Build();
            this.IOTHUB_CONNECTION_STRING = config["PCS_IOTHUB_CONNSTRING"];
            this.httpClient = new HttpClient();
        }

        /// <summary>
        /// Integration test using a real HTTP instance.
        /// Test that the service starts normally and returns ok status
        /// </summary>
        [Fact, Trait("Type", "IntegrationTest")]
        public void Should_Return_OK_Status()
        {
            // Act
            var request = new HttpRequest(Constants.DS_ADDRESS + "/status");
            request.AddHeader("X-Foo", "Bar");
            var response = this.httpClient.GetAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public void Should_Return_Currently_Running_Simulation()
        {
            //Arrange
            this.Should_Start_Given_Simulation();

            //Act
            var getCurrentSimulationRequest = new HttpRequest(Constants.DS_ADDRESS + "/simulations/1");
            var getCurrentSimulationResponse = this.httpClient.GetAsync(getCurrentSimulationRequest).Result;
            JObject jsonResponse = JObject.Parse(getCurrentSimulationResponse.Content);

            //Assert
            Assert.Equal(HttpStatusCode.OK, getCurrentSimulationResponse.StatusCode);
            Assert.True((bool)jsonResponse["Enabled"]);
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public void Should_Start_Given_Simulation()
        {
            //Arrange
            string ETag = this.get_ETag_Of_Running_Simulation();

            var simulationContent = "{" + $"'ETag': '{ETag}' ,'Enabled': true" + "}";
            var simulationContentByteArray = Encoding.ASCII.GetBytes(simulationContent);

            //Act
            HttpWebRequest startSimulationRequest = this.Create_Simulation_Request(simulationContentByteArray);
            Stream dataStream = startSimulationRequest.GetRequestStream();
            dataStream.Write(simulationContentByteArray, 0, simulationContentByteArray.Length);
            dataStream.Close();
            var startSimulationResponse = (HttpWebResponse)startSimulationRequest.GetResponse();

            //Assert
            Assert.Equal(HttpStatusCode.OK, startSimulationResponse.StatusCode);

            var verificationRequest = new HttpRequest(Constants.DS_ADDRESS + "/simulations/1");
            var verificationResponse = this.httpClient.GetAsync(verificationRequest).Result;
            JObject jsonResponse = JObject.Parse(verificationResponse.Content);

            Assert.True((bool)jsonResponse["Enabled"]);
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public void Should_Stop_Given_Simulation()
        {
            //Arrange
            string ETag = this.get_ETag_Of_Running_Simulation();

            var simulationContent = "{" + $"'ETag': '{ETag}' ,'Enabled': false" + "}";
            var simulationContentByteArray = Encoding.ASCII.GetBytes(simulationContent);

            //Act
            HttpWebRequest stopSimulationRequest = this.Create_Simulation_Request(simulationContentByteArray);
            Stream dataStream = stopSimulationRequest.GetRequestStream();
            dataStream.Write(simulationContentByteArray, 0, simulationContentByteArray.Length);
            dataStream.Close();
            var stopSimulationResponse = (HttpWebResponse)stopSimulationRequest.GetResponse();

            //Assert
            Assert.Equal(HttpStatusCode.OK, stopSimulationResponse.StatusCode);

            var verificationRequest = new HttpRequest(Constants.DS_ADDRESS + "/simulations/1");
            var verificationResponse = this.httpClient.GetAsync(verificationRequest).Result;
            JObject jsonResponse = JObject.Parse(verificationResponse.Content);

            Assert.False((bool)jsonResponse["Enabled"]);
        }

        private string get_ETag_Of_Running_Simulation()
        {
            var currentSimulationRequest = new HttpRequest(Constants.DS_ADDRESS + "/simulations/1");
            var currentSimulationResponse = this.httpClient.GetAsync(currentSimulationRequest).Result;
            Assert.Equal(HttpStatusCode.OK, currentSimulationResponse.StatusCode);
            JObject JsonResponse = JObject.Parse(currentSimulationResponse.Content);

            return (string)JsonResponse["ETag"];
        }

        

        public HttpWebRequest Create_Simulation_Request(byte[] simulationContentByteArray)
        {
            var startSimulationRequest = (HttpWebRequest)WebRequest.Create(Constants.SIMULATION_URL);
            startSimulationRequest.Method = "PATCH";
            startSimulationRequest.ContentLength = simulationContentByteArray.Length;
            startSimulationRequest.ContentType = "application/json";
            return startSimulationRequest;
        }
    }
}
