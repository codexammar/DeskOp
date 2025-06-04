using System.Windows;
using System.Windows.Controls;

namespace DeskOp
{
    public partial class SnapHintOverlay : Window
    {
        public SnapHintOverlay()
        {
            InitializeComponent();
            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = SystemParameters.PrimaryScreenHeight;
            this.Left = 0;
            this.Top = 0;
        }

        public void ShowHint(Rect rect)
        {
            HintBorder.Width = rect.Width;
            HintBorder.Height = rect.Height;
            Canvas.SetLeft(HintBorder, rect.Left);
            Canvas.SetTop(HintBorder, rect.Top);
            HintBorder.Visibility = Visibility.Visible;
        }

        public void HideHint()
        {
            HintBorder.Visibility = Visibility.Collapsed;
        }
    }
}
