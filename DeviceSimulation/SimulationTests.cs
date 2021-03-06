﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Helpers;
using Helpers.Http;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DeviceSimulation
{
    public class SimulationTests
    {
        private readonly IHttpClient httpClient;

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
            var request = new HttpRequest(Constants.DS_ADDRESS + "/status");
            var response = this.httpClient.GetAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        /// <summary>
        /// Simulation service should return a list of simulations
        /// </summary>
        [Fact, Trait("Type", "IntegrationTest")]
        public void Should_Get_A_List_Of_Simulations()
        {
            // Act
            var request = new HttpRequest(Constants.SIMULATIONS_URL);
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
        [Fact, Trait("Type", "IntegrationTest")]
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
        }

        /// <summary>
        /// Simulation service should able to create a simulation
        /// </summary>
        [Fact, Trait("Type", "IntegrationTest")]
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
                'IoTHubs': [
                  {
                    'ConnectionString': 'default'
                  }
                ]
            }");
            var request = new HttpRequest(Constants.SIMULATIONS_URL);
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

                if (sourceProperty.Key == "ETag")
                {
                    // The Etag will be changed with success upsert/insert
                    Assert.False(JToken.DeepEquals(sourceProperty.Value, targetProp.Value));
                }
                else if (sourceProperty.Key == "IoTHubs")
                {
                    Assert.True(targetProp.Value.Children().Count() == 1);
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
        [Fact, Trait("Type", "IntegrationTest")]
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
                'IoTHubs': [
                  {
                    'ConnectionString': 'default'
                  }
                ]
            }");
            IHttpResponse postResponse = this.CreateSimulation(simulation);
            JObject postJsonResponse = JObject.Parse(postResponse.Content);
            string id = (string)postJsonResponse["Id"];

            // Act
            var request = new HttpRequest(Constants.SIMULATIONS_URL + $"/{id}");
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
        [Fact, Trait("Type", "IntegrationTest")]
        public void Should_Upsert_A_Simulation_By_Id()
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
                'IoTHubs': [
                  {
                    ConnectionString: 'default'
                  }
                ]
            }");
            IHttpResponse postResponse = this.CreateSimulation(simulation);
            JObject postJsonResponse = JObject.Parse(postResponse.Content);
            string id = (string)postJsonResponse["Id"];
            string eTag = (string)postJsonResponse["ETag"];

            // Update simulation

            var upsertSimulation = JObject.Parse(@"{  
                'Enabled': false,
                'Name': 'simulation test updated',
                'DeviceModels': [  
                    {
                        'Id': 'chiller-02',
                        'Count': 2
                    }
                ],
                'IoTHubs': [
                  {
                    ConnectionString: 'default'
                  }
                ]
            }");
            upsertSimulation["ETag"] = eTag;

            IHttpResponse upsertResponse = this.UpsertSimulation(id, upsertSimulation);

            // Assert
            Assert.Equal(HttpStatusCode.OK, upsertResponse.StatusCode);

            JObject jsonResponse = JObject.Parse(upsertResponse.Content);
            Assert.True(jsonResponse.HasValues);

            foreach (KeyValuePair<string, JToken> sourceProperty in upsertSimulation)
            {
                if (sourceProperty.Key != "IoTHubs" && sourceProperty.Key != "ETag")
                {
                    JProperty targetProp = jsonResponse.Property(sourceProperty.Key);
                    Assert.True(JToken.DeepEquals(sourceProperty.Value, targetProp.Value));
                }
            }
        }

        /// <summary>
        /// Simulation service should return Conflict when etag doesn't match 
        /// in upserting a simulation
        /// </summary>
        [Fact, Trait("Type", "IntegrationTest")]
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
                'IoTHubs': [
                  {
                    'ConnectionString': 'default'
                  }
                ]
            }");
            IHttpResponse postResponse = this.CreateSimulation(simulation);
            JObject postJsonResponse = JObject.Parse(postResponse.Content);
            string id = (string)postJsonResponse["Id"];

            IHttpResponse upsertResponse = this.CreateSimulation(simulation);
            JObject upsertJsonResponse = JObject.Parse(postResponse.Content);

            // Act
            var request = new HttpRequest(Constants.SIMULATIONS_URL + $"/{id}");
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
        [Fact, Trait("Type", "IntegrationTest")]
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
                'IoTHubs': [
                  {
                    ConnectionString: 'default'
                  }
                ]
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
                'IoTHubs': [
                  {
                    ConnectionString: 'default'
                  }
                ]
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
        [Fact, Trait("Type", "IntegrationTest")]
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
        [Fact, Trait("Type", "IntegrationTest")]
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
        [Fact(Skip = "delete devices"), Trait("Type", "IntegrationTest")]
        public void Should_Delete_A_Simulation_By_Id()
        {
            // Arrage
            var simulation = JObject.Parse(@"{
                'Enabled': false,
                'Name': 'simulation test',
                'DeviceModels': [  
                    {  
                        'Id': 'engine-01',
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
            
            Thread.Sleep(Constants.WAIT_TIME);

            // Act
            var request = new HttpRequest(Constants.SIMULATIONS_URL + $"/{id}");
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
            //Act
            var request = new HttpRequest(Constants.SIMULATIONS_URL + "?template=default");
            request.SetContent("");
            var response = this.httpClient.PostAsync(request).Result;

            //Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var getDefaultSimulationRequest = new HttpRequest(Constants.DEFAULT_SIMULATION_URL);
            var getDefaultSimulationResponse = this.httpClient.GetAsync(getDefaultSimulationRequest).Result;

            Assert.Equal(HttpStatusCode.OK, getDefaultSimulationResponse.StatusCode);
            JObject defaultSimulationResponse = JObject.Parse(getDefaultSimulationResponse.Content);

            foreach (KeyValuePair<string, JToken> sourceProperty in defaultSimulationResponse)
            {
                JProperty targetProp = defaultSimulationResponse.Property(sourceProperty.Key);
                Assert.True(JToken.DeepEquals(sourceProperty.Value, targetProp.Value));
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

            var request = new HttpRequest(Constants.SIMULATIONS_URL);
            request.SetContent(simulation);

            return this.httpClient.PostAsync(request).Result;
        }

        private IHttpResponse UpsertSimulation(string id, JObject simulation)
        {

            var request = new HttpRequest(Constants.SIMULATIONS_URL + $"/{id}");
            request.SetContent(simulation);

            return this.httpClient.PutAsync(request).Result;
        }
    }
}
