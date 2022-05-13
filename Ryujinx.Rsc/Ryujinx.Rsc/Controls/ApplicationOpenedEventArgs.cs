using Avalonia.Interactivity;
using Ryujinx.Rsc.Library.Common;

namespace Ryujinx.Rsc.Controls
{
    public class ApplicationOpenedEventArgs : RoutedEventArgs
    {
        public ApplicationData Application { get; }

        public ApplicationOpenedEventArgs(ApplicationData application, RoutedEvent routedEvent)
        {
            Application = application;
            RoutedEvent = routedEvent;
        }
    }
}