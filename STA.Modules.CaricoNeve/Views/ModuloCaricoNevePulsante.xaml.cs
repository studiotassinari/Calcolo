using System.Windows.Controls;
using Microsoft.Practices.Prism.Regions;
using STA.Modules.CaricoNeve.ViewModels;

namespace STA.Modules.CaricoNeve.Views
{
    /// <summary>
    /// Logica di interazione per ModuloCaricoNevePulsante.xaml
    /// </summary>
    [ViewSortHint("01")]
    public partial class ModuloCaricoNevePulsante : UserControl
    {
        public ModuloCaricoNevePulsante(ModuloCaricoNevePulsanteViewModel viewmodel)
        {
            InitializeComponent();
            this.DataContext = viewmodel;
        }
    }
}
