using Android.App;
using Android.Bluetooth;
using Android.Content;
using AndroidX.Core.Content;
using System.IO;

namespace SimpleBluetoothTerminalNet;

internal class SerialSocket : Java.Lang.Object, Java.Lang.IRunnable
{
    private static readonly Java.Util.UUID BLUETOOTH_SPP = Java.Util.UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");

    private readonly DisconnectBroadcastReceiver _disconnectBroadcastReceiver;

    private readonly Context _context;
    private ISerialListener? _listener;
    private readonly BluetoothDevice _device;
    private BluetoothSocket? _socket;
    private bool _connected;

    public SerialSocket(Context context, BluetoothDevice device)
    {
        if (context is Activity)
            throw new InvalidOperationException("expected non UI context");
        _context = context;
        _device = device;
        _disconnectBroadcastReceiver = new DisconnectBroadcastReceiver(this);
    }

    public string GetName()
    {
        return !string.IsNullOrEmpty(_device.Name) ? _device.Name : _device.Address ?? "Unknown";
    }

    /// <summary>
    /// connect-success and most connect-errors are returned asynchronously to listener
    /// </summary>
    public void Connect(ISerialListener listener)
    {
        _listener = listener;
        ContextCompat.RegisterReceiver(_context, _disconnectBroadcastReceiver, 
            new IntentFilter(Constants.INTENT_ACTION_DISCONNECT), ContextCompat.ReceiverNotExported);
        System.Threading.Tasks.Task.Run(() => Run());
    }

    public void Disconnect()
    {
        _listener = null; // ignore remaining data and errors
        if (_socket != null)
        {
            try
            {
                _socket.Close();
            }
            catch (Exception)
            {
            }
            _socket = null;
        }
        try
        {
            _context.UnregisterReceiver(_disconnectBroadcastReceiver);
        }
        catch (Exception)
        {
        }
    }

    public void Write(byte[] data)
    {
        if (!_connected)
            throw new IOException("not connected");
        _socket?.OutputStream?.Write(data);
    }

    public void Run()
    {
        try
        {
            _socket = _device.CreateRfcommSocketToServiceRecord(BLUETOOTH_SPP);
            _socket.Connect();
            _listener?.OnSerialConnect();
        }
        catch (Exception e)
        {
            _listener?.OnSerialConnectError(e);
            try
            {
                _socket?.Close();
            }
            catch (Exception)
            {
            }
            _socket = null;
            return;
        }
        _connected = true;
        try
        {
            byte[] buffer = new byte[1024];
            int len;
            while (true)
            {
                len = _socket?.InputStream?.Read(buffer) ?? 0;
                byte[] data = new byte[len];
                Array.Copy(buffer, data, len);
                _listener?.OnSerialRead(data);
            }
        }
        catch (Exception e)
        {
            _connected = false;
            _listener?.OnSerialIoError(e);
            try
            {
                _socket?.Close();
            }
            catch (Exception)
            {
            }
            _socket = null;
        }
    }

    private class DisconnectBroadcastReceiver : BroadcastReceiver
    {
        private readonly SerialSocket _serialSocket;

        public DisconnectBroadcastReceiver(SerialSocket serialSocket)
        {
            _serialSocket = serialSocket;
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            _serialSocket._listener?.OnSerialIoError(new IOException("background disconnect"));
            _serialSocket.Disconnect(); // disconnect now, else would be queued until UI re-attached
        }
    }
}
