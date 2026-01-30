using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace SimpleBluetoothTerminalNet;

[Service(ForegroundServiceType = Android.Content.PM.ForegroundService.TypeRemoteMessaging | 
                                  Android.Content.PM.ForegroundService.TypeConnectedDevice)]
public class SerialService : Service, ISerialListener
{
    public class SerialBinder : Binder
    {
        private readonly SerialService _service;

        public SerialBinder(SerialService service)
        {
            _service = service;
        }

        public SerialService GetService() => _service;
    }

    private enum QueueType { Connect, ConnectError, Read, IoError }

    private class QueueItem
    {
        public QueueType Type { get; set; }
        public Queue<byte[]>? Datas { get; set; }
        public Exception? E { get; set; }

        public QueueItem(QueueType type)
        {
            Type = type;
            if (type == QueueType.Read) Init();
        }

        public QueueItem(QueueType type, Exception e)
        {
            Type = type;
            E = e;
        }

        public QueueItem(QueueType type, Queue<byte[]> datas)
        {
            Type = type;
            Datas = datas;
        }

        public void Init()
        {
            Datas = new Queue<byte[]>();
        }

        public void Add(byte[] data)
        {
            Datas?.Enqueue(data);
        }
    }

    private readonly Handler _mainLooper;
    private readonly IBinder _binder;
    private readonly Queue<QueueItem> _queue1, _queue2;
    private readonly QueueItem _lastRead;

    private SerialSocket? _socket;
    private ISerialListener? _listener;
    private bool _connected;

    public SerialService()
    {
        _mainLooper = new Handler(Looper.MainLooper!);
        _binder = new SerialBinder(this);
        _queue1 = new Queue<QueueItem>();
        _queue2 = new Queue<QueueItem>();
        _lastRead = new QueueItem(QueueType.Read);
    }

    public override void OnDestroy()
    {
        CancelNotification();
        Disconnect();
        base.OnDestroy();
    }

    public override IBinder? OnBind(Intent? intent)
    {
        return _binder;
    }

    public void Connect(SerialSocket socket)
    {
        socket.Connect(this);
        _socket = socket;
        _connected = true;
    }

    public void Disconnect()
    {
        _connected = false; // ignore data,errors while disconnecting
        CancelNotification();
        if (_socket != null)
        {
            _socket.Disconnect();
            _socket = null;
        }
    }

    public void Write(byte[] data)
    {
        if (!_connected)
            throw new IOException("not connected");
        _socket?.Write(data);
    }

    public void Attach(ISerialListener listener)
    {
        if (Looper.MainLooper?.Thread != Java.Lang.Thread.CurrentThread())
            throw new ArgumentException("not in main thread");
        InitNotification();
        CancelNotification();
        
        lock (this)
        {
            _listener = listener;
        }
        
        foreach (var item in _queue1)
        {
            switch (item.Type)
            {
                case QueueType.Connect: listener.OnSerialConnect(); break;
                case QueueType.ConnectError: listener.OnSerialConnectError(item.E!); break;
                case QueueType.Read: listener.OnSerialRead(item.Datas!); break;
                case QueueType.IoError: listener.OnSerialIoError(item.E!); break;
            }
        }
        foreach (var item in _queue2)
        {
            switch (item.Type)
            {
                case QueueType.Connect: listener.OnSerialConnect(); break;
                case QueueType.ConnectError: listener.OnSerialConnectError(item.E!); break;
                case QueueType.Read: listener.OnSerialRead(item.Datas!); break;
                case QueueType.IoError: listener.OnSerialIoError(item.E!); break;
            }
        }
        _queue1.Clear();
        _queue2.Clear();
    }

    public void Detach()
    {
        if (_connected)
            CreateNotification();
        _listener = null;
    }

