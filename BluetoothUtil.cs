using Android;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;
using AndroidX.Fragment.App;
using static Android.Provider.Settings;

namespace SimpleBluetoothTerminalNet;

internal static class BluetoothUtil
{
    public interface IPermissionGrantedCallback
    {
        void Call();
    }

    /// <summary>
    /// sort by name, then address. sort named devices first
    /// </summary>
    public static int CompareTo(BluetoothDevice? a, BluetoothDevice? b)
    {
        if (a == null || b == null) return 0;
        
        bool aValid = !string.IsNullOrEmpty(a.Name);
        bool bValid = !string.IsNullOrEmpty(b.Name);
        if (aValid && bValid)
        {
            int ret = string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            if (ret != 0) return ret;
            return string.Compare(a.Address, b.Address, StringComparison.Ordinal);
        }
        if (aValid) return -1;
        if (bValid) return +1;
        return string.Compare(a.Address, b.Address, StringComparison.Ordinal);
    }

    /// <summary>
    /// Android 12 permission handling
    /// </summary>
    private static void ShowRationaleDialog(Fragment fragment, EventHandler<DialogClickEventArgs> listener)
    {
        var builder = new AlertDialog.Builder(fragment.Activity);
        builder.SetTitle(fragment.GetString(Resource.String.bluetooth_permission_title));
        builder.SetMessage(fragment.GetString(Resource.String.bluetooth_permission_grant));
        builder.SetNegativeButton("Cancel", (IDialogInterfaceOnClickListener?)null);
        builder.SetPositiveButton("Continue", listener);
        builder.Show();
    }

    private static void ShowSettingsDialog(Fragment fragment)
    {
        string s = fragment.Resources?.GetString(
            fragment.Resources.GetIdentifier("@android:string/permgrouplab_nearby_devices", null, null)) ?? "Nearby devices";
        var builder = new AlertDialog.Builder(fragment.Activity);
        builder.SetTitle(fragment.GetString(Resource.String.bluetooth_permission_title));
        builder.SetMessage(string.Format(fragment.GetString(Resource.String.bluetooth_permission_denied), s));
        builder.SetNegativeButton("Cancel", (IDialogInterfaceOnClickListener?)null);
        builder.SetPositiveButton("Settings", (sender, args) =>
        {
            var intent = new Intent(ActionApplicationDetailsSettings,
                Android.Net.Uri.Parse("package:" + Constants.INTENT_ACTION_DISCONNECT.Split('.')[0] + "." + 
                                     Constants.INTENT_ACTION_DISCONNECT.Split('.')[1] + "." + 
                                     Constants.INTENT_ACTION_DISCONNECT.Split('.')[2]));
            fragment.StartActivity(intent);
        });
        builder.Show();
    }

    public static bool HasPermissions(Fragment fragment, ActivityResultLauncher requestPermissionLauncher)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.S)
            return true;
        bool missingPermissions = fragment.Activity?.CheckSelfPermission(Manifest.Permission.BluetoothConnect) != Permission.Granted;
        bool showRationale = fragment.ShouldShowRequestPermissionRationale(Manifest.Permission.BluetoothConnect);

        if (missingPermissions)
        {
            if (showRationale)
            {
                ShowRationaleDialog(fragment, (dialog, which) =>
                    requestPermissionLauncher.Launch(Manifest.Permission.BluetoothConnect));
            }
            else
            {
                requestPermissionLauncher.Launch(Manifest.Permission.BluetoothConnect);
            }
            return false;
        }
        else
        {
            return true;
        }
    }

    public static void OnPermissionsResult(Fragment fragment, bool granted, IPermissionGrantedCallback cb)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.S)
            return;
        bool showRationale = fragment.ShouldShowRequestPermissionRationale(Manifest.Permission.BluetoothConnect);
        if (granted)
        {
            cb.Call();
        }
        else if (showRationale)
        {
            ShowRationaleDialog(fragment, (dialog, which) => cb.Call());
        }
        else
        {
            ShowSettingsDialog(fragment);
        }
    }
}
