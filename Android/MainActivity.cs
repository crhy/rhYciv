using Android.App;
using Android.OS;
using Android.Widget;
using Raylib_CSharp;

namespace RhYciv.Android;

[Activity(Label = "rhYciv", MainActivity = true)]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // First mobile milestone: prove the APK pipeline works end to end -
        // game code compiles for net9.0-android and the embedded arm64 libraylib.so
        // actually dlopens. Raylib's window/game loop needs a native EGL surface,
        // which a Java-hosted activity does not provide yet, so we do not call
        // Window.Init here. See docs/MOBILE-PORT.md.
        string status;
        try
        {
            status = "rhYciv mobile groundwork: raylib " + Raylib.GetVersion() +
                     " loaded from the embedded native library. Game loop not yet ported.";
        }
        catch (Exception ex)
        {
            status = "Failed to load native raylib: " + ex.Message;
        }

        var view = new TextView(this);
        view.Text = status;
        SetContentView(view);
    }
}
