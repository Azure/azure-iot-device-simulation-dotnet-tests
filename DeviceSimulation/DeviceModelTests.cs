// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Net;
using Helpers.Http;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DeviceSimulation
{
    public class DeviceModelTests
    {
        private readonly IHttpClient httpClient;
        private const string DS_ADDRESS = "http://127.0.0.1:9003/v1";

        public DeviceModelTests()
        {
            this.httpClient = new HttpClient();
        }

        /// <summary>
        /// Simulation service should return a list of device models
        /// </summary>
        [Fact]
        public void Should_Return_A_List_Of_DeviceModels()
        {
            // Arrange
            const int STOCKMODELS = 10;

            // Act
            var request = new HttpRequest(DS_ADDRESS + "/devicemodels");
            request.AddHeader("X-Foo", "Bar");
            var response = this.httpClient.GetAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            JObject jsonResponse = JObject.Parse(response.Content);

            Assert.True(jsonResponse.HasValues);

            JArray items = (JArray)jsonResponse["Items"];

            Assert.True(items.Count >= STOCKMODELS);
        }

        /// <summary>
        /// Simulation service should able to create a device model
        /// </summary>
        [Fact]
        public void Should_Create_A_Custom_DeviceModel()
        {
            // Arrage
            var deviceModel = JObject.Parse(@"{  
                'ETag': 'etag',
                'Name': 'chiller',
                'Protocol': 'MQTT',
                'Type': 'Custom',
                'Telemetry': [  
                    {  
                        'Interval': '00:00:10',
                        'MessageTemplate': '{\'temperature\':${temperature},\'temperature_unit\':${temperature_unit}}',
                        'MessageSchema': {
                            'Name': 'chiller-sonsors',
                            'Format': 'JSON',
                            'Fields': {
                                'temperature': 'Double',
                                'temperature_unit': 'Text'
                            }
                        }
                    }
                ],
                'Simulation': {
                    'Interval': '00:00:10',
                    'InitialState': {},
                    'Scripts': [{
                        'Type': 'javascript',
                        'Path': 'chiller-state.js'
                    }]
                },
                'Properties': {}
            }");
            var request = new HttpRequest(DS_ADDRESS + "/devicemodels");
            request.AddHeader("Content-Type", "application/json");
            request.SetContent(deviceModel);

            // Act
            var response = this.httpClient.PostAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            JObject jsonResponse = JObject.Parse(response.Content);

            Assert.True(jsonResponse.HasValues);

            foreach (KeyValuePair<string, JToken> sourceProperty in deviceModel)
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
        /// Simulation service should able to retrieve a device model by id
        /// </summary>
        [Fact]
        public void Should_Return_A_DeviceModel_By_Id()
        {
            // Arrage
            var deviceModel = JObject.Parse(@"{  
                'ETag': 'etag',
                'Name': 'chiller',
                'Protocol': 'MQTT',
                'Type': 'Custom',
                'Telemetry': [  
                    {  
                        'Interval': '00:00:10',
                        'MessageTemplate': '{\'temperature\':${temperature},\'temperature_unit\':${temperature_unit}}',
                        'MessageSchema': {
                            'Name': 'chiller-sonsors',
                            'Format': 'JSON',
                            'Fields': {
                                'temperature': 'Double',
                                'temperature_unit': 'Text'
                            }
                        }
                    }
                ],
                'Simulation': {
                    'Interval': '00:00:10',
                    'InitialState': {},
                    'Scripts': [{
                        'Type': 'javascript',
                        'Path': 'chiller-state.js'
                    }]
                },
                'Properties': {}
            }");
            IHttpResponse postResponse = this.CreateDeviceModel(deviceModel);
            JObject postJsonResponse = JObject.Parse(postResponse.Content);
            string id = (string)postJsonResponse["Id"];

            // Act
            var request = new HttpRequest(DS_ADDRESS + $"/devicemodels/{id}");
            request.AddHeader("X-Foo", "Bar");
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
        /// Simulation service should able to upsert a device model by id
        /// </summary>
        [Fact]
        public void Should_Upsert_A_DeviceModel_By_Id()
        {
            // Arrage
            var deviceModel = JObject.Parse(@"{  
                'ETag': 'etag',
                'Name': 'chiller',
                'Protocol': 'MQTT',
                'Type': 'Custom',
                'Telemetry': [  
                    {  
                        'Interval': '00:00:10',
                        'MessageTemplate': '{\'temperature\':${temperature},\'temperature_unit\':${temperature_unit}}',
                        'MessageSchema': {
                            'Name': 'chiller-sonsors',
                            'Format': 'JSON',
                            'Fields': {
                                'temperature': 'Double',
                                'temperature_unit': 'Text'
                            }
                        }
                    }
                ],
                'Simulation': {
                    'Interval': '00:00:10',
                    'InitialState': {},
                    'Scripts': [{
                        'Type': 'javascript',
                        'Path': 'chiller-state.js'
                    }]
                },
                'Properties': {}
            }");
            IHttpResponse postResponse = this.CreateDeviceModel(deviceModel);
            JObject postJsonResponse = JObject.Parse(postResponse.Content);
            string id = (string)postJsonResponse["Id"];

            // Upsert device model
            deviceModel["ETag"] = (string)postJsonResponse["ETag"];
            deviceModel["Name"] = "Updated name";

            IHttpResponse upsertResponse = this.CreateDeviceModel(deviceModel);
            JObject upsertJsonResponse = JObject.Parse(upsertResponse.Content);

            // Act
            var request = new HttpRequest(DS_ADDRESS + $"/devicemodels/{id}");
            request.AddHeader("X-Foo", "Bar");
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
        /// Simulation service should return Conflict when etag doesn't match 
        /// in upserting a device model
        /// </summary>
        [Fact]
        public void Should_Return_Conflict_When_ETag_Does_Not_Match_In_Upserting()
        {
            // Arrage
            var deviceModel = JObject.Parse(@"{  
                'ETag': 'etag',
                'Name': 'chiller',
                'Protocol': 'MQTT',
                'Type': 'Custom',
                'Telemetry': [  
                    {  
                        'Interval': '00:00:10',
                        'MessageTemplate': '{\'temperature\':${temperature},\'temperature_unit\':${temperature_unit}}',
                        'MessageSchema': {
                            'Name': 'chiller-sonsors',
                            'Format': 'JSON',
                            'Fields': {
                                'temperature': 'Double',
                                'temperature_unit': 'Text'
                            }
                        }
                    }
                ],
                'Simulation': {
                    'Interval': '00:00:10',
                    'InitialState': {},
                    'Scripts': [{
                        'Type': 'javascript',
                        'Path': 'chiller-state.js'
                    }]
                },
                'Properties': {}
            }");
            IHttpResponse postResponse = this.CreateDeviceModel(deviceModel);
            JObject postJsonResponse = JObject.Parse(postResponse.Content);
            string id = (string)postJsonResponse["Id"];

            IHttpResponse upsertResponse = this.CreateDeviceModel(deviceModel);
            JObject upsertJsonResponse = JObject.Parse(postResponse.Content);

            // Act
            var request = new HttpRequest(DS_ADDRESS + $"/devicemodels/{id}");
            request.AddHeader("Content-Type", "application/json");
            request.SetContent(deviceModel);
            var response = this.httpClient.PutAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        /// <summary>
        /// Simulation service should return bad request when failed to provide
        /// valid protocol
        /// </summary>
        [Fact]
        public void Should_Return_Badrequest_With_Invalid_Protocol()
        {
            // Arrage
            var deviceModelWithInvaildProtocol = JObject.Parse(@"{  
                'ETag': 'etag',
                'Name': 'chiller',
                'Protocol': 'MMQQTT',
                'Type': 'Custom',
                'Telemetry': [  
                    {  
                        'Interval': '00:00:10',
                        'MessageTemplate': '{\'temperature\':${temperature},\'temperature_unit\':${temperature_unit}}',
                        'MessageSchema': {
                            'Name': 'chiller-sonsors',
                            'Format': 'JSON',
                            'Fields': {
                                'temperature': 'Double',
                                'temperature_unit': 'Text'
                            }
                        }
                    }
                ],
                'Simulation': {
                    'Interval': '00:00:10',
                    'InitialState': {},
                    'Scripts': [{
                        'Type': 'javascript',
                        'Path': 'chiller-state.js'
                    }]
                },
                'Properties': {}
            }");

            var deviceModelWithoutProtocol = JObject.Parse(@"{  
                'ETag': 'etag',
                'Name': 'chiller',
                'Type': 'Custom',
                'Telemetry': [  
                    {  
                        'Interval': '00:00:10',
                        'MessageTemplate': '{\'temperature\':${temperature},\'temperature_unit\':${temperature_unit}}',
                        'MessageSchema': {
                            'Name': 'chiller-sonsors',
                            'Format': 'JSON',
                            'Fields': {
                                'temperature': 'Double',
                                'temperature_unit': 'Text'
                            }
                        }
                    }
                ],
                'Simulation': {
                    'Interval': '00:00:10',
                    'InitialState': {},
                    'Scripts': [{
                        'Type': 'javascript',
                        'Path': 'chiller-state.js'
                    }]
                },
                'Properties': {}
            }");

            // Act
            var response = this.CreateDeviceModel(deviceModelWithInvaildProtocol);
            var response_1 = this.CreateDeviceModel(deviceModelWithoutProtocol);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response_1.StatusCode);
        }

        /// <summary>
        /// Simulation service should return bad request when failed to provide
        /// valid type
        /// </summary>
        [Fact]
        public void Should_Return_Badrequest_With_Invalid_Type()
        {
            // Arrage
            var deviceModelWithInvaildType = JObject.Parse(@"{  
                'ETag': 'etag',
                'Name': 'chiller',
                'Protocol': 'MQTT',
                'Type': 'Invalid',
                'Telemetry': [  
                    {  
                        'Interval': '00:00:10',
                        'MessageTemplate': '{\'temperature\':${temperature},\'temperature_unit\':${temperature_unit}}',
                        'MessageSchema': {
                            'Name': 'chiller-sonsors',
                            'Format': 'JSON',
                            'Fields': {
                                'temperature': 'Double',
                                'temperature_unit': 'Text'
                            }
                        }
                    }
                ],
                'Simulation': {
                    'Interval': '00:00:10',
                    'InitialState': {},
                    'Scripts': [{
                        'Type': 'javascript',
                        'Path': 'chiller-state.js'
                    }]
                },
                'Properties': {}
            }");

            // Act
            var response = this.CreateDeviceModel(deviceModelWithInvaildType);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        /// <summary>
        /// Simulation service should return bad request when failed to provide
        /// valid telemetry
        /// </summary>
        [Fact]
        public void Should_Return_Badrequest_With_Invalid_Telemetry()
        {
            // Arrage
            var zeroTelemetry = JObject.Parse(@"{  
                'ETag': 'etag',
                'Name': 'chiller',
                'Protocol': 'MQTT',
                'Type': 'Custom',
                'Telemetry': [],
                'Simulation': {
                    'Interval': '00:00:10',
                    'InitialState': {},
                    'Scripts': [{
                        'Type': 'javascript',
                        'Path': 'chiller-state.js'
                    }]
                },
                'Properties': {}
            }");

            var invalidInterval = JObject.Parse(@"{  
                'ETag': 'etag',
                'Name': 'chiller',
                'Protocol': 'MQTT',
                'Type': 'Custom',
                'Telemetry': [  
                    {  
                        'Interval': 'invalid',
                        'MessageTemplate': '{\'temperature\':${temperature},\'temperature_unit\':${temperature_unit}}',
                        'MessageSchema': {
                            'Name': 'chiller-sonsors',
                            'Format': 'JSON',
                            'Fields': {
                                'temperature': 'Double',
                                'temperature_unit': 'Text'
                            }
                        }
                    }
                ],
                'Simulation': {
                    'Interval': '00:00:10',
                    'InitialState': {},
                    'Scripts': [{
                        'Type': 'javascript',
                        'Path': 'chiller-state.js'
                    }]
                },
                'Properties': {}
            }");

            var invalidMessageTemplate = JObject.Parse(@"{  
                'ETag': 'etag',
                'Name': 'chiller',
                'Type': 'Custom',
                'Protocol': 'MQTT',
                'Telemetry': [  
                    {  
                        'Interval': '00:00:10',
                        'MessageTemplate': '',
                        'MessageSchema': {
                            'Name': 'chiller-sonsors',
                            'Format': 'JSON',
                            'Fields': {
                                'temperature': 'Double',
                                'temperature_unit': 'Text'
                            }
                        }
                    }
                ],
                'Simulation': {
                    'Interval': '00:00:10',
                    'InitialState': {},
                    'Scripts': [{
                        'Type': 'javascript',
                        'Path': 'chiller-state.js'
                    }]
                },
                'Properties': {}
            }");

            var invalidMessageSchema = JObject.Parse(@"{  
                'ETag': 'etag',
                'Name': 'chiller',
                'Protocol': 'MQTT',
                'Type': 'Custom',
                'Telemetry': [  
                    {  
                        'Interval': '00:00:10',
                        'MessageTemplate': '{\'temperature\':${temperature},\'temperature_unit\':${temperature_unit}}',
                        'MessageSchema': {}
                    }
                ],
                'Simulation': {
                    'Interval': '00:00:10',
                    'InitialState': {},
                    'Scripts': [{
                        'Type': 'javascript',
                        'Path': 'chiller-state.js'
                    }]
                },
                'Properties': {}
            }");

            // Act
            var response = this.CreateDeviceModel(zeroTelemetry);
            var response_1 = this.CreateDeviceModel(invalidInterval);
            var response_2 = this.CreateDeviceModel(invalidMessageTemplate);
            var response_3 = this.CreateDeviceModel(invalidMessageSchema);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response_1.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response_2.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response_3.StatusCode);
        }

        /// <summary>
        /// Simulation service should able to delete a device model by id
        /// </summary>
        [Fact]
        public void Should_Delete_A_DeviceModel_By_Id()
        {
            // Arrage
            var deviceModel = JObject.Parse(@"{  
                'ETag': 'etag',
                'Name': 'chiller',
                'Protocol': 'MQTT',
                'Type': 'Custom',
                'Telemetry': [  
                    {  
                        'Interval': '00:00:10',
                        'MessageTemplate': '{\'temperature\':${temperature},\'temperature_unit\':${temperature_unit}}',
                        'MessageSchema': {
                            'Name': 'chiller-sonsors',
                            'Format': 'JSON',
                            'Fields': {
                                'temperature': 'Double',
                                'temperature_unit': 'Text'
                            }
                        }
                    }
                ],
                'Simulation': {
                    'Interval': '00:00:10',
                    'InitialState': {},
                    'Scripts': [{
                        'Type': 'javascript',
                        'Path': 'chiller-state.js'
                    }]
                },
                'Properties': {}
            }");
            IHttpResponse postResponse = this.CreateDeviceModel(deviceModel);
            JObject postJsonResponse = JObject.Parse(postResponse.Content);
            string id = (string)postJsonResponse["Id"];

            // Act
            var request = new HttpRequest(DS_ADDRESS + $"/devicemodels/{id}");
            request.AddHeader("X-Foo", "Bar");
            var response = this.httpClient.DeleteAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        /// <summary>
        /// Simulation service should return forbidden when delete 
        /// a stock device model by id
        /// </summary>
        [Fact]
        public void Should_Return_Forbidden_When_Delete_A_Stock_DeviceModel_By_Id()
        {
            // Arrage
            string stockModelId = "chiller-01";

            // Act
            var request = new HttpRequest(DS_ADDRESS + $"/devicemodels/{stockModelId}");
            request.AddHeader("X-Foo", "Bar");
            var response = this.httpClient.DeleteAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        private IHttpResponse CreateDeviceModel(JObject deviceModel)
        {
            var request = new HttpRequest(DS_ADDRESS + "/devicemodels");
            request.AddHeader("Content-Type", "application/json");
            request.SetContent(deviceModel);

            return this.httpClient.PostAsync(request).Result;
        }
    }
}
