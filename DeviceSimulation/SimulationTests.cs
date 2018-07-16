using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Helpers.Http;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DeviceSimulation
{
    public class SimulationTests
    {
        private readonly IHttpClient httpClient;
        private readonly RegistryManager registry;
        private const string DS_ADDRESS = "http://localhost:9003/v1";
        private const string IOTHUB_CONNECTION_STRING = "HostName=SaiDeviceSimulation.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=+dqkraJnTFZjEylsZLRfJCh3Jo8DhOpQ6NOoCAlSWCE=";
        private const string SIMULATION_URL = "http://localhost:9003/v1/simulations/1";
        private const int WAIT_TIME = 15000;
        public SimulationTests()
        {
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
            var request = new HttpRequest(DS_ADDRESS + "/status");
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
            var createSimulationRequest = new HttpRequest(DS_ADDRESS + "/simulations");
            createSimulationRequest.SetContent(simulation);
            var createSimulationResponse = this.httpClient.PostAsync(createSimulationRequest).Result;
            Assert.Equal(HttpStatusCode.OK, createSimulationResponse.StatusCode);

            //Act
            var getCurrentSimulationRequest = new HttpRequest(DS_ADDRESS + "/simulations/1");
            var getCurrentSimulationResponse = this.httpClient.GetAsync(getCurrentSimulationRequest).Result;

            //Assert
            Assert.Equal(HttpStatusCode.OK, getCurrentSimulationResponse.StatusCode);

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
            var request = new HttpRequest(DS_ADDRESS + "/simulations");
            request.SetContent(simulation);
            var response = this.httpClient.PostAsync(request).Result;

            //Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var getCurrentSimulationRequest = new HttpRequest(DS_ADDRESS + "/simulations/1");
            var getCurrentSimulationResponse = this.httpClient.GetAsync(getCurrentSimulationRequest).Result;
            Assert.Equal(HttpStatusCode.OK, getCurrentSimulationResponse.StatusCode);
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
            var request = new HttpRequest(DS_ADDRESS + "/simulations");
            request.SetContent(simulation);
            var response = this.httpClient.PostAsync(request).Result;

            //Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Thread.Sleep(WAIT_TIME);
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
            var request = new HttpRequest(DS_ADDRESS + "/simulations");
            request.SetContent(simulation);
            var response = this.httpClient.PostAsync(request).Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Thread.Sleep(WAIT_TIME);

            //Act
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(IOTHUB_CONNECTION_STRING);
            Task<CloudToDeviceMethodResult> firmwareUpgradeMethodResult = serviceClient.InvokeDeviceMethodAsync("truck-01.0",
                new CloudToDeviceMethod("FirmwareUpdate"));
            CloudToDeviceMethodResult firmwareUpdateResponse = await firmwareUpgradeMethodResult.ConfigureAwait(false);

            //Assert
            Assert.Equal(200, firmwareUpdateResponse.Status);
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
            var request = new HttpRequest(DS_ADDRESS + "/simulations");
            request.SetContent(simulation);
            var response = this.httpClient.PostAsync(request).Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Thread.Sleep(WAIT_TIME);

            //Act
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(IOTHUB_CONNECTION_STRING);
            Task<CloudToDeviceMethodResult> rebootMethodResult = serviceClient.InvokeDeviceMethodAsync("chiller-01.0",
                new CloudToDeviceMethod("Reboot"));
            CloudToDeviceMethodResult rebootResponse = await rebootMethodResult.ConfigureAwait(false);

            //Assert
            Assert.Equal(200, rebootResponse.Status);
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public void Should_Start_Given_Simulation()
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
            var createSimulationRequest = new HttpRequest(DS_ADDRESS + "/simulations");
            createSimulationRequest.SetContent(simulation);
            var createSimulationResponse = this.httpClient.PostAsync(createSimulationRequest).Result;
            Assert.Equal(HttpStatusCode.OK, createSimulationResponse.StatusCode);

            var currentSimulationRequest = new HttpRequest(DS_ADDRESS + "/simulations/1");
            var currentSimulationResponse = this.httpClient.GetAsync(currentSimulationRequest).Result;
            Assert.Equal(HttpStatusCode.OK, currentSimulationResponse.StatusCode);
            JObject JsonResponse = JObject.Parse(currentSimulationResponse.Content);

            string ETag = (string)JsonResponse["ETag"];

            var simulationContent = "{" + $"'ETag': '{ETag}' ,'Enabled': true" + "}";
            var simulationContentByteArray = Encoding.ASCII.GetBytes(simulationContent);

            //Act
            var startSimulationRequest = (HttpWebRequest)WebRequest.Create("http://localhost:9003/v1/simulations/1");
            startSimulationRequest.Method = "PATCH";
            startSimulationRequest.ContentLength = simulationContentByteArray.Length;
            startSimulationRequest.ContentType = "application/json";
            Stream dataStream = startSimulationRequest.GetRequestStream();
            dataStream.Write(simulationContentByteArray, 0, simulationContentByteArray.Length);
            dataStream.Close();
            var startSimulationResponse = (HttpWebResponse)startSimulationRequest.GetResponse();

            //Assert
            Assert.Equal(HttpStatusCode.OK, startSimulationResponse.StatusCode);

            var verificationRequest = new HttpRequest(DS_ADDRESS + "/simulations/1");
            var verificationResponse = this.httpClient.GetAsync(verificationRequest).Result;
            JObject jsonResponse = JObject.Parse(verificationResponse.Content);

            Assert.True((bool)jsonResponse["Enabled"]);
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public void Should_Stop_Given_Simulation()
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
            var createSimulationRequest = new HttpRequest(DS_ADDRESS + "/simulations");
            createSimulationRequest.SetContent(simulation);
            var createSimulationResponse = this.httpClient.PostAsync(createSimulationRequest).Result;
            Assert.Equal(HttpStatusCode.OK, createSimulationResponse.StatusCode);
            var currentSimulationRequest = new HttpRequest(DS_ADDRESS + "/simulations/1");
            var currentSimulationResponse = this.httpClient.GetAsync(currentSimulationRequest).Result;
            Assert.Equal(HttpStatusCode.OK, currentSimulationResponse.StatusCode);
            JObject JsonResponse = JObject.Parse(currentSimulationResponse.Content);

            string ETag = (string)JsonResponse["ETag"];

            var simulationContent = "{" + $"'ETag': '{ETag}' ,'Enabled': false" + "}";
            var simulationContentByteArray = Encoding.ASCII.GetBytes(simulationContent);

            //Act
            var startSimulationRequest = (HttpWebRequest)WebRequest.Create(SIMULATION_URL);
            startSimulationRequest.Method = "PATCH";
            startSimulationRequest.ContentLength = simulationContentByteArray.Length;
            startSimulationRequest.ContentType = "application/json";
            Stream dataStream = startSimulationRequest.GetRequestStream();
            dataStream.Write(simulationContentByteArray, 0, simulationContentByteArray.Length);
            dataStream.Close();
            var startSimulationResponse = (HttpWebResponse)startSimulationRequest.GetResponse();

            //Assert
            Assert.Equal(HttpStatusCode.OK, startSimulationResponse.StatusCode);

            var verificationRequest = new HttpRequest(DS_ADDRESS + "/simulations/1");
            var verificationResponse = this.httpClient.GetAsync(verificationRequest).Result;
            JObject jsonResponse = JObject.Parse(verificationResponse.Content);

            Assert.False((bool)jsonResponse["Enabled"]);
        }

        private void Should_Delete_Existing_Simulation()
        {
            var runningSimulationRequest = new HttpRequest(DS_ADDRESS + "/simulations/1");
            var runningSimulationResponse = this.httpClient.GetAsync(runningSimulationRequest).Result;
            if(runningSimulationResponse.StatusCode == HttpStatusCode.OK)
            {
                var deleteCurrentSimulationRequest = new HttpRequest(DS_ADDRESS + "/simulations/1");
                var deleteCurrentSimulationResponse = this.httpClient.DeleteAsync(deleteCurrentSimulationRequest).Result;
                Assert.Equal(HttpStatusCode.OK, deleteCurrentSimulationResponse.StatusCode);
                
                var getCurrentSimulationRequest = new HttpRequest(DS_ADDRESS + "/simulations/1");
                var getCurrentSimulationResponse = this.httpClient.GetAsync(getCurrentSimulationRequest).Result;
                Assert.Equal(HttpStatusCode.NotFound, getCurrentSimulationResponse.StatusCode);
            }
            
        }
    }
}
