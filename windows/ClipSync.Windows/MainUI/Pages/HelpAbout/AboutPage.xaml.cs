using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI.Pages.HelpAbout;
public partial class AboutPage : System.Windows.Controls.UserControl, IDisposable
{
 AboutPageViewModel viewModel; bool disposed;
 public AboutPage(ShellViewModel shell){InitializeComponent();viewModel=new(shell);DataContext=viewModel;SizeChanged+=AboutPage_SizeChanged;Loaded+=(_,_)=>AdaptLayout();}
 void AboutPage_SizeChanged(object sender,SizeChangedEventArgs e)=>AdaptLayout();
 UniformGrid? FindUniformGrid(DependencyObject root){for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is UniformGrid grid)return grid;var found=FindUniformGrid(child);if(found is not null)return found;}return null;}
 void AdaptLayout()
 {
  var panel=FindUniformGrid(FeatureTiles);if(panel is null)return;
  panel.Columns=ActualWidth<620?1:3;
  var grid=HeroLayout;
  if(ActualWidth<760){grid.RowDefinitions.Clear();grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Grid.SetRow(AboutLinks,1);Grid.SetColumn(AboutLinks,1);Grid.SetColumnSpan(AboutLinks,2);AboutLinks.Margin=new Thickness(0,14,0,0);}
  else{grid.RowDefinitions.Clear();Grid.SetRow(AboutLinks,0);Grid.SetColumn(AboutLinks,2);Grid.SetColumnSpan(AboutLinks,1);AboutLinks.Margin=new Thickness(0);}
 }
 public void Dispose(){if(disposed)return;disposed=true;viewModel.Dispose();DataContext=null;}
}
