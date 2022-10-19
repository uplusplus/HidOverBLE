// See https://aka.ms/new-console-template for more information

using HID;

HID_Advertise advertise = new HID_Advertise();
var result = await advertise.ServiceProviderInitAsync();
System.Console.ReadKey();
