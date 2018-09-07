// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Net;
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
    public class DeviceTests
    {
        private readonly IHttpClient httpClient;
        private readonly string IOTHUB_CONNECTION_STRING;

        public DeviceTests()
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
            this.IOTHUB_CONNECTION_STRING = config["PCS_IOTHUB_CONNSTRING"];
            this.httpClient = new HttpClient();
        }

        [Fact(Skip = "device test"), Trait("Type", "IntegrationTest")]
        public async void Should_Create_SimulatedDevice()
        {
            //Arrange
            Thread.Sleep(Constants.WAIT_TIME);
            this.Try_Run_Existing_Simulation();
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

        [Fact(Skip = "device test"), Trait("Type", "IntegrationTest")]
        public async void Should_Upgrade_Firmware_On_SimulatedDevice()
        {
            //Arrange
            Thread.Sleep(Constants.WAIT_TIME);
            this.Try_Run_Existing_Simulation();
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

        [Fact(Skip = "device test"), Trait("Type", "IntegrationTest")]
        public async void Should_Reboot_SimulatedDevice()
        {
            //Arrange
            Thread.Sleep(Constants.WAIT_TIME);
            this.Try_Run_Existing_Simulation();
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

        private void Try_Run_Existing_Simulation()
        {
            //this.Should_Start_Given_Simulation();
            var request = new HttpRequest(Constants.DEFAULT_SIMULATION_URL);
            var defaultSimulationResponse = this.httpClient.GetAsync(request).Result;

            //run  current simulation only if it exists
            if (defaultSimulationResponse.StatusCode == HttpStatusCode.OK)
            {
                var deleteCurrentSimulationRequest = new HttpRequest(Constants.DEFAULT_SIMULATION_URL);
                var deleteCurrentSimulationResponse = this.httpClient.DeleteAsync(deleteCurrentSimulationRequest).Result;
                Assert.Equal(HttpStatusCode.OK, deleteCurrentSimulationResponse.StatusCode);

                var getCurrentSimulationRequest = new HttpRequest(Constants.DEFAULT_SIMULATION_URL);
                var getCurrentSimulationResponse = this.httpClient.GetAsync(getCurrentSimulationRequest).Result;
                Assert.Equal(HttpStatusCode.NotFound, getCurrentSimulationResponse.StatusCode);
            }
        }
    }
}
