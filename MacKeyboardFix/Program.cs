using HidApiAdapter;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MacKeyboardFix
{
    // If you get a compiler error about "cannot find hidapi64.dll" or similar, amend the properties for
    // hidapi32.dll and hidapi64.dll in solution explorer to set "Copy to output" to "Copy if newer"
    // (or manually copy those DLLs to the bin dir)

    class Program
    {
        // To disable the capslock press delay, we send HID "feature report" with data [0x09, 0x00, 0x00, 0x00]
        // This info comes from https://apple.stackexchange.com/a/199958
        // Note that the product ID is different in the sources here. Your hardware may vary.
        // Also there may be multiple matching devices, not all of which will advertise support for this feature report

        static async Task Main(string[] args)
        {
            // For some reason the package doesn't expose the device pointer, but we need it to call the APIs
            var devicePtrField = typeof(HidDevice).GetField("m_DevicePtr", BindingFlags.Instance | BindingFlags.NonPublic);

            var devices = HidDeviceManager.GetManager().SearchDevices(vid: 0x5ac, pid: 0x263);

            foreach (var device in devices)
            {
                device.Connect();

                if (device.Product().Contains("Keyboard")) // Filter out "trackpad" or other non-matching devices
                {
                    Console.WriteLine(
                        $"-------------------\n" +
                        $"Found keyboard\n" +
                        $"device: {device.Path()}\n" +
                        $"manufacturer: {device.Manufacturer()}\n" +
                        $"product: {device.Product()}\n" +
                        $"serial number: {device.SerialNumber()}\n");

                    var devicePtr = (IntPtr)devicePtrField.GetValue(device);

                    if (TryGetFeatureReport(devicePtr, out var reportBefore))
                    {
                        Console.WriteLine("Feature report before: " + reportBefore);

                        Console.Write("Sending updated feature report: ");
                        Console.WriteLine(SetFeatureReport(devicePtr) ? "Success" : "Failure");

                        Console.Write("Feature report after: ");
                        Console.WriteLine(TryGetFeatureReport(devicePtr, out var reportAfter) ? reportAfter : "Failure");
                    }
                    else
                    {
                        Console.WriteLine("Failed to get feature report for this device");
                    }

                    Console.WriteLine($"-------------------\n");
                }

                device.Disconnect();
            }
        }

        private static bool SetFeatureReport(IntPtr devicePtr)
        {
            var data = new byte[] { 0x09, 0x00, 0x00, 0x00 };
            var bytesWritten = HidApi.hid_send_feature_report(devicePtr, data, (uint)data.Length);
            return bytesWritten == data.Length;
        }

        private static bool TryGetFeatureReport(IntPtr devicePtr, out string report)
        {
            var buf = new byte[256];
            buf[0] = 0x09;
            var bytesWritten = HidApi.hid_get_feature_report(devicePtr, buf, (uint)buf.Length);
            report = bytesWritten >= 0 ? FormatByteArray(buf, bytesWritten) : null;
            return bytesWritten >= 0;
        }

        private static string FormatByteArray(byte[] data, int length)
        {
            return string.Join(", ", data.Take(length).Select(x => x.ToString("x")).ToArray());
        }
    }
}
