using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace KinectFaceTracking
{
    public class ViewModel : DisposableBase, INotifyPropertyChanged
    {

        #region Private Variables

        private double _x;
        private double _y;
        private string _text;
        private KinectTrackingService _kinectTrackingService = null;
        private IDisposable _trackingXYZSubscription;

        #endregion

        #region Public Properties

        public double X
        {
            get { return _x; }
            set { _x = value; OnPropertyChanged("X"); }
        }
               
        public double Y
        {
            get { return _y; }
            set { _y = value; OnPropertyChanged("Y"); }
        }
               
        public string Text
        {
            get { return _text; }
            set { _text = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Constructor

        public ViewModel(double width, double height)
        {
            _kinectTrackingService = new KinectTrackingService(width, height);

            _trackingXYZSubscription = _kinectTrackingService.TrackingXYObservable_Pixels.Subscribe(obj =>
            {
                X = obj.X -30;
                Y = obj.Y -30;
                Text = string.Format("{0},{1}", (int)X, (int)Y);
            });

        }

        #endregion

        #region Protected Methods

        protected void OnPropertyChanged([CallerMemberName]string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                App.RootFrame.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_trackingXYZSubscription != null) { _trackingXYZSubscription.Dispose(); }
            if (_kinectTrackingService != null) { _kinectTrackingService.Dispose(); }
        }

        #endregion
    }

}
