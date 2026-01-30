namespace SimpleBluetoothTerminalNet;

internal static class Constants
{
    // values have to be globally unique
    public const string INTENT_ACTION_DISCONNECT = "de.kai_morich.simple_bluetooth_terminal.Disconnect";
    public const string NOTIFICATION_CHANNEL = "de.kai_morich.simple_bluetooth_terminal.Channel";
    public const string INTENT_CLASS_MAIN_ACTIVITY = "de.kai_morich.simple_bluetooth_terminal.MainActivity";

    // values have to be unique within each app
    public const int NOTIFY_MANAGER_START_FOREGROUND_SERVICE = 1001;
}
