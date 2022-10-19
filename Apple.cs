using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth.Advertisement;

class Apple
{
	public void Start()
	{
		BluetoothLEAdvertisementPublisher _blePublisher;
		var manufactureData = new BluetoothLEManufacturerData();
		//0x004C	Apple, Inc.
		manufactureData.CompanyId = 0x004c;
		//uuid:E4 C8 A4 FC F6 8B 47 0D 95 9F 29 38 2A F7 2C E7
		//majorid:1
		//minorid:1
		var dataArray = new byte[] {
				// last 2 bytes of Apple's iBeacon
				0x02, 0x15,
				// UUID E4 C8 A4 FC F6 8B 47 0D 95 9F 29 38 2A F7 2C E7
				0xE4, 0xC8, 0xA4, 0xFC,
				0xF6, 0x8B, 0x47, 0x0D,
				0x95, 0x9F, 0x29, 0x38,
				0x2A, 0xF7, 0x2C, 0xE7,
				// Major
				0x00, 0x01,
				// Minor
				0x00, 0x01,
				// TX power
				0xc5
			};
		//
		manufactureData.Data = dataArray.AsBuffer();
		_blePublisher = new BluetoothLEAdvertisementPublisher();
		_blePublisher.Advertisement.ManufacturerData.Add(manufactureData);
		//開始發佈
		_blePublisher.Start();
		Console.WriteLine("ble advertisement. any key to quit.");
	}
}