    private void InitNotification()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var nc = new NotificationChannel(Constants.NOTIFICATION_CHANNEL, "Background service", NotificationImportance.Low);
            nc.SetShowBadge(false);
            var nm = (NotificationManager?)GetSystemService(NotificationService);
            nm?.CreateNotificationChannel(nc);
        }
    }

    public bool AreNotificationsEnabled()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var nm = (NotificationManager?)GetSystemService(NotificationService);
            if (nm == null) return false;
            var nc = nm.GetNotificationChannel(Constants.NOTIFICATION_CHANNEL);
            return nm.AreNotificationsEnabled() && nc != null && nc.Importance > NotificationImportance.None;
        }
        return true;
    }

    private void CreateNotification()
    {
        var disconnectIntent = new Intent()
            .SetPackage(PackageName)
            .SetAction(Constants.INTENT_ACTION_DISCONNECT);
        var restartIntent = new Intent()
            .SetClassName(this, Constants.INTENT_CLASS_MAIN_ACTIVITY)
            .SetAction(Intent.ActionMain)
            .AddCategory(Intent.CategoryLauncher);
        PendingIntentFlags flags = Build.VERSION.SdkInt >= BuildVersionCodes.M ? 
            PendingIntentFlags.Immutable : 0;
        var disconnectPendingIntent = PendingIntent.GetBroadcast(this, 1, disconnectIntent, flags);
        var restartPendingIntent = PendingIntent.GetActivity(this, 1, restartIntent, flags);
        var builder = new NotificationCompat.Builder(this, Constants.NOTIFICATION_CHANNEL)
            .SetSmallIcon(Resource.Drawable.ic_notification)
            .SetColor(Resources?.GetColor(Resource.Color.colorPrimary) ?? 0)
            .SetContentTitle(Resources?.GetString(Resource.String.app_name))
            .SetContentText(_socket != null ? "Connected to " + _socket.GetName() : "Background Service")
            .SetContentIntent(restartPendingIntent)
            .SetOngoing(true)
            .AddAction(new NotificationCompat.Action(Resource.Drawable.ic_clear_white_24dp, "Disconnect", disconnectPendingIntent));
        
        var notification = builder.Build();
        StartForeground(Constants.NOTIFY_MANAGER_START_FOREGROUND_SERVICE, notification);
    }

    private void CancelNotification()
    {
        StopForeground(true);
    }

    // ISerialListener implementation
    public void OnSerialConnect()
    {
        if (_connected)
        {
            lock (this)
            {
                if (_listener != null)
                {
                    _mainLooper.Post(() =>
                    {
                        if (_listener != null)
                        {
                            _listener.OnSerialConnect();
                        }
                        else
                        {
                            _queue1.Enqueue(new QueueItem(QueueType.Connect));
                        }
                    });
                }
                else
                {
                    _queue2.Enqueue(new QueueItem(QueueType.Connect));
                }
            }
        }
    }

    public void OnSerialConnectError(Exception e)
    {
        if (_connected)
        {
            lock (this)
            {
                if (_listener != null)
                {
                    _mainLooper.Post(() =>
                    {
                        if (_listener != null)
                        {
                            _listener.OnSerialConnectError(e);
                        }
                        else
                        {
                            _queue1.Enqueue(new QueueItem(QueueType.ConnectError, e));
                            Disconnect();
                        }
                    });
                }
                else
                {
                    _queue2.Enqueue(new QueueItem(QueueType.ConnectError, e));
                    Disconnect();
                }
            }
        }
    }

    public void OnSerialRead(Queue<byte[]> datas)
    {
        throw new NotSupportedException();
    }

    public void OnSerialRead(byte[] data)
    {
        if (_connected)
        {
            lock (this)
            {
                if (_listener != null)
                {
                    bool first;
                    lock (_lastRead)
                    {
                        first = _lastRead.Datas?.Count == 0;
                        _lastRead.Add(data);
                    }
                    if (first)
                    {
                        _mainLooper.Post(() =>
                        {
                            Queue<byte[]> datas;
                            lock (_lastRead)
                            {
                                datas = _lastRead.Datas!;
                                _lastRead.Init();
                            }
                            if (_listener != null)
                            {
                                _listener.OnSerialRead(datas);
                            }
                            else
                            {
                                _queue1.Enqueue(new QueueItem(QueueType.Read, datas));
                            }
                        });
                    }
                }
                else
                {
                    if (_queue2.Count == 0 || _queue2.Last().Type != QueueType.Read)
                        _queue2.Enqueue(new QueueItem(QueueType.Read));
                    _queue2.Last().Add(data);
                }
            }
        }
    }

    public void OnSerialIoError(Exception e)
    {
        if (_connected)
        {
            lock (this)
            {
                if (_listener != null)
                {
                    _mainLooper.Post(() =>
                    {
                        if (_listener != null)
                        {
                            _listener.OnSerialIoError(e);
                        }
                        else
                        {
                            _queue1.Enqueue(new QueueItem(QueueType.IoError, e));
                            Disconnect();
                        }
                    });
                }
                else
                {
                    _queue2.Enqueue(new QueueItem(QueueType.IoError, e));
                    Disconnect();
                }
            }
        }
    }
}
