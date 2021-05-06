using Avalonia.Collections;
using LibHac.FsSystem;
using Ryujinx.Audio.Backends.OpenAL;
using Ryujinx.Audio.Backends.SDL2;
using Ryujinx.Audio.Backends.SoundIo;
using Ryujinx.Ava.Ui.Windows;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Configuration;
using Ryujinx.Configuration.System;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.FileSystem.Content;
using Ryujinx.HLE.HOS.Services.Time.TimeZone;
using System;
using System.Collections.Generic;
using TimeZone = Ryujinx.Ava.Ui.Windows.TimeZone;

namespace Ryujinx.Ava.Ui.ViewModels
{
    public class SettingsViewModel : BaseModel
    {
        private readonly ContentManager _contentManager;
        private readonly List<string> _validTzRegions;
        private readonly VirtualFileSystem _virtualFileSystem;
        private float _customResolutionScale;

        private int _resolutionScale;

        private TimeZoneContentManager _timeZoneContentManager;

        public SettingsViewModel(VirtualFileSystem virtualFileSystem, ContentManager contentManager) : this()
        {
            _virtualFileSystem = virtualFileSystem;
            _contentManager = contentManager;

            LoadTimeZones();
        }

        public SettingsViewModel()
        {
            GameDirectories = new AvaloniaList<string>();
            TimeZones = new AvaloniaList<TimeZone>();
            _validTzRegions = new List<string>();

            CheckSoundBackends();

            LoadCurrentConfiguration();
        }

        public AvaloniaList<string> GameDirectories { get; set; }

        public bool EnableDiscordIntegration { get; set; }
        public bool CheckUpdatesOnStart { get; set; }
        public bool ShowConfirmExit { get; set; }
        public bool HideCursorOnIdle { get; set; }
        public bool EnableDockedMode { get; set; }
        public bool EnableKeyboard { get; set; }
        public bool EnableVsync { get; set; }
        public bool EnablePptc { get; set; }
        public bool EnableFsIntegrityChecks { get; set; }
        public bool IgnoreMissingServices { get; set; }
        public bool ExpandDramSize { get; set; }
        public bool EnableShaderCache { get; set; }
        public bool EnableFileLog { get; set; }
        public bool EnableStub { get; set; }
        public bool EnableInfo { get; set; }
        public bool EnableWarn { get; set; }
        public bool EnableError { get; set; }
        public bool EnableGuest { get; set; }
        public bool EnableFsAccessLog { get; set; }
        public bool EnableDebug { get; set; }

        public string TimeZone { get; set; }
        public string ShaderDumpPath { get; set; }

        public int Language { get; set; }
        public int Region { get; set; }
        public int FsGlobalAccessLogMode { get; set; }
        public int AudioBackend { get; set; }
        public int MaxAnisotropy { get; set; }
        public int AspectRatio { get; set; }
        public int OpenglDebugLevel { get; set; }
        public bool IsOpenAlEnabled { get; set; }
        public bool IsSoundIoEnabled { get; set; }
        public bool IsSDL2Enabled { get; set; }
        public bool IsResolutionScaleActive => _resolutionScale == 0;

        public DateTimeOffset DateOffset { get; set; }
        public TimeSpan TimeOffset { get; set; }

        public AvaloniaList<TimeZone> TimeZones { get; set; }

        public int ResolutionScale
        {
            get => _resolutionScale;
            set
            {
                _resolutionScale = value;

                OnPropertyChanged("CustomResolutionScale");
                OnPropertyChanged("IsResolutionScaleActive");
            }
        }

        public float CustomResolutionScale
        {
            get => _customResolutionScale;
            set
            {
                _customResolutionScale = MathF.Round(value, 2);

                OnPropertyChanged();
            }
        }

        public void CheckSoundBackends()
        {
            IsOpenAlEnabled = OpenALHardwareDeviceDriver.IsSupported;
            IsSoundIoEnabled = SoundIoHardwareDeviceDriver.IsSupported;
            IsSDL2Enabled = SDL2HardwareDeviceDriver.IsSupported;
        }

        public void LoadTimeZones()
        {
            _timeZoneContentManager = new TimeZoneContentManager();
            _timeZoneContentManager.InitializeInstance(_virtualFileSystem, _contentManager, IntegrityCheckLevel.None);

            foreach ((int offset, string location, string abbr) in _timeZoneContentManager.ParseTzOffsets())
            {
                int hours = Math.DivRem(offset, 3600, out int seconds);
                int minutes = Math.Abs(seconds) / 60;

                string abbr2 = abbr.StartsWith('+') || abbr.StartsWith('-') ? string.Empty : abbr;

                TimeZones.Add(new TimeZone($"UTC{hours:+0#;-0#;+00}:{minutes:D2}", location, abbr2));

                _validTzRegions.Add(location);
            }
        }

        public void ValidateAndSetTimeZone(string location)
        {
            if (_validTzRegions.Contains(location))
            {
                TimeZone = location;

                OnPropertyChanged("TimeZone");
            }
        }


