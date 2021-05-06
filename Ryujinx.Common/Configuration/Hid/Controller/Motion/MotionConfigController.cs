using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using NotImplementedException = System.NotImplementedException;

namespace Ryujinx.Common.Configuration.Hid.Controller.Motion
{
    public class MotionConfigController : INotifyPropertyChanged
    {
        [JsonIgnore]
        private double _gyroDeadzone;

        public MotionInputBackendType MotionBackend { get; set; }

        /// <summary>
        /// Gyro Sensitivity
        /// </summary>
        public int Sensitivity { get; set; }

        /// <summary>
        /// Gyro Deadzone
        /// </summary>
        public double GyroDeadzone
        {
            get => _gyroDeadzone; set
            {
                _gyroDeadzone = Math.Round(value, 3);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Enable Motion Controls
        /// </summary>
        public bool EnableMotion { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
