using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace STA.Calcolo.Desktop.Infrastructure;

/// <summary>Routes wheel input from horizontal nested viewers to their page.</summary>
public static class PageScrollBehavior
{
    public static readonly DependencyProperty TargetProperty=DependencyProperty.RegisterAttached(
        "Target",typeof(ScrollViewer),typeof(PageScrollBehavior),new PropertyMetadata(null,TargetChanged));
    public static void SetTarget(DependencyObject element,ScrollViewer? value)=>element.SetValue(TargetProperty,value);
    public static ScrollViewer? GetTarget(DependencyObject element)=>(ScrollViewer?)element.GetValue(TargetProperty);
    private static void TargetChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        if(d is not UIElement element)return;
        element.PreviewMouseWheel-=ForwardWheel;
        if(e.NewValue!=null)element.PreviewMouseWheel+=ForwardWheel;
    }
    private static void ForwardWheel(object sender,MouseWheelEventArgs e)
    {
        var page=GetTarget((DependencyObject)sender);if(page==null)return;
        double step=SystemParameters.WheelScrollLines<0?page.ViewportHeight:SystemParameters.WheelScrollLines*16;
        page.ScrollToVerticalOffset(page.VerticalOffset-e.Delta/120d*step);
        e.Handled=true;
    }
}
