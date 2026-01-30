using Android;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Text;
using Android.Text.Method;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using Fragment = AndroidX.Fragment.App.Fragment;

namespace SimpleBluetoothTerminalNet;

public class TerminalFragment : Fragment, IServiceConnection, ISerialListener
{
    private enum Connected { False, Pending, True }

    private string? _deviceAddress;
    private SerialService? _service;

    private TextView? _receiveText;
    private TextView? _sendText;
    private TextUtil.HexWatcher? _hexWatcher;

    private Connected _connected = Connected.False;
    private bool _initialStart = true;
    private bool _hexEnabled = false;
    private bool _pendingNewline = false;
    private string _newline = TextUtil.NewlineCrlf;

    public override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        HasOptionsMenu = true;
        RetainInstance = true;
        _deviceAddress = Arguments?.GetString("device");
    }

    public override void OnDestroy()
    {
        if (_connected != Connected.False)
            Disconnect();
        Activity?.StopService(new Intent(Activity, typeof(SerialService)));
        base.OnDestroy();
    }

    public override void OnStart()
    {
        base.OnStart();
        if (_service != null)
            _service.Attach(this);
        else
            Activity?.StartService(new Intent(Activity, typeof(SerialService)));
    }

    public override void OnStop()
    {
        if (_service != null && Activity?.IsChangingConfigurations == false)
            _service.Detach();
        base.OnStop();
    }

    public override void OnAttach(Context context)
    {
        base.OnAttach(context);
        Activity?.BindService(new Intent(Activity, typeof(SerialService)), this, Bind.AutoCreate);
    }

    public override void OnDetach()
    {
        try { Activity?.UnbindService(this); } catch (Exception) { }
        base.OnDetach();
    }

    public override void OnResume()
    {
        base.OnResume();
        if (_initialStart && _service != null)
        {
            _initialStart = false;
            Activity?.RunOnUiThread(Connect);
        }
    }

    public void OnServiceConnected(ComponentName? name, IBinder? service)
    {
        _service = ((SerialService.SerialBinder?)service)?.GetService();
        _service?.Attach(this);
        if (_initialStart && IsResumed)
        {
            _initialStart = false;
            Activity?.RunOnUiThread(Connect);
        }
    }

    public void OnServiceDisconnected(ComponentName? name)
    {
        _service = null;
    }

    public override View? OnCreateView(LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
    {
        var view = inflater.Inflate(Resource.Layout.fragment_terminal, container, false);
        _receiveText = view.FindViewById<TextView>(Resource.Id.receive_text);
        _receiveText!.SetTextColor(Resources!.GetColor(Resource.Color.colorRecieveText));
        _receiveText.MovementMethod = new ScrollingMovementMethod();

        _sendText = view.FindViewById<TextView>(Resource.Id.send_text);
        _hexWatcher = new TextUtil.HexWatcher(_sendText!);
        _hexWatcher.Enable(_hexEnabled);
        _sendText.AddTextChangedListener(_hexWatcher);
        _sendText.Hint = _hexEnabled ? "HEX mode" : "";

        var sendBtn = view.FindViewById<View>(Resource.Id.send_btn);
        sendBtn!.Click += (s, e) => Send(_sendText.Text ?? "");
        return view;
    }

    public override void OnCreateOptionsMenu(IMenu menu, MenuInflater inflater)
    {
        inflater.Inflate(Resource.Menu.menu_terminal, menu);
    }

    public override void OnPrepareOptionsMenu(IMenu menu)
    {
        menu.FindItem(Resource.Id.hex)?.SetChecked(_hexEnabled);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            menu.FindItem(Resource.Id.backgroundNotification)?.SetChecked(_service?.AreNotificationsEnabled() ?? false);
        }
        else
        {
            menu.FindItem(Resource.Id.backgroundNotification)?.SetChecked(true);
            menu.FindItem(Resource.Id.backgroundNotification)?.SetEnabled(false);
        }
    }

    public override bool OnOptionsItemSelected(IMenuItem item)
    {
        int id = item.ItemId;
        if (id == Resource.Id.clear)
        {
            _receiveText!.Text = "";
            return true;
        }
        else if (id == Resource.Id.newline)
        {
            string[] newlineNames = Resources!.GetStringArray(Resource.Array.newline_names)!;
            string[] newlineValues = Resources.GetStringArray(Resource.Array.newline_values)!;
            int pos = Array.IndexOf(newlineValues, _newline);
            var builder = new AlertDialog.Builder(Activity);
            builder.SetTitle("Newline");
            builder.SetSingleChoiceItems(newlineNames, pos, (sender, args) =>
            {
                _newline = newlineValues[args.Which];
                ((AlertDialog)sender!).Dismiss();
            });
            builder.Create().Show();
            return true;
        }
        else if (id == Resource.Id.hex)
        {
            _hexEnabled = !_hexEnabled;
            _sendText!.Text = "";
            _hexWatcher!.Enable(_hexEnabled);
            _sendText.Hint = _hexEnabled ? "HEX mode" : "";
            item.SetChecked(_hexEnabled);
            return true;
        }
        else if (id == Resource.Id.backgroundNotification)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                if (_service?.AreNotificationsEnabled() == false && Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                {
                    RequestPermissions(new[] { Manifest.Permission.PostNotifications }, 0);
                }
                else
                {
                    ShowNotificationSettings();
                }
            }
            return true;
        }
        else
        {
            return base.OnOptionsItemSelected(item);
        }
    }

    private void Connect()
    {
        try
        {
            var bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
            var device = bluetoothAdapter?.GetRemoteDevice(_deviceAddress);
            Status("connecting...");
            _connected = Connected.Pending;
            var socket = new SerialSocket(Activity!.ApplicationContext!, device!);
            _service?.Connect(socket);
        }
        catch (Exception e)
        {
            OnSerialConnectError(e);
        }
    }

    private void Disconnect()
    {
        _connected = Connected.False;
        _service?.Disconnect();
    }

    private void Send(string str)
    {
        if (_connected != Connected.True)
        {
            Toast.MakeText(Activity, "not connected", ToastLength.Short)?.Show();
            return;
        }
        try
        {
            string msg;
            byte[] data;
            if (_hexEnabled)
            {
                var sb = new System.Text.StringBuilder();
                TextUtil.ToHexString(sb, TextUtil.FromHexString(str));
                TextUtil.ToHexString(sb, System.Text.Encoding.ASCII.GetBytes(_newline));
                msg = sb.ToString();
                data = TextUtil.FromHexString(msg);
            }
            else
            {
                msg = str;
                data = System.Text.Encoding.ASCII.GetBytes(str + _newline);
            }
            var spn = new SpannableStringBuilder(msg + '\n');
            spn.SetSpan(new ForegroundColorSpan(Resources!.GetColor(Resource.Color.colorSendText)), 0, spn.Length(), SpanTypes.ExclusiveExclusive);
            _receiveText!.Append(spn);
            _service?.Write(data);
        }
        catch (Exception e)
        {
            OnSerialIoError(e);
        }
    }

    private void Receive(Queue<byte[]> datas)
    {
        var spn = new SpannableStringBuilder();
        foreach (var data in datas)
        {
            if (_hexEnabled)
            {
                spn.Append(TextUtil.ToHexString(data)).Append('\n');
            }
            else
            {
                string msg = System.Text.Encoding.ASCII.GetString(data);
                if (_newline.Equals(TextUtil.NewlineCrlf) && msg.Length > 0)
                {
                    msg = msg.Replace(TextUtil.NewlineCrlf, TextUtil.NewlineLf);
                    if (_pendingNewline && msg[0] == '\n')
                    {
                        if (spn.Length() >= 2)
                        {
                            spn.Delete(spn.Length() - 2, spn.Length());
                        }
                        else
                        {
                            var edt = _receiveText!.EditableText;
                            if (edt != null && edt.Length() >= 2)
                                edt.Delete(edt.Length() - 2, edt.Length());
                        }
                    }
                    _pendingNewline = msg.Length > 0 && msg[msg.Length - 1] == '\r';
                }
                spn.Append(TextUtil.ToCaretString(new Java.Lang.String(msg), _newline.Length != 0));
            }
        }
        _receiveText!.Append(spn);
    }

    private void Status(string str)
    {
        var spn = new SpannableStringBuilder(str + '\n');
        spn.SetSpan(new ForegroundColorSpan(Resources!.GetColor(Resource.Color.colorStatusText)), 0, spn.Length(), SpanTypes.ExclusiveExclusive);
        _receiveText!.Append(spn);
    }

    private void ShowNotificationSettings()
    {
        var intent = new Intent();
        intent.SetAction("android.settings.APP_NOTIFICATION_SETTINGS");
        intent.PutExtra("android.provider.extra.APP_PACKAGE", Activity?.PackageName);
        StartActivity(intent);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        if (permissions.SequenceEqual(new[] { Manifest.Permission.PostNotifications }) &&
            Build.VERSION.SdkInt >= BuildVersionCodes.O && _service?.AreNotificationsEnabled() == false)
            ShowNotificationSettings();
    }

    // ISerialListener implementation
    public void OnSerialConnect()
    {
        Status("connected");
        _connected = Connected.True;
    }

    public void OnSerialConnectError(Exception e)
    {
        Status("connection failed: " + e.Message);
        Disconnect();
    }

    public void OnSerialRead(byte[] data)
    {
        var datas = new Queue<byte[]>();
        datas.Enqueue(data);
        Receive(datas);
    }

    public void OnSerialRead(Queue<byte[]> datas)
    {
        Receive(datas);
    }

    public void OnSerialIoError(Exception e)
    {
        Status("connection lost: " + e.Message);
        Disconnect();
    }
}
