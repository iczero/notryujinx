using Avalonia.Media;
using Avalonia.Threading;
using Ryujinx.Ava.Ui.Windows;
using Ryujinx.HLE.Ui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.Ava.Ui.Applet
{
    class AvaloniaHostUiTheme : IHostUiTheme
    {
        private readonly StyleableWindow _parent;

        public AvaloniaHostUiTheme(StyleableWindow parent)
        {
            _parent = parent;
        }

        public string FontFamily
        {
            get
            {
                string fontFamily = string.Empty;

                Dispatcher.UIThread.InvokeAsync(() => { fontFamily = _parent.FontFamily.Name; }).Wait();

                return fontFamily;
            }
        }

        public ThemeColor DefaultBackgroundColor
        {
            get
            {
                ThemeColor color = new ThemeColor();

                Dispatcher.UIThread.InvokeAsync(() => { color = BrushToThemeColor(_parent.Background); }).Wait();

                return color;
            }
        }

        public ThemeColor DefaultForegroundColor
        {
            get
            {
                ThemeColor color = new ThemeColor();

                Dispatcher.UIThread.InvokeAsync(() => { color = BrushToThemeColor(_parent.Foreground); }).Wait();

                return color;
            }
        }

        public ThemeColor DefaultBorderColor
        {
            get
            {
                _parent.Styles.Resources.TryGetValue("ThemeControlBorderColor", out var color);

                return ColorToThemeColor((Color)color);
            }
        }

        public ThemeColor SelectionBackgroundColor
        {
            get
            {
                _parent.Styles.Resources.TryGetValue("SystemAccentColor", out var color);

                return ColorToThemeColor((Color)color);
            }
        }

        public ThemeColor SelectionForegroundColor
        {
            get
            {
                _parent.Styles.Resources.TryGetValue("TextOnAccentFillColorSelectedText", out var color);

                return ColorToThemeColor((Color)color);
            }
        }

        private ThemeColor BrushToThemeColor(IBrush brush)
        {
            if (brush is SolidColorBrush solidColor)
            {
                return new ThemeColor((float)solidColor.Color.A / 255,
                    (float)solidColor.Color.R / 255,
                    (float)solidColor.Color.G / 255,
                    (float)solidColor.Color.B / 255);
            }
            else return new ThemeColor();
        }

        private ThemeColor ColorToThemeColor(Color color)
        {
                return new ThemeColor((float)color.A / 255,
                    (float)color.R / 255,
                    (float)color.G / 255,
                    (float)color.B / 255);
        }
    }
}
