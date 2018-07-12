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
        private const string IOTHUB_CONNECTION_STRING = "HostName=iothub-gi6bx.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=ncS4YgSWTp85ESqCpmkzN99WcWlRl0stMJTu3WfrR10=";
        private const string PATCH_SIMULATION = "http://localhost:9003/v1/simulations/1";
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
            var request = new HttpRequest(DS_ADDRESS + "/simulations/1");
            var response = this.httpClient.GetAsync(request).Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public void Should_Create_Default_Simulation()
        {
            //delete the simulation that is already running
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
            request.AddHeader("Content-Type", "application/json");
            request.SetContent(simulation);
            var response = this.httpClient.PostAsync(request).Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public async void Should_Create_SimulatedDevice()
        {
            //delete the simulation that is already running
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
            request.AddHeader("Content-Type", "application/json");
            request.SetContent(simulation);
            var response = this.httpClient.PostAsync(request).Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Thread.Sleep(15000);
            RegistryManager registry = RegistryManager.CreateFromConnectionString(IOTHUB_CONNECTION_STRING);
            Twin dev1 = await registry.GetTwinAsync("truck-01.0");
            Assert.True(dev1.Tags["IsSimulated"] == 'Y');
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public async void Should_Upgrade_Firmware_On_SimulatedDevice()
        {
            //delete the simulation that is already running
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
            request.AddHeader("Content-Type", "application/json");
            request.SetContent(simulation);
            var response = this.httpClient.PostAsync(request).Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Thread.Sleep(15000);

            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(IOTHUB_CONNECTION_STRING);
            Task<CloudToDeviceMethodResult> directResponseFuture = serviceClient.InvokeDeviceMethodAsync("truck-01.0",
                new CloudToDeviceMethod("FirmwareUpdate"));
            CloudToDeviceMethodResult response1 = await directResponseFuture.ConfigureAwait(false);
            Assert.Equal(200, response1.Status);
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public async void Should_Reboot_SimulatedDevice()
        {
            //delete the simulation that is already running
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
            request.AddHeader("Content-Type", "application/json");
            request.SetContent(simulation);
            var response = this.httpClient.PostAsync(request).Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Thread.Sleep(15000);

            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(IOTHUB_CONNECTION_STRING);
            Task<CloudToDeviceMethodResult> directResponseFuture = serviceClient.InvokeDeviceMethodAsync("chiller-01.0",
                new CloudToDeviceMethod("Reboot"));
            CloudToDeviceMethodResult response1 = await directResponseFuture.ConfigureAwait(false);

            Assert.Equal(200, response1.Status);
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public void Should_Start_Given_Simulation()
        {

            var request1 = new HttpRequest(DS_ADDRESS + "/simulations/1");
            var response1 = this.httpClient.GetAsync(request1).Result;
            Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
            JObject JsonResponse = JObject.Parse(response1.Content);

            string ETag = (string)JsonResponse["ETag"];

            var simulation2 = "{" + $"'ETag': '{ETag}' ,'Enabled': true" + "}";
            var byteArray = Encoding.ASCII.GetBytes(simulation2);

            var request2 = (HttpWebRequest)WebRequest.Create("http://localhost:9003/v1/simulations/1");
            request2.Method = "PATCH";
            request2.ContentLength = byteArray.Length;
            request2.ContentType = "application/json";
            Stream dataStream = request2.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
            var response2 = (HttpWebResponse)request2.GetResponse();
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

            var request = new HttpRequest(DS_ADDRESS + "/simulations/1");
            var response = this.httpClient.GetAsync(request).Result;
            JObject jsonResponse = JObject.Parse(response.Content);

            Assert.True((bool)jsonResponse["Enabled"]);
        }

        [Fact, Trait("Type", "IntegrationTest")]
        public void Should_Stop_Given_Simulation()
        {
            var request1 = new HttpRequest(DS_ADDRESS + "/simulations/1");
            var response1 = this.httpClient.GetAsync(request1).Result;
            Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
            JObject JsonResponse = JObject.Parse(response1.Content);

            string ETag = (string)JsonResponse["ETag"];

            var simulation2 = "{" + $"'ETag': '{ETag}' ,'Enabled': false" + "}";
            var byteArray = Encoding.ASCII.GetBytes(simulation2);

            var request2 = (HttpWebRequest)WebRequest.Create(PATCH_SIMULATION);
            request2.Method = "PATCH";
            request2.ContentLength = byteArray.Length;
            request2.ContentType = "application/json";
            Stream dataStream = request2.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
            var response2 = (HttpWebResponse)request2.GetResponse();
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

            var request = new HttpRequest(DS_ADDRESS + "/simulations/1");
            var response = this.httpClient.GetAsync(request).Result;
            JObject jsonResponse = JObject.Parse(response.Content);

            Assert.False((bool)jsonResponse["Enabled"]);
        }

        private void Should_Delete_Existing_Simulation()
        {
            var request = new HttpRequest(DS_ADDRESS + "/simulations/1");
            var response = this.httpClient.DeleteAsync(request).Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }



    }
}
