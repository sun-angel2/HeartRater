using System.Globalization;
using System.Resources;
using System.Threading;

namespace PulseLink.Resources
{
    public class Strings
    {
        private static ResourceManager _resourceMan = new ResourceManager("PulseLink.Resources.Strings", typeof(Strings).Assembly);

        public static string GetString(string name)
        {
            return _resourceMan.GetString(name, Thread.CurrentThread.CurrentUICulture) ?? name;
        }

        // Properties for each string
        public static string App_Title => GetString(nameof(App_Title));
        public static string Button_Live => GetString(nameof(Button_Live));
        public static string Button_Scan => GetString(nameof(Button_Scan));
        public static string Checkbox_GhostMode => GetString(nameof(Checkbox_GhostMode));
        public static string Label_Bpm => GetString(nameof(Label_Bpm));
        public static string Label_Header => GetString(nameof(Label_Header));
        public static string Status_Connected => GetString(nameof(Status_Connected));
        public static string Status_Connecting => GetString(nameof(Status_Connecting));
        public static string Status_Error_DeviceNotFound => GetString(nameof(Status_Error_DeviceNotFound));
        public static string Status_Error_Exception => GetString(string.Format(GetString(nameof(Status_Error_Exception)), "{0}"));
        public static string Status_Error_NoHrCharacteristic => GetString(nameof(Status_Error_NoHrCharacteristic));
        public static string Status_Error_PleaseEnableHrBroadcast => GetString(nameof(Status_Error_PleaseEnableHrBroadcast));
        public static string Status_InitializingStream => GetString(nameof(Status_InitializingStream));
        public static string Status_LivestreamOnline => GetString(nameof(Status_LivestreamOnline));
        public static string Status_Ready => GetString(nameof(Status_Ready));
        public static string Status_Scanning => GetString(nameof(Status_Scanning));
        public static string Tooltip_GhostMode => GetString(nameof(Tooltip_GhostMode));
        public static string Web_Label_Bpm => GetString(nameof(Web_Label_Bpm));
        public static string Web_Label_LiveHeartRate => GetString(nameof(Web_Label_LiveHeartRate));
        public static string Web_Status_Connecting => GetString(nameof(Web_Status_Connecting));
        public static string Web_Status_Disconnected => GetString(nameof(Web_Status_Disconnected));
        public static string Web_Status_Error_NoUserId => GetString(nameof(Web_Status_Error_NoUserId));
        public static string Web_Status_LiveSignalActive => GetString(nameof(Web_Status_LiveSignalActive));
        public static string Web_Status_WaitingForData => GetString(nameof(Web_Status_WaitingForData));
        public static string Web_Title => GetString(nameof(Web_Title));
    }
}
