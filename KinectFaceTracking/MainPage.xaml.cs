using System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace KinectFaceTracking
{

    public sealed partial class MainPage : Page
    {
        private ViewModel _viewModel;

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) =>
                {
                    _viewModel = new ViewModel(myCanvas.ActualWidth, myCanvas.ActualHeight);
                    this.DataContext = _viewModel;
                };
            this.Unloaded += (s, e) => _viewModel.Dispose(); 
        }
                
    }

}
