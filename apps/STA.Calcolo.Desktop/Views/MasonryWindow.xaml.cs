using System.Windows;
using System.Windows.Input;
namespace STA.Calcolo.Desktop.Views;
public partial class MasonryWindow : Window
{
    public MasonryWindow() { InitializeComponent(); }
    private void PierScroll_PreviewMouseWheel(object sender,MouseWheelEventArgs e)
    {
        // Nested viewers scroll horizontally; the wheel belongs to the page.
        double step=SystemParameters.WheelScrollLines<0?PageScroll.ViewportHeight:SystemParameters.WheelScrollLines*16;
        PageScroll.ScrollToVerticalOffset(PageScroll.VerticalOffset-e.Delta/120d*step);
        e.Handled=true;
    }
}
