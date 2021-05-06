using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using System;
using System.Collections.Generic;

namespace Ryujinx.Ava.Ui.Styles
{
    public sealed class StyleManager
    {
        private readonly List<Window> _windows;

        public StyleManager()
        {
            _windows = new List<Window>();
        }

        public StyleInclude BaseStyle => CreateStyle("avares://Ryujinx.Ava/Ui/Styles/Styles.xaml");

        public void AddWindow(Window window)
        {
            if (window != null)
            {
                _windows.Add(window);

                // We add the style to the window styles section, so it
                // will override the default style defined in App.xaml. 
                if (window.Styles.Count == 0)
                {
                    window.Styles.Add(BaseStyle);
                }

                // If there are styles defined already, we assume that
                // the first style imported it related to citrus.
                // This allows one to override citrus styles.
                else
                {
                    window.Styles[0] = BaseStyle;
                }

                window.Closed += Window_Closed;
            }
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            _windows.Remove(sender as Window);
        }

        private static StyleInclude CreateStyle(string url)
        {
            Uri self = new("resm:Styles?assembly=Citrus.Avalonia.Sandbox");
            return new StyleInclude(self) {Source = new Uri(url)};
        }
    }
}