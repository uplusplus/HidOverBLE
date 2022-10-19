//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace HID
{
    // This scenario declares support for a calculator service. 
    // Remote clients (including this sample on another machine) can supply:
    // - Operands 1 and 2
    // - an operator (+,-,*,/)
    // and get a result
    public sealed partial class HID_Advertise
    {
        GattServiceProvider serviceProvider;

        private GattLocalCharacteristic op1Characteristic;
        private int operand1Received = 0;

        private GattLocalCharacteristic op2Characteristic;
        private int operand2Received = 0;

        private GattLocalCharacteristic operatorCharacteristic;
        CalculatorOperators operatorReceived = 0;

        private GattLocalCharacteristic resultCharacteristic;
        private int resultVal = 0;

        private bool peripheralSupported = true;

        private enum CalculatorCharacteristics
        {
            Operand1 = 1,
            Operand2 = 2,
            Operator = 3
        }

        private enum CalculatorOperators
        {
            Add = 1,
            Subtract = 2,
            Multiply = 3,
            Divide = 4
        }

        private async Task<bool> CheckPeripheralRoleSupportAsync()
        {
            // BT_Code: New for Creator's Update - Bluetooth adapter has properties of the local BT radio.
            var localAdapter = await BluetoothAdapter.GetDefaultAsync();

            if (localAdapter != null)
            {
                return localAdapter.IsPeripheralRoleSupported;
            }
            else
            {
                // Bluetooth is not turned on 
                return false;
            }
        }

        /// <summary>
        /// Uses the relevant Service/Characteristic UUIDs to initialize, hook up event handlers and start a service on the local system.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ServiceProviderInitAsync()
        {
            // BT_Code: Initialize and starting a custom GATT Service using GattServiceProvider.
            GattServiceProviderResult serviceResult = await GattServiceProvider.CreateAsync(Constants.CalcServiceUuid);
            if (serviceResult.Error == BluetoothError.Success)
            {
                serviceProvider = serviceResult.ServiceProvider;
            }
            else
            {
                Console.WriteLine($"Could not create service provider: {serviceResult.Error}");
                return false;
            }

            GattLocalCharacteristicResult result = await serviceProvider.Service.CreateCharacteristicAsync(Constants.Op1CharacteristicUuid, Constants.gattOperandParameters);
            if (result.Error == BluetoothError.Success)
            {
                op1Characteristic = result.Characteristic;
            }
            else
            {
                Console.WriteLine($"Could not create operand1 characteristic: {result.Error}");
                return false;
            }
            op1Characteristic.WriteRequested += Op1Characteristic_WriteRequestedAsync;

            result = await serviceProvider.Service.CreateCharacteristicAsync(Constants.Op2CharacteristicUuid, Constants.gattOperandParameters);
            if (result.Error == BluetoothError.Success)
            {
                op2Characteristic = result.Characteristic;
            }
            else
            {
                Console.WriteLine($"Could not create operand2 characteristic: {result.Error}");
                return false;
            }

            op2Characteristic.WriteRequested += Op2Characteristic_WriteRequestedAsync;

            result = await serviceProvider.Service.CreateCharacteristicAsync(Constants.OperatorCharacteristicUuid, Constants.gattOperatorParameters);
            if (result.Error == BluetoothError.Success)
            {
                operatorCharacteristic = result.Characteristic;
            }
            else
            {
                Console.WriteLine($"Could not create operator characteristic: {result.Error}");
                return false;
            }

            operatorCharacteristic.WriteRequested += OperatorCharacteristic_WriteRequestedAsync;

            // Add presentation format - 32-bit unsigned integer, with exponent 0, the unit is unitless, with no company description
            GattPresentationFormat intFormat = GattPresentationFormat.FromParts(
                GattPresentationFormatTypes.UInt32,
                PresentationFormats.Exponent,
                Convert.ToUInt16(PresentationFormats.Units.Unitless),
                Convert.ToByte(PresentationFormats.NamespaceId.BluetoothSigAssignedNumber),
                PresentationFormats.Description);

            Constants.gattResultParameters.PresentationFormats.Add(intFormat);

            result = await serviceProvider.Service.CreateCharacteristicAsync(Constants.ResultCharacteristicUuid, Constants.gattResultParameters);
            if (result.Error == BluetoothError.Success)
            {
                resultCharacteristic = result.Characteristic;
            }
            else
            {
                Console.WriteLine($"Could not create result characteristic: {result.Error}");
                return false;
            }
            resultCharacteristic.ReadRequested += ResultCharacteristic_ReadRequestedAsync;
            resultCharacteristic.SubscribedClientsChanged += ResultCharacteristic_SubscribedClientsChanged;

            // BT_Code: Indicate if your sever advertises as connectable and discoverable.
            GattServiceProviderAdvertisingParameters advParameters = new GattServiceProviderAdvertisingParameters
            {
                // IsConnectable determines whether a call to publish will attempt to start advertising and 
                // put the service UUID in the ADV packet (best effort)
                IsConnectable = peripheralSupported,

                // IsDiscoverable determines whether a remote device can query the local device for support 
                // of this service
                IsDiscoverable = true
            };
            serviceProvider.AdvertisementStatusChanged += ServiceProvider_AdvertisementStatusChanged;
            serviceProvider.StartAdvertising(advParameters);
            Console.WriteLine("ble advertisement. any key to quit.");
            return true;
        }

        private void ResultCharacteristic_SubscribedClientsChanged(GattLocalCharacteristic sender, object args)
        {
            Console.WriteLine($"New device subscribed. New subscribed count: {sender.SubscribedClients.Count}");
        }

        private void ServiceProvider_AdvertisementStatusChanged(GattServiceProvider sender, GattServiceProviderAdvertisementStatusChangedEventArgs args)
        {
            // Created - The default state of the advertisement, before the service is published for the first time.
            // Stopped - Indicates that the application has canceled the service publication and its advertisement.
            // Started - Indicates that the system was successfully able to issue the advertisement request.
            // Aborted - Indicates that the system was unable to submit the advertisement request, or it was canceled due to resource contention.

            Console.WriteLine($"New Advertisement Status: {sender.AdvertisementStatus}");
        }

        private async void ResultCharacteristic_ReadRequestedAsync(GattLocalCharacteristic sender, GattReadRequestedEventArgs args)
        {
            // BT_Code: Process a read request. 
            using (args.GetDeferral())
            {
                // Get the request information.  This requires device access before an app can access the device's request. 
                GattReadRequest request = await args.GetRequestAsync();
                if (request == null)
                {
                    // No access allowed to the device.  Application should indicate this to the user.
                    Console.WriteLine("Access to device not allowed");
                    return;
                }

                var writer = new DataWriter();
                writer.ByteOrder = ByteOrder.LittleEndian;
                writer.WriteInt32(resultVal);

                // Can get details about the request such as the size and offset, as well as monitor the state to see if it has been completed/cancelled externally.
                // request.Offset
                // request.Length
                // request.State
                // request.StateChanged += <Handler>

                // Gatt code to handle the response
                request.RespondWithValue(writer.DetachBuffer());
            }
        }

        private int ComputeResult()
        {
            Int32 computedValue = 0;
            switch (operatorReceived)
            {
                case CalculatorOperators.Add:
                    computedValue = operand1Received + operand2Received;
                    break;
                case CalculatorOperators.Subtract:
                    computedValue = operand1Received - operand2Received;
                    break;
                case CalculatorOperators.Multiply:
                    computedValue = operand1Received * operand2Received;
                    break;
                case CalculatorOperators.Divide:
                    if (operand2Received == 0 || (operand1Received == -0x80000000 && operand2Received == -1))
                    {
                        Console.WriteLine("Division overflow");
                    }
                    else
                    {
                        computedValue = operand1Received / operand2Received;
                    }
                    break;
                default:
                    Console.WriteLine("Invalid Operator");
                    break;
            }
            NotifyClientDevices(computedValue);
            return computedValue;
        }

        private async void NotifyClientDevices(int computedValue)
        {
            var writer = new DataWriter();
            writer.ByteOrder = ByteOrder.LittleEndian;
            writer.WriteInt32(computedValue);

            // BT_Code: Returns a collection of all clients that the notification was attempted and the result.
            IReadOnlyList<GattClientNotificationResult> results = await resultCharacteristic.NotifyValueAsync(writer.DetachBuffer());

            Console.WriteLine($"Sent value {computedValue} to clients.");
            foreach (var result in results)
            {
                // An application can iterate through each registered client that was notified and retrieve the results:
                //
                // result.SubscribedClient: The details on the remote client.
                // result.Status: The GattCommunicationStatus
                // result.ProtocolError: iff Status == GattCommunicationStatus.ProtocolError
            }
        }

        private async void Op1Characteristic_WriteRequestedAsync(GattLocalCharacteristic sender, GattWriteRequestedEventArgs args)
        {
            // BT_Code: Processing a write request.
            using (args.GetDeferral())
            {
                // Get the request information.  This requires device access before an app can access the device's request.
                GattWriteRequest request = await args.GetRequestAsync();
                if (request == null)
                {
                    // No access allowed to the device.  Application should indicate this to the user.
                    return;
                }
                ProcessWriteCharacteristic(request, CalculatorCharacteristics.Operand1);
            }
        }

        private async void Op2Characteristic_WriteRequestedAsync(GattLocalCharacteristic sender, GattWriteRequestedEventArgs args)
        {
            using (args.GetDeferral())
            {
                // Get the request information.  This requires device access before an app can access the device's request.
                GattWriteRequest request = await args.GetRequestAsync();
                if (request == null)
                {
                    // No access allowed to the device.  Application should indicate this to the user.
                    return;
                }
                ProcessWriteCharacteristic(request, CalculatorCharacteristics.Operand2);
            }
        }

        private async void OperatorCharacteristic_WriteRequestedAsync(GattLocalCharacteristic sender, GattWriteRequestedEventArgs args)
        {
            using (args.GetDeferral())
            {
                // Get the request information.  This requires device access before an app can access the device's request.
                GattWriteRequest request = await args.GetRequestAsync();
                if (request == null)
                {
                    // No access allowed to the device.  Application should indicate this to the user.
                    return;
                }
                ProcessWriteCharacteristic(request, CalculatorCharacteristics.Operator);
            }
        }

        /// <summary>
        /// BT_Code: Processing a write request.Takes in a GATT Write request and updates UX based on opcode.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="opCode">Operand (1 or 2) and Operator (3)</param>
        private void ProcessWriteCharacteristic(GattWriteRequest request, CalculatorCharacteristics opCode)
        {
            if (request.Value.Length != 4)
            {
                // Input is the wrong length. Respond with a protocol error if requested.
                if (request.Option == GattWriteOption.WriteWithResponse)
                {
                    request.RespondWithProtocolError(GattProtocolError.InvalidAttributeValueLength);
                }
                return;
            }

            var reader = DataReader.FromBuffer(request.Value);
            reader.ByteOrder = ByteOrder.LittleEndian;
            int val = reader.ReadInt32();

            switch (opCode)
            {
                case CalculatorCharacteristics.Operand1:
                    operand1Received = val;
                    Console.WriteLine("op1:" + val);
                    break;
                case CalculatorCharacteristics.Operand2:
                    operand2Received = val;
                    Console.WriteLine("op2:" + val);
                    break;
                case CalculatorCharacteristics.Operator:
                    Console.WriteLine("op:" + val);
                    if (!Enum.IsDefined(typeof(CalculatorOperators), val))
                    {
                        if (request.Option == GattWriteOption.WriteWithResponse)
                        {
                            request.RespondWithProtocolError(GattProtocolError.InvalidPdu);
                        }
                        return;
                    }
                    operatorReceived = (CalculatorOperators)val;
                    break;
            }
            // Complete the request if needed
            if (request.Option == GattWriteOption.WriteWithResponse)
            {
                request.Respond();
            }

            resultVal = ComputeResult();
            
        }
    }
}