        public void LoadCurrentConfiguration()
        {
            ConfigurationState config = ConfigurationState.Instance;

            GameDirectories.Clear();
            GameDirectories.AddRange(config.Ui.GameDirs.Value);

            EnableDiscordIntegration = config.EnableDiscordIntegration;
            CheckUpdatesOnStart = config.CheckUpdatesOnStart;
            ShowConfirmExit = config.ShowConfirmExit;
            HideCursorOnIdle = config.HideCursorOnIdle;
            EnableDockedMode = config.System.EnableDockedMode;
            EnableKeyboard = config.Hid.EnableKeyboard;
            EnableVsync = config.Graphics.EnableVsync;
            EnablePptc = config.System.EnablePtc;
            EnableFsIntegrityChecks = config.System.EnableFsIntegrityChecks;
            IgnoreMissingServices = config.System.IgnoreMissingServices;
            ExpandDramSize = config.System.ExpandRam;
            EnableShaderCache = config.Graphics.EnableShaderCache;
            EnableFileLog = config.Logger.EnableFileLog;
            EnableStub = config.Logger.EnableStub;
            EnableInfo = config.Logger.EnableInfo;
            EnableWarn = config.Logger.EnableWarn;
            EnableError = config.Logger.EnableError;
            EnableGuest = config.Logger.EnableGuest;
            EnableDebug = config.Logger.EnableDebug;
            EnableFsAccessLog = config.Logger.EnableFsAccessLog;

            OpenglDebugLevel = (int)config.Logger.GraphicsDebugLevel.Value;

            TimeZone = config.System.TimeZone;
            ShaderDumpPath = config.Graphics.ShadersDumpPath;

            Language = (int)config.System.Language.Value;
            Region = (int)config.System.Region.Value;
            FsGlobalAccessLogMode = config.System.FsGlobalAccessLogMode;
            AudioBackend = (int)config.System.AudioBackend.Value;

            float anisotropy = config.Graphics.MaxAnisotropy;
            MaxAnisotropy = anisotropy == -1 ? 0 : (int)(MathF.Sqrt(anisotropy) - 1);

            AspectRatio = (int)config.Graphics.AspectRatio.Value;

            int resolution = config.Graphics.ResScale;
            ResolutionScale = resolution == -1 ? 0 : resolution;
            CustomResolutionScale = config.Graphics.ResScaleCustom;

            long systemTimeOffset = config.System.SystemTimeOffset;

            DateTime dateTimeOffset = DateTime.Now.AddSeconds(systemTimeOffset);

            DateOffset = dateTimeOffset.Date;
            TimeOffset = dateTimeOffset.TimeOfDay;
        }

        public void SaveSettings()
        {
            List<string> gameDirs = new(GameDirectories);

            if (_validTzRegions.Contains(TimeZone))
            {
                ConfigurationState.Instance.System.TimeZone.Value = TimeZone;
            }

            ConfigurationState.Instance.Logger.EnableError.Value = EnableError;
            ConfigurationState.Instance.Logger.EnableWarn.Value = EnableWarn;
            ConfigurationState.Instance.Logger.EnableInfo.Value = EnableInfo;
            ConfigurationState.Instance.Logger.EnableStub.Value = EnableStub;
            ConfigurationState.Instance.Logger.EnableDebug.Value = EnableDebug;
            ConfigurationState.Instance.Logger.EnableGuest.Value = EnableGuest;
            ConfigurationState.Instance.Logger.EnableFsAccessLog.Value = EnableFsAccessLog;
            ConfigurationState.Instance.Logger.EnableFileLog.Value = EnableFileLog;
            ConfigurationState.Instance.Logger.GraphicsDebugLevel.Value = (GraphicsDebugLevel)OpenglDebugLevel;
            ConfigurationState.Instance.System.EnableDockedMode.Value = EnableDockedMode;
            ConfigurationState.Instance.EnableDiscordIntegration.Value = EnableDiscordIntegration;
            ConfigurationState.Instance.CheckUpdatesOnStart.Value = CheckUpdatesOnStart;
            ConfigurationState.Instance.ShowConfirmExit.Value = ShowConfirmExit;
            ConfigurationState.Instance.HideCursorOnIdle.Value = HideCursorOnIdle;
            ConfigurationState.Instance.Graphics.EnableVsync.Value = EnableVsync;
            ConfigurationState.Instance.Graphics.EnableShaderCache.Value = EnableShaderCache;
            ConfigurationState.Instance.System.EnablePtc.Value = EnablePptc;
            ConfigurationState.Instance.System.EnableFsIntegrityChecks.Value = EnableFsIntegrityChecks;
            ConfigurationState.Instance.System.IgnoreMissingServices.Value = IgnoreMissingServices;
            ConfigurationState.Instance.System.ExpandRam.Value = ExpandDramSize;
            ConfigurationState.Instance.Hid.EnableKeyboard.Value = EnableKeyboard;
            ConfigurationState.Instance.System.Language.Value = (Language)Language;
            ConfigurationState.Instance.System.Region.Value = (Region)Region;

            TimeSpan systemTimeOffset = DateOffset - DateTime.Now;

            ConfigurationState.Instance.System.SystemTimeOffset.Value = systemTimeOffset.Seconds;
            ConfigurationState.Instance.Graphics.ShadersDumpPath.Value = ShaderDumpPath;
            ConfigurationState.Instance.Ui.GameDirs.Value = gameDirs;
            ConfigurationState.Instance.System.FsGlobalAccessLogMode.Value = FsGlobalAccessLogMode;

            float anisotropy = MaxAnisotropy == 0 ? -1 : MathF.Pow(MaxAnisotropy, 2);

            ConfigurationState.Instance.Graphics.MaxAnisotropy.Value = anisotropy;
            ConfigurationState.Instance.Graphics.AspectRatio.Value = (AspectRatio)AspectRatio;
            ConfigurationState.Instance.Graphics.ResScale.Value = ResolutionScale == 0 ? -1 : ResolutionScale;
            ConfigurationState.Instance.Graphics.ResScaleCustom.Value = CustomResolutionScale;

            AudioBackend audioBackend = (AudioBackend)AudioBackend;
            if (audioBackend != ConfigurationState.Instance.System.AudioBackend.Value)
            {
                ConfigurationState.Instance.System.AudioBackend.Value = audioBackend;

                Logger.Info?.Print(LogClass.Application, $"AudioBackend toggled to: {audioBackend}");
            }

            ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
            MainWindow.UpdateGraphicsConfig();
        }
    }
}