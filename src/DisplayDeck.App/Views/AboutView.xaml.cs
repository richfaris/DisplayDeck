using System.Windows.Controls;
using DisplayDeck.App.ViewModels;

namespace DisplayDeck.App.Views;

public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
        DataContext = new AboutViewModel();
    }
}
