using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Helpers;
using Helpers.Http;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DeviceSimulation
{
    public class SimulationTests
    {
        private readonly IHttpClient httpClient;
        private readonly string IOTHUB_CONNECTION_STRING;
        public SimulationTests()
        {
            var config = new ConfigurationBuilder()
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
            var response = this.httpClient.GetAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        /// <summary>
        /// Simulation service should return a list of simulations
        /// </summary>
        [Fact]
        public void Should_Get_A_List_Of_Simulations()
        {
            // Act
            var request = new HttpRequest(Constants.DS_ADDRESS + "/simulations");
            var response = this.httpClient.GetAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            JObject jsonResponse = JObject.Parse(response.Content);

            Assert.True(jsonResponse.HasValues);

            JArray items = (JArray)jsonResponse["Items"];

            Assert.True(items.Count >= 0);
        }

        /// <summary>
        /// Simulation service should return default simulation
        /// </summary>
        [Fact]
        public void Should_Get_Default_Simulation()
        {
            // Arrange
            const string DEFAULT_SIMULATION_ID = "1";
            var request = new HttpRequest(Constants.DS_ADDRESS + "/simulations/1");

            // Act
            var response = this.httpClient.GetAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            JObject jsonResponse = JObject.Parse(response.Content);
            JToken jId = jsonResponse["Id"];
            JToken jEtag = jsonResponse["ETag"];
            JToken jDeviceModels = jsonResponse["DeviceModels"];
            JToken jEnabled = jsonResponse["Enabled"];

            string id = (string)jId;
            string etag = (string)jEtag;
            bool enabled = (bool)jEnabled;

            Assert.True(jsonResponse.HasValues);
            Assert.Equal(DEFAULT_SIMULATION_ID, id);
            Assert.False(string.IsNullOrEmpty(etag));
            Assert.True(jDeviceModels.HasValues);
            Assert.False(enabled);
        }

        /// <summary>
        /// Simulation service should able to create a simulation
        /// </summary>
        [Fact]
        public void Should_Create_A_Simulation()
        {
            // Arrage
            var simulation = JObject.Parse(@"{  
                'ETag': 'etag',
                'Enabled': false,
                'Name': 'simulation test',
                'DeviceModels': [  
                    {  
                        'Id': 'model_1',
                        'Count': 150
                    }
                ],
                'IoTHubs': []
            }");
            var request = new HttpRequest(Constants.DS_ADDRESS + "/simulations");
            request.AddHeader("Content-Type", "application/json");
            request.SetContent(simulation);

            // Act
            var response = this.httpClient.PostAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            JObject jsonResponse = JObject.Parse(response.Content);

            Assert.True(jsonResponse.HasValues);

            foreach (KeyValuePair<string, JToken> sourceProperty in simulation)
            {
                JProperty targetProp = jsonResponse.Property(sourceProperty.Key);

                if ((string)sourceProperty.Key == "ETag")
                {
                    // The Etag will be changed with success upsert/insert
                    Assert.False(JToken.DeepEquals(sourceProperty.Value, targetProp.Value));
                }
                else
                {
                    Assert.True(JToken.DeepEquals(sourceProperty.Value, targetProp.Value));
                }
            }
        }

        /// <summary>
        /// Simulation service should able to retrieve a simulation by id
        /// </summary>
        [Fact]
        public void Should_Get_A_Simulation_By_Id()
        {
            // Arrage
            var simulation = JObject.Parse(@"{  
                'ETag': 'etag',
                'Enabled': false,
                'Name': 'simulation test',
                'DeviceModels': [  
                    {  
                        'Id': 'model_1',
                        'Count': 150
                    }
                ],
                'IoTHub': {
                    ConnectionString: 'default'
                }
            }");
            IHttpResponse postResponse = this.CreateSimulation(simulation);
            JObject postJsonResponse = JObject.Parse(postResponse.Content);
            string id = (string)postJsonResponse["Id"];

            // Act
            var request = new HttpRequest(Constants.DS_ADDRESS + $"/simulations/{id}");
            var response = this.httpClient.GetAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            JObject jsonResponse = JObject.Parse(response.Content);

            Assert.True(jsonResponse.HasValues);

            foreach (KeyValuePair<string, JToken> sourceProperty in postJsonResponse)
            {
                JProperty targetProp = jsonResponse.Property(sourceProperty.Key);
                Assert.True(JToken.DeepEquals(sourceProperty.Value, targetProp.Value));
            }
        }

        /// <summary>
        /// Simulation service should able to upsert a simulation by id
        /// </summary>
        [Fact]
        public void Should_Upsert_A_Simulation_By_Id()
        {
            // Arrage
            var simulation = JObject.Parse(@"{  
                'ETag': 'etag',
                'Enabled': false,
                'Name': 'simulation test',
                'DeviceModels': [  
                    {  
                        'Id': 'model_1',
                        'Count': 150
                    }
                ],
                'IoTHub': {
                    ConnectionString: 'default'
                }
            }");
            IHttpResponse postResponse = this.CreateSimulation(simulation);
            JObject postJsonResponse = JObject.Parse(postResponse.Content);
            string id = (string)postJsonResponse["Id"];

            // Update simulation
            simulation["ETag"] = (string)postJsonResponse["ETag"];
            simulation["Name"] = "Updated Name";
            simulation["StartTime"] = "NOW";
            simulation["EndTime"] = "NOW+PT2H";
            simulation["DeviceModels"] = @"[  
                {
                    'Id': 'model_2',
                    'Count': 250
                }
            ]";

            IHttpResponse upsertResponse = this.CreateSimulation(simulation);
            JObject upsertJsonResponse = JObject.Parse(postResponse.Content);

            // Act
            var request = new HttpRequest(Constants.DS_ADDRESS + $"/simulations/{id}");
            var response = this.httpClient.GetAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            JObject jsonResponse = JObject.Parse(response.Content);

            Assert.True(jsonResponse.HasValues);

            foreach (KeyValuePair<string, JToken> sourceProperty in upsertJsonResponse)
            {
                JProperty targetProp = jsonResponse.Property(sourceProperty.Key);
                Assert.True(JToken.DeepEquals(sourceProperty.Value, targetProp.Value));
            }
        }

        /// <summary>
        /// Simulation service should return Conflict when etag doesn't match 
        /// in upserting a simulation
        /// </summary>
        [Fact]
        public void Should_Return_Conflict_When_ETag_Does_Not_Match_In_Upserting()
        {
            // Arrage
            var simulation = JObject.Parse(@"{  
                'ETag': 'etag',
                'Enabled': false,
                'Name': 'simulation test',
                'DeviceModels': [  
                    {  
                        'Id': 'model_1',
                        'Count': 150
                    }
                ],
                'IoTHub': {
                    ConnectionString: 'default'
                }
            }");
            IHttpResponse postResponse = this.CreateSimulation(simulation);
            JObject postJsonResponse = JObject.Parse(postResponse.Content);
            string id = (string)postJsonResponse["Id"];

            IHttpResponse upsertResponse = this.CreateSimulation(simulation);
            JObject upsertJsonResponse = JObject.Parse(postResponse.Content);

            // Act
            var request = new HttpRequest(Constants.DS_ADDRESS + $"/simulations/{id}");
            request.AddHeader("Content-Type", "application/json");
            request.SetContent(simulation);
            var response = this.httpClient.PutAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        /// <summary>
        /// Simulation service should return bad request when failed to provide
        /// valid start time and end time
        /// </summary>
        [Fact]
        public void Should_Return_Badrequest_With_Invalid_StartTime_And_EndTime()
        {
            // Arrage
            var endTimeBeforeStartTime = JObject.Parse(@"{  
                'ETag': 'etag',
                'Enabled': false,
                'Name': 'simulation test',
                'StartTime': 'Now+P1D',
                'EndTime': 'Now',
                'DeviceModels': [
                    {  
                        'Id': 'model_1',
                        'Count': 150
                    }
                ],
                'IoTHubs': [{
                    ConnectionString: 'default'
                }]
            }");

            var invalidTimeFormat = JObject.Parse(@"{  
                'ETag': 'etag',
                'Enabled': false,
                'Name': 'simulation test',
                'StartTime': 'invalid time',
                'EndTime': 'invalid time',
                'DeviceModels': [
                    {  
                        'Id': 'model_1',
                        'Count': 150
                    }
                ],
                'IoTHubs': [{
                    ConnectionString: 'default'
                }]
            }");

            // Act
            var response = this.CreateSimulation(endTimeBeforeStartTime);
            var response_1 = this.CreateSimulation(invalidTimeFormat);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response_1.StatusCode);
        }

        /// <summary>
        /// Simulation service should return bad request when failed to provide
        /// valid device models
        /// </summary>
        [Fact]
        public void Should_Return_Badrequest_With_Invalid_DeviceModels()
        {
            // Arrage
            var zeroDeviceModels = JObject.Parse(@"{  
                'ETag': 'etag',
                'Enabled': false,
                'Name': 'simulation test',
                'DeviceModels': [],
                'IoTHubs': [{
                    ConnectionString: 'default'
                }]
            }");

            var totalDeviceCountIsZero = JObject.Parse(@"{  
                'ETag': 'etag',
                'Enabled': false,
                'Name': 'simulation test',
                'DeviceModels': [
                    {
                        'Id': 'model_1',
                        'Count': 0
                    },
                    {
                        'Id': 'model_2',
                        'Count': 0
                    }
                ],
                'IoTHubs': [{
                    ConnectionString: 'default'
                }]
            }");

            // Act
            var response = this.CreateSimulation(zeroDeviceModels);
            var response_1 = this.CreateSimulation(totalDeviceCountIsZero);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response_1.StatusCode);
        }

        /// <summary>
        /// Simulation service should return bad request when failed to provide
        /// valid iothub
        /// </summary>
        [Fact]
        public void Should_Return_Badrequest_With_Invalid_IotHub()
        {
            // Arrage
            var invalidSimulation = JObject.Parse(@"{  
                'ETag': 'etag',
                'Enabled': false,
                'Name': 'simulation test',
                'DeviceModels': [
                    {  
                        'Id': 'model_1',
                        'Count': 150
                    }
                ],
                'IoTHubs': [{
                    ConnectionString: 'invalid string'
                }]
            }");

            // Act
            var response = this.CreateSimulation(invalidSimulation);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        /// <summary>
        /// Simulation service should able to delete a simulation by id
        /// </summary>
        [Fact]
        public void Should_Delete_A_Simulation_By_Id()
        {
            // Arrage
            var simulation = JObject.Parse(@"{  
                'ETag': 'etag',
                'Enabled': false,
                'Name': 'simulation test',
                'DeviceModels': [  
                    {  
                        'Id': 'chiller-01',
                        'Count': 1
                    }
                ],
                'IoTHubs': [{
                    ConnectionString: 'default'
                }]
            }");
            IHttpResponse postResponse = this.CreateSimulation(simulation);
            JObject postJsonResponse = JObject.Parse(postResponse.Content);
            string id = (string)postJsonResponse["Id"];

            // Act
            var request = new HttpRequest(Constants.DS_ADDRESS + $"/simulations/{id}");
            var response = this.httpClient.DeleteAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public void Should_Return_All_Simulations()
        {
            //Arrange

            //Act
            var getSimulationsRequest = new HttpRequest(Constants.SIMULATIONS_URL);
            var getSimulationsResponse = this.httpClient.GetAsync(getSimulationsRequest).Result;

            //Assert
            Assert.Equal(HttpStatusCode.OK, getSimulationsResponse.StatusCode);

            JObject jsonResponse = JObject.Parse(getSimulationsResponse.Content);
            Assert.True(jsonResponse.Count > 0);
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public void Should_Return_Currently_Running_Simulation()
        {
            //Arrange
            this.Should_Start_Given_Simulation();

            //Act
            var getCurrentSimulationRequest = new HttpRequest(Constants.DEFAULT_SIMULATION_URL);
            var getCurrentSimulationResponse = this.httpClient.GetAsync(getCurrentSimulationRequest).Result;

            //Assert
            Assert.Equal(HttpStatusCode.OK, getCurrentSimulationResponse.StatusCode);

            JObject jsonResponse = JObject.Parse(getCurrentSimulationResponse.Content);
            Assert.True((bool)jsonResponse["Enabled"]);
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public void Should_Start_Given_Simulation()
        {
            //Arrange
            string ETag = this.Get_ETag_Of_Running_Simulation();

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

            var verificationRequest = new HttpRequest(Constants.DEFAULT_SIMULATION_URL);
            var verificationResponse = this.httpClient.GetAsync(verificationRequest).Result;
            JObject jsonResponse = JObject.Parse(verificationResponse.Content);

            Assert.True((bool)jsonResponse["Enabled"]);
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public void Should_Stop_Given_Simulation()
        {
            //Arrange
            string ETag = this.Get_ETag_Of_Running_Simulation();

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

            var verificationRequest = new HttpRequest(Constants.DEFAULT_SIMULATION_URL);
            var verificationResponse = this.httpClient.GetAsync(verificationRequest).Result;
            JObject jsonResponse = JObject.Parse(verificationResponse.Content);

            Assert.False((bool)jsonResponse["Enabled"]);
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public void Should_Create_Default_Simulation()
        {
            //Arrange
            this.Try_Delete_Existing_Simulation();
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
            var request = new HttpRequest(Constants.SIMULATIONS_URL + "?template=default");
            request.SetContent(simulation);
            var response = this.httpClient.PostAsync(request).Result;

            //Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var getCurrentSimulationRequest = new HttpRequest(Constants.DEFAULT_SIMULATION_URL);
            var getCurrentSimulationResponse = this.httpClient.GetAsync(getCurrentSimulationRequest).Result;
            JObject jsonResponse = JObject.Parse(getCurrentSimulationResponse.Content);

            Assert.Equal(HttpStatusCode.OK, getCurrentSimulationResponse.StatusCode);
            Assert.True((bool)jsonResponse["Enabled"]);
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public async void Should_Create_SimulatedDevice()
        {
            //Arrange
            Thread.Sleep(Constants.WAIT_TIME);
            this.Try_Delete_Existing_Simulation();
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
            var request = new HttpRequest(Constants.SIMULATIONS_URL + "?template=default");
            request.SetContent(simulation);
            var response = this.httpClient.PostAsync(request).Result;

            //Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Thread.Sleep(Constants.WAIT_TIME);
            RegistryManager registry = RegistryManager.CreateFromConnectionString(this.IOTHUB_CONNECTION_STRING);
            Twin deviceTwin = await registry.GetTwinAsync("truck-01.0");
            Assert.True(deviceTwin != null);
            Assert.True(deviceTwin.Tags["IsSimulated"] == 'Y');
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public async void Should_Upgrade_Firmware_On_SimulatedDevice()
        {
            //Arrange
            Thread.Sleep(Constants.WAIT_TIME);
            this.Try_Delete_Existing_Simulation();
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
            var request = new HttpRequest(Constants.SIMULATIONS_URL + "?template=default");
            request.SetContent(simulation);
            var response = this.httpClient.PostAsync(request).Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Thread.Sleep(Constants.WAIT_TIME);

            //Act
            RegistryManager registry = RegistryManager.CreateFromConnectionString(this.IOTHUB_CONNECTION_STRING);
            Twin deviceTwin = await registry.GetTwinAsync("truck-01.0");
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(this.IOTHUB_CONNECTION_STRING);
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
            Thread.Sleep(Constants.WAIT_TIME);
            this.Try_Delete_Existing_Simulation();
            var simulation = JObject.Parse(@"{   
                'Enabled': true, 
                'DeviceModels': [   
                    {   
                        'Id': 'chiller-01', 
                        'Count': 5 
                    } 
                ]
            }");

            //Act
            var request = new HttpRequest(Constants.SIMULATIONS_URL + "?template=default");
            request.SetContent(simulation);
            var response = this.httpClient.PostAsync(request).Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Thread.Sleep(Constants.WAIT_TIME);

            //Act
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(this.IOTHUB_CONNECTION_STRING);
            Task<CloudToDeviceMethodResult> rebootMethodResult = serviceClient.InvokeDeviceMethodAsync("chiller-01.0",
                new CloudToDeviceMethod("Reboot"));
            CloudToDeviceMethodResult rebootResponse = await rebootMethodResult.ConfigureAwait(false);

            //Assert
            Assert.Equal(200, rebootResponse.Status);
            //TODO
            //Currently, there is no property of the device that indicates a reboot happened. We have to handle this in the future. 
        }

        private void Try_Delete_Existing_Simulation()
        {
            this.Should_Start_Given_Simulation();
            var runningSimulationRequest = new HttpRequest(Constants.DEFAULT_SIMULATION_URL);
            var runningSimulationResponse = this.httpClient.GetAsync(runningSimulationRequest).Result;

            //delete current simulation only if it exists
            if (runningSimulationResponse.StatusCode == HttpStatusCode.OK)
            {
                var deleteCurrentSimulationRequest = new HttpRequest(Constants.DEFAULT_SIMULATION_URL );
                var deleteCurrentSimulationResponse = this.httpClient.DeleteAsync(deleteCurrentSimulationRequest).Result;
                Assert.Equal(HttpStatusCode.OK, deleteCurrentSimulationResponse.StatusCode);

                var getCurrentSimulationRequest = new HttpRequest(Constants.DEFAULT_SIMULATION_URL );
                var getCurrentSimulationResponse = this.httpClient.GetAsync(getCurrentSimulationRequest).Result;
                Assert.Equal(HttpStatusCode.NotFound, getCurrentSimulationResponse.StatusCode);
            }
        }

        private string Get_ETag_Of_Running_Simulation()
        {
            var currentSimulationRequest = new HttpRequest(Constants.DEFAULT_SIMULATION_URL);
            var currentSimulationResponse = this.httpClient.GetAsync(currentSimulationRequest).Result;
            Assert.Equal(HttpStatusCode.OK, currentSimulationResponse.StatusCode);
            JObject jsonResponse = JObject.Parse(currentSimulationResponse.Content);

            return (string)jsonResponse["ETag"];
        }

        public HttpWebRequest Create_Simulation_Request(byte[] simulationContentByteArray)
        {
            var startSimulationRequest = (HttpWebRequest)WebRequest.Create(Constants.DEFAULT_SIMULATION_URL);
            startSimulationRequest.Method = "PATCH";
            startSimulationRequest.ContentLength = simulationContentByteArray.Length;
            startSimulationRequest.ContentType = "application/json";
            return startSimulationRequest;
        }

        private IHttpResponse CreateSimulation(JObject simulation)
        {

            var request = new HttpRequest(Constants.DS_ADDRESS + "/simulations");
            request.SetContent(simulation);

            return this.httpClient.PostAsync(request).Result;
        }
    }
}
