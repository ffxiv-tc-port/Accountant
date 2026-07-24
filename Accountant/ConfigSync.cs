using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Accountant.Gui.Timer;
using Accountant.Manager;
using Accountant.Timers;
using Accountant.Util;
using Dalamud.Plugin.Services;

namespace Accountant;

public class ConfigSync : IDisposable
{
    private readonly TimerManager      _manager;
    private readonly TimerWindow       _window;
    private readonly DemolitionManager _demoManager;
    private          int               _frameCounter;
    private          bool              _configReloadingAsync;

    public ConfigSync(TimerManager manager, TimerWindow window, DemolitionManager demoManager)
    {
        _manager                 =  manager;
        _window                  =  window;
        _demoManager             =  demoManager;
        Dalamud.Framework.Update += OnFramework;
    }

    private void OnFramework(IFramework _)
    {
        switch (_frameCounter++ % 128)
        {
            case 0:
                if (File.GetLastWriteTimeUtc(Dalamud.PluginInterface.ConfigFile.FullName) > Accountant.Config.LastChangeTime)
                    ReloadConfigAsync();

                break;
            case 10:
                CheckTimersFolder(_manager.AirshipTimers);
                break;
            case 20:
                CheckTimersFolder(_manager.PlotCropTimers);
                break;
            case 30:
                CheckTimersFolder(_manager.PrivateCropTimers);
                break;
            case 40:
                CheckTimersFolder(_manager.RetainerTimers);
                break;
            case 50:
                CheckTimersFolder(_manager.SubmersibleTimers);
                break;
            case 60:
                CheckTimersFolder(_manager.TaskTimers);
                break;
            case 70:
                CheckTimersFolder(_manager.WheelTimers);
                break;
            case 80:
                if (FreeCompanyStorage.GetWriteTime() > _manager.CompanyStorage.LastChangeTime)
                    _manager.CompanyStorage.ReloadAsync(() => _window.ResetCache());

                break;
            case 90:
                if (_demoManager.GetWriteTime() > _demoManager.LastChangeTime)
                    _demoManager.ReloadAsync(() =>
                    {
                        Dalamud.Log.Verbose("Reloaded {Timer:l} due to external changes.", typeof(DemolitionManager));
                        _window.ResetCache();
                    });

                break;
        }
    }

    // Same rationale as TimersBase.ReloadAsync(): AccountantConfiguration.Load() calls
    // IDalamudPluginInterface.GetPluginConfig(), which does a reflection-based type scan of this
    // assembly plus a File.ReadAllText + JsonConvert.DeserializeObject (and, occasionally, a
    // SavePluginConfig() write-back if defaults were missing) - none of that touches Dalamud's UI
    // or native game memory, so it is safe to run off the framework thread. Only the publish step
    // (swapping in the newly loaded config and notifying dependents) runs on the framework thread.
    private void ReloadConfigAsync()
    {
        if (_configReloadingAsync)
            return;
        _configReloadingAsync = true;

        Task.Run(() =>
        {
            var config = AccountantConfiguration.Load();
            Dalamud.Framework.RunOnFrameworkThread(() =>
            {
                Accountant.Config = config;
                _manager.CheckSettings();
                _window.ResetCache();
                Dalamud.Log.Verbose("Reloaded Config due to external changes.");
                _configReloadingAsync = false;
            });
        });
    }

    private static void CheckTimersFolder<T1, T2>(TimersBase<T1, T2> timer) where T1 : struct, ITimerIdentifier
    {
        var dir = timer.CreateFolder();
        if (dir.LastWriteTimeUtc <= timer.FileChangeTime
         && !dir.EnumerateFiles("*.json").Any(file => file.LastWriteTimeUtc > timer.FileChangeTime))
            return;

        timer.ReloadAsync();
        Dalamud.Log.Verbose("Reloaded {Timer:l} due to external changes.", typeof(T2).Name);
    }

    public void Dispose()
    {
        Dalamud.Framework.Update -= OnFramework;
    }
}
