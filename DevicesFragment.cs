using Android;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;
using Fragment = AndroidX.Fragment.App.Fragment;
using ListFragment = AndroidX.Fragment.App.ListFragment;
using System.Collections.Generic;
using System.Linq;

namespace SimpleBluetoothTerminalNet;

public class DevicesFragment : ListFragment
{
    private BluetoothAdapter? _bluetoothAdapter;
    private readonly List<BluetoothDevice> _listItems = new List<BluetoothDevice>();
    private ArrayAdapter<BluetoothDevice>? _listAdapter;
    private ActivityResultLauncher? _requestBluetoothPermissionLauncherForRefresh;
    private IMenu? _menu;
    private bool _permissionMissing;

    public override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        HasOptionsMenu = true;
        if (Activity?.PackageManager?.HasSystemFeature(PackageManager.FeatureBluetooth) == true)
            _bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
        
        _listAdapter = new DeviceListAdapter(Activity!, _listItems);
        
        _requestBluetoothPermissionLauncherForRefresh = RegisterForActivityResult(
            new ActivityResultContracts.RequestPermission(),
            new PermissionCallback(this));
    }

    public override void OnActivityCreated(Bundle? savedInstanceState)
    {
        base.OnActivityCreated(savedInstanceState);
        ListAdapter = null;
        var header = Activity!.LayoutInflater!.Inflate(Resource.Layout.device_list_header, null, false);
        ListView!.AddHeaderView(header, null, false);
        SetEmptyText("initializing...");
        ((TextView)ListView.EmptyView!).TextSize = 18;
        ListAdapter = _listAdapter;
    }

    public override void OnCreateOptionsMenu(IMenu menu, MenuInflater inflater)
    {
        _menu = menu;
        inflater.Inflate(Resource.Menu.menu_devices, menu);
        if (_permissionMissing)
            menu.FindItem(Resource.Id.bt_refresh)?.SetVisible(true);
        if (_bluetoothAdapter == null)
            menu.FindItem(Resource.Id.bt_settings)?.SetEnabled(false);
    }

    public override void OnResume()
    {
        base.OnResume();
        Refresh();
    }

    public override bool OnOptionsItemSelected(IMenuItem item)
    {
        int id = item.ItemId;
        if (id == Resource.Id.bt_settings)
        {
            var intent = new Intent();
            intent.SetAction(Android.Provider.Settings.ActionBluetoothSettings);
            StartActivity(intent);
            return true;
        }
        else if (id == Resource.Id.bt_refresh)
        {
            if (BluetoothUtil.HasPermissions(this, _requestBluetoothPermissionLauncherForRefresh!))
                Refresh();
            return true;
        }
        else
        {
            return base.OnOptionsItemSelected(item);
        }
    }

    private void Refresh()
    {
        _listItems.Clear();
        if (_bluetoothAdapter != null)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            {
                _permissionMissing = Activity?.CheckSelfPermission(Manifest.Permission.BluetoothConnect) != Permission.Granted;
                if (_menu != null && _menu.FindItem(Resource.Id.bt_refresh) != null)
                    _menu.FindItem(Resource.Id.bt_refresh)?.SetVisible(_permissionMissing);
            }
            if (!_permissionMissing)
            {
                var bondedDevices = _bluetoothAdapter.BondedDevices;
                if (bondedDevices != null)
                {
                    foreach (var device in bondedDevices)
                    {
                        if (device.Type != BluetoothDeviceType.Le)
                            _listItems.Add(device);
                    }
                }
                _listItems.Sort((a, b) => BluetoothUtil.CompareTo(a, b));
            }
        }
        if (_bluetoothAdapter == null)
            SetEmptyText("<bluetooth not supported>");
        else if (_bluetoothAdapter.IsEnabled == false)
            SetEmptyText("<bluetooth is disabled>");
        else if (_permissionMissing)
            SetEmptyText("<permission missing, use REFRESH>");
        else
            SetEmptyText("<no bluetooth devices found>");
        _listAdapter?.NotifyDataSetChanged();
    }

    public override void OnListItemClick(ListView l, View v, int position, long id)
    {
        var device = _listItems[position - 1];
        var args = new Bundle();
        args.PutString("device", device.Address);
        var fragment = new TerminalFragment();
        fragment.Arguments = args;
        ParentFragmentManager.BeginTransaction().Replace(Resource.Id.fragment, fragment, "terminal").AddToBackStack(null).Commit();
    }

    private class PermissionCallback : Java.Lang.Object, IActivityResultCallback
    {
        private readonly DevicesFragment _fragment;

        public PermissionCallback(DevicesFragment fragment)
        {
            _fragment = fragment;
        }

        public void OnActivityResult(Java.Lang.Object? result)
        {
            bool granted = result is Java.Lang.Boolean boolResult && boolResult.BooleanValue();
            BluetoothUtil.OnPermissionsResult(_fragment, granted, new PermissionGrantedCallbackImpl(_fragment));
        }
    }

    private class PermissionGrantedCallbackImpl : Java.Lang.Object, BluetoothUtil.IPermissionGrantedCallback
    {
        private readonly DevicesFragment _fragment;

        public PermissionGrantedCallbackImpl(DevicesFragment fragment)
        {
            _fragment = fragment;
        }

        public void Call()
        {
            _fragment.Refresh();
        }
    }

    private class DeviceListAdapter : ArrayAdapter<BluetoothDevice>
    {
        private readonly List<BluetoothDevice> _items;

        public DeviceListAdapter(Context context, List<BluetoothDevice> items)
            : base(context, 0, items)
        {
            _items = items;
        }

        public override View? GetView(int position, View? convertView, ViewGroup parent)
        {
            var device = _items[position];
            if (convertView == null)
                convertView = ((Activity)Context).LayoutInflater.Inflate(Resource.Layout.device_list_item, parent, false);
            var text1 = convertView!.FindViewById<TextView>(Resource.Id.text1);
            var text2 = convertView.FindViewById<TextView>(Resource.Id.text2);
            string? deviceName = device.Name;
            text1!.Text = deviceName;
            text2!.Text = device.Address;
            return convertView;
        }
    }
}
