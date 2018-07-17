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
            this.Should_Delete_Existing_Simulation();
            var simulation = JObject.Parse(@"{   
                'Enabled': true, 
                'DeviceModels': [   
                    {   
                        'Id': 'truck-01', 
                        'Count': 5 
                    } 
                ]
            }");
            var createSimulationRequest = new HttpRequest(Constants.DS_ADDRESS + "/simulations");
            createSimulationRequest.SetContent(simulation);
            var createSimulationResponse = this.httpClient.PostAsync(createSimulationRequest).Result;
            Assert.Equal(HttpStatusCode.OK, createSimulationResponse.StatusCode);

            //Act
            var getCurrentSimulationRequest = new HttpRequest(Constants.DS_ADDRESS + "/simulations/1");
            var getCurrentSimulationResponse = this.httpClient.GetAsync(getCurrentSimulationRequest).Result;
            JObject jsonResponse = JObject.Parse(getCurrentSimulationResponse.Content);

            //Assert
            Assert.True(HttpStatusCode.OK == getCurrentSimulationResponse.StatusCode && (bool)jsonResponse["Enabled"] && (int)jsonResponse["Id"] == 1);

        }

        [Fact, Trait("Type", "IntegrationTest")]
        public void Should_Create_Default_Simulation()
        {
            //Arrange
            this.Should_Delete_Existing_Simulation();
            var simulation = JObject.Parse(@"{   
                'Enabled': true, 
                'DeviceModels': [   
                    {   
                        'Id': 'truck-01', 
                        'Count': 5 
                    } 
                ]
            }");

            //Act
            var request = new HttpRequest(Constants.DS_ADDRESS + "/simulations");
            request.SetContent(simulation);
            var response = this.httpClient.PostAsync(request).Result;

            //Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var getCurrentSimulationRequest = new HttpRequest(Constants.DS_ADDRESS + "/simulations/1");
            var getCurrentSimulationResponse = this.httpClient.GetAsync(getCurrentSimulationRequest).Result;
            JObject jsonResponse = JObject.Parse(getCurrentSimulationResponse.Content);

            Assert.True(HttpStatusCode.OK == getCurrentSimulationResponse.StatusCode && (int)jsonResponse["Id"] == 1);
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public async void Should_Create_SimulatedDevice()
        {
            //Arrange
            this.Should_Delete_Existing_Simulation();
            var simulation = JObject.Parse(@"{   
                'Enabled': true, 
                'DeviceModels': [   
                    {   
                        'Id': 'truck-01', 
                        'Count': 5 
                    } 
                ]
            }");

            //Act
            var request = new HttpRequest(Constants.DS_ADDRESS + "/simulations");
            request.SetContent(simulation);
            var response = this.httpClient.PostAsync(request).Result;

            //Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Thread.Sleep(Constants.WAIT_TIME);
            RegistryManager registry = RegistryManager.CreateFromConnectionString(IOTHUB_CONNECTION_STRING);
            Twin deviceTwin = await registry.GetTwinAsync("truck-01.0");
            Assert.True(deviceTwin != null && deviceTwin.Tags["IsSimulated"] == 'Y');
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public async void Should_Upgrade_Firmware_On_SimulatedDevice()
        {
            //Arrange
            this.Should_Delete_Existing_Simulation();
            var simulation = JObject.Parse(@"{   
                'Enabled': true, 
                'DeviceModels': [   
                    {   
                        'Id': 'truck-01', 
                        'Count': 5 
                    } 
                ]
            }");
            var request = new HttpRequest(Constants.DS_ADDRESS + "/simulations");
            request.SetContent(simulation);
            var response = this.httpClient.PostAsync(request).Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Thread.Sleep(Constants.WAIT_TIME);

            //Act
            RegistryManager registry = RegistryManager.CreateFromConnectionString(IOTHUB_CONNECTION_STRING);
            Twin deviceTwin = await registry.GetTwinAsync("truck-01.0");
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(IOTHUB_CONNECTION_STRING);
            Task<CloudToDeviceMethodResult> firmwareUpgradeMethodResult = serviceClient.InvokeDeviceMethodAsync("truck-01.0",
                new CloudToDeviceMethod("FirmwareUpdate"));
            Thread.Sleep(Constants.WAIT_TIME);
            CloudToDeviceMethodResult firmwareUpdateResponse = await firmwareUpgradeMethodResult.ConfigureAwait(false);

            //Assert
            Twin updatedDevice = await registry.GetTwinAsync("truck-01.0");
            Assert.True(updatedDevice.Properties.Reported.Contains("FirmwareUpdateStatus"));
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public async void Should_Reboot_SimulatedDevice()
        {
            //Arrange
            this.Should_Delete_Existing_Simulation();
            var simulation = JObject.Parse(@"{   
                'Enabled': true, 
                'DeviceModels': [   
                    {   
                        'Id': 'chiller-01', 
                        'Count': 5 
                    } 
                ]
            }");
            var request = new HttpRequest(Constants.DS_ADDRESS + "/simulations");
            request.SetContent(simulation);
            var response = this.httpClient.PostAsync(request).Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Thread.Sleep(Constants.WAIT_TIME);

            //Act
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(IOTHUB_CONNECTION_STRING);
            Task<CloudToDeviceMethodResult> rebootMethodResult = serviceClient.InvokeDeviceMethodAsync("chiller-01.0",
                new CloudToDeviceMethod("Reboot"));
            CloudToDeviceMethodResult rebootResponse = await rebootMethodResult.ConfigureAwait(false);

            //Assert
            Assert.Equal(200, rebootResponse.Status);
            //TODO
            //Currently, there is no property of the device that indicates a reboot happened. We have to handle this in the future. 
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
            this.Should_Delete_Existing_Simulation();
            var simulation = JObject.Parse(@"{   
                'Enabled': true, 
                'DeviceModels': [   
                    {   
                        'Id': 'truck-01', 
                        'Count': 5 
                    } 
                ]
            }");
            var createSimulationRequest = new HttpRequest(Constants.DS_ADDRESS + "/simulations");
            createSimulationRequest.SetContent(simulation);
            var createSimulationResponse = this.httpClient.PostAsync(createSimulationRequest).Result;
            Assert.Equal(HttpStatusCode.OK, createSimulationResponse.StatusCode);

            var currentSimulationRequest = new HttpRequest(Constants.DS_ADDRESS + "/simulations/1");
            var currentSimulationResponse = this.httpClient.GetAsync(currentSimulationRequest).Result;
            Assert.Equal(HttpStatusCode.OK, currentSimulationResponse.StatusCode);
            JObject JsonResponse = JObject.Parse(currentSimulationResponse.Content);

            string ETag = (string)JsonResponse["ETag"];
            return ETag;
        }

        private void Should_Delete_Existing_Simulation()
        {
            var runningSimulationRequest = new HttpRequest(Constants.DS_ADDRESS + "/simulations/1");
            var runningSimulationResponse = this.httpClient.GetAsync(runningSimulationRequest).Result;
            
            //delete current simulation only if it exists
            if(runningSimulationResponse.StatusCode == HttpStatusCode.OK)
            {
                var deleteCurrentSimulationRequest = new HttpRequest(Constants.DS_ADDRESS + "/simulations/1");
                var deleteCurrentSimulationResponse = this.httpClient.DeleteAsync(deleteCurrentSimulationRequest).Result;
                Assert.Equal(HttpStatusCode.OK, deleteCurrentSimulationResponse.StatusCode);
                
                var getCurrentSimulationRequest = new HttpRequest(Constants.DS_ADDRESS + "/simulations/1");
                var getCurrentSimulationResponse = this.httpClient.GetAsync(getCurrentSimulationRequest).Result;
                Assert.Equal(HttpStatusCode.NotFound, getCurrentSimulationResponse.StatusCode);
            }
            
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
