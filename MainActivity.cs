using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;
using AndroidX.Fragment.App;

namespace SimpleBluetoothTerminalNet;

[Activity(Label = "@string/app_name", MainLauncher = true, Exported = true, WindowSoftInputMode = Android.Views.SoftInput.StateHidden | Android.Views.SoftInput.AdjustResize)]
public class MainActivity : AppCompatActivity, FragmentManager.IOnBackStackChangedListener
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);
        var toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
        SetSupportActionBar(toolbar);
        SupportFragmentManager.AddOnBackStackChangedListener(this);
        if (savedInstanceState == null)
            SupportFragmentManager.BeginTransaction().Add(Resource.Id.fragment, new DevicesFragment(), "devices").Commit();
        else
            OnBackStackChanged();
    }

    public void OnBackStackChanged()
    {
        SupportActionBar?.SetDisplayHomeAsUpEnabled(SupportFragmentManager.BackStackEntryCount > 0);
    }

    public override bool OnSupportNavigateUp()
    {
        OnBackPressed();
        return true;
    }
}
