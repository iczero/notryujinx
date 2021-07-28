using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using FluentAvalonia.Styling;
using System;
using System.Collections.Generic;

namespace Ryujinx.Ava.Common
{
    public sealed class StyleManager
    {
        private static StyleInclude _baseStyle => CreateStyle("avares://Ryujinx.Ava/Assets/Styles/Styles.xaml");

        private readonly List<Window> _windows;

        public StyleManager()
        {
            AvaloniaLocator.Current.GetService<FluentAvaloniaTheme>().RequestedTheme = "Dark";

            _windows = new List<Window>();
        }

        public void AddWindow(Window window)
        {
            if (window != null)
            {
                _windows.Add(window);

                if (window.Styles.Count == 0)
                {
                    // We add the style to the window styles section, so it will override the default style defined in App.xaml.
                    window.Styles.Add(_baseStyle);
                }
                else
                {
                    // If there are styles defined already, we assume that the first style imported it related to citrus.
                    // This allows one to override citrus styles.
                    window.Styles[0] = _baseStyle;
                }

                window.Closed += Window_Closed;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _windows.Remove(sender as Window);
        }

        private static StyleInclude CreateStyle(string url)
        {
            Uri self = new Uri("resm:Styles?assembly=Citrus.Avalonia.Sandbox");

            return new StyleInclude(self)
            {
                Source = new Uri(url)
            };
        }
    }
}