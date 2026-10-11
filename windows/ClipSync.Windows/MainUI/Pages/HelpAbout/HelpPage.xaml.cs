using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI.Pages.HelpAbout;
public partial class HelpPage:System.Windows.Controls.UserControl,IDisposable
{
 readonly HelpPageViewModel viewModel;bool disposed;
 public HelpPage(ShellViewModel shell){InitializeComponent();viewModel=new(shell);DataContext=viewModel;Loaded+=OnLoaded;SizeChanged+=OnSizeChanged;}
 void OnLoaded(object sender,RoutedEventArgs e)=>AdaptLayout();
 void OnSizeChanged(object sender,SizeChangedEventArgs e)=>AdaptLayout();
 UniformGrid? FindGrid(DependencyObject parent){for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is UniformGrid grid)return grid;var found=FindGrid(child);if(found is not null)return found;}return null;}
 void AdaptLayout(){var columns=ActualWidth<700?1:ActualWidth<1000?2:3;var grids=FindGrid(GuideGrid);if(grids is not null){grids.Columns=columns;grids.Rows=(int)Math.Ceiling(6d/columns);}var steps=FindGrid(TroubleSteps);if(steps is not null)steps.Columns=ActualWidth<700?1:ActualWidth<1000?2:3;}
 public void Dispose(){if(disposed)return;disposed=true;Loaded-=OnLoaded;SizeChanged-=OnSizeChanged;viewModel.Dispose();DataContext=null;}
}
