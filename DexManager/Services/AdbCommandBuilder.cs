using System;

namespace DexManager.Services
{
    public static class AdbCommandBuilder
    {
        public static string ForDevice(string serial, string arguments)
        {
            if (string.IsNullOrWhiteSpace(serial))
                throw new ArgumentException(
                    "ADB device serial is empty.",
                    "serial");
            if (string.IsNullOrWhiteSpace(arguments))
                throw new ArgumentException(
                    "ADB device command is empty.",
                    "arguments");

            return "-s " + Quote(serial.Trim()) + " " + arguments;
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
