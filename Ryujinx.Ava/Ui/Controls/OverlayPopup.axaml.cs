using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;

namespace Ryujinx.Ava.Ui.Controls
{
    public partial class OverlayPopup : Popup
    {
        public ContentDialog ContentDialog { get; private set; }
        public OverlayPopup()
        {
            InitializeComponent();

            IsLightDismissEnabled = false;

            PlacementMode = PlacementMode.AnchorAndGravity;
            PlacementGravity = Avalonia.Controls.Primitives.PopupPositioning.PopupGravity.TopLeft;
            PlacementAnchor = Avalonia.Controls.Primitives.PopupPositioning.PopupAnchor.TopLeft;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            ContentDialog = this.FindControl<ContentDialog>("ContentDialog");
        }
    }
}
