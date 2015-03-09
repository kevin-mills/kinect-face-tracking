using Microsoft.Kinect.Face;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using WindowsPreview.Kinect;

namespace KinectFaceTracking
{

    public class KinectTrackingService : DisposableBase
    {

        #region Private Fields

        private KinectSensor _kinectSensor;
        private FrameworkElement _container;
        private ulong CurrentTrackingId { get; set; }
        private IObservable<BodyFrame> _bodyFrameObservable;
        private IObservable<Point> _hdFaceTrackingObservable;
        private Body[] _bodies = null;
        private Body _currentTrackedBody;
        private BodyFrameReader _bodyFrameReader = null;
        private HighDefinitionFaceFrameSource _highDefinitionFaceFrameSource = null;
        private HighDefinitionFaceFrameReader _highDefinitionFaceFrameReader = null;
        private FaceAlignment _currentFaceAlignment = null;
        private IDisposable _bodyFrameSubscription;
        private IDisposable _hdFaceTrackingSubscription;
        private double _containerWidth;
        private double _containerHeight;

        #endregion

        #region Constructor

        public KinectTrackingService(double containerWidth, double containerHeight)
        {
            _containerWidth = containerWidth;
            _containerHeight = containerHeight;

            _kinectSensor = KinectSensor.GetDefault();
            _kinectSensor.Open();
            
            CreateBodyFrameObservable();
            CreateHDFaceObservable();
            CreatePixelsObservable();

            _bodyFrameSubscription = _bodyFrameObservable.Subscribe();
            _hdFaceTrackingSubscription = _hdFaceTrackingObservable.Subscribe();
        }

        #endregion

        #region Public Properties

        public IObservable<Point> TrackingXYObservable_Pixels { get; private set; }

        #endregion

        #region Private Methods
        
        private void CreateBodyFrameObservable()
        {
            _bodyFrameReader = _kinectSensor.BodyFrameSource.OpenReader();

            _bodyFrameObservable = Observable.FromEventPattern<BodyFrameArrivedEventArgs>(_bodyFrameReader, "FrameArrived")
                .Select(f => f.EventArgs.FrameReference.AcquireFrame())
                .Where(bodyFrame => bodyFrame != null)
                .Select( bodyFrame =>
                {

                        if (bodyFrame != null)
                        {
                            if (_bodies == null)
                            {
                                // initialise to number of bodies supported by the Kinect
                                _bodies = new Body[bodyFrame.BodyFrameSource.BodyCount];
                            }
                            else
                            {
                                bodyFrame.GetAndRefreshBodyData(_bodies);
                            }
                            bodyFrame.Dispose();
                        }

                        if (_bodies != null)
                        {
                            _currentTrackedBody = FindBodyWithTrackingId(CurrentTrackingId, _bodies);
                            if (_currentTrackedBody == null)
                            {
                                SelectClosestBody();
                            }

                            // set the HighDefinitionFaceFrameSource tracking Id to the current person's tracking Id                   
                            if (this._highDefinitionFaceFrameSource != null)
                            {
                                this._highDefinitionFaceFrameSource.TrackingId = CurrentTrackingId;
                            }
                        }
                           
                        return bodyFrame;

                });
        }

        private void CreateHDFaceObservable()
        {
            _currentFaceAlignment = new FaceAlignment();

            _highDefinitionFaceFrameSource = new HighDefinitionFaceFrameSource(_kinectSensor);
            _highDefinitionFaceFrameReader = _highDefinitionFaceFrameSource.OpenReader();

            _hdFaceTrackingObservable = Observable.FromEventPattern<HighDefinitionFaceFrameArrivedEventArgs>(_highDefinitionFaceFrameReader, "FrameArrived")
               .Select(frame => frame.EventArgs.FrameReference.AcquireFrame())
               .Where(frame => frame != null)
               .Select(frame =>
               {
                   if (frame.IsFaceTracked)
                   {
                       // update our face alignment data 
                       frame.GetAndRefreshFaceAlignmentResult(_currentFaceAlignment);
                   }
                   frame.Dispose();
                   return _currentFaceAlignment;
               })
               .Select(faceAlignment => new Point
               {
                   X = faceAlignment.FaceOrientation.X,
                   Y = faceAlignment.FaceOrientation.Y,
               });
        }

        private void CreatePixelsObservable()
        {
            TrackingXYObservable_Pixels = _hdFaceTrackingObservable.Select(e =>
            {
                // set the limits for x and y axis
                double xLimit = .1d;
                double yLimit = .2d;

                // apply limits / normalise values
                double x = (e.X < xLimit ? e.X : xLimit);
                double y = (e.Y < yLimit ? e.Y : yLimit);

                x = x < -xLimit ? -xLimit : x;
                y = y < -yLimit ? -yLimit : y;

                x += xLimit;
                y += yLimit;

                // do the conversion
                var xMulti = (_containerHeight / 2) / (xLimit * 1000);
                var yMulti = (_containerWidth / 2) / (yLimit * 1000);

                x *= 1000 * xMulti;
                y *= 1000 * yMulti;
                
                // reverse values so that pixel 0,0 corresponds to kinect 0,0
                x = _containerHeight - x;
                y = _containerWidth - y;
                
                // placing x into y and y into x as these are screen as opposed to 3d coordinates now
                return new Point { X = Math.Truncate(y), Y = Math.Truncate(x) };

            })
              .Buffer(TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(10))
              .Select(listOfScreenPixels =>
              {
                  var result = new Point();
                  if (listOfScreenPixels.Any())
                  {
                      result.X = listOfScreenPixels.Average(p => p.X);
                      result.Y = listOfScreenPixels.Average(p => p.Y);
                  }
                  return result;
              });
        }


        #endregion

        #region Utility functions from SDK

        private void SelectClosestBody()
        {
            _currentTrackedBody = FindClosestBody(_bodies);
            CurrentTrackingId = _currentTrackedBody == null ? 0 : _currentTrackedBody.TrackingId;
        }

        private Body FindBodyWithTrackingId(ulong trackingId, Body[] bodies)
        {
            return bodies.SingleOrDefault(b => b != null && b.IsTracked == true && b.TrackingId == trackingId);
        }

        private Body FindClosestBody(Body[] bodies)
        {
            Body result = null;
            double closestBodyDistance = double.MaxValue;

            foreach (var body in bodies)
            {
                if (body != null && body.IsTracked)
                {
                    var joints = body.Joints;
                    var currentJoint = joints[JointType.SpineBase];

                    var currentLocation = currentJoint.Position;

                    var currentDistance = VectorLength(currentLocation);

                    if (result == null || currentDistance < closestBodyDistance)
                    {
                        result = body;
                        closestBodyDistance = currentDistance;
                    }
                }
            }

            return result;
        }

        private static double VectorLength(CameraSpacePoint point)
        {
            return Math.Sqrt(Math.Pow(point.X, 2) + Math.Pow(point.Y, 2) + Math.Pow(point.Z, 2));
        }
        #endregion

        #region DisposableBase Implementation

        protected override void Dispose(bool disposing)
        {
            if (_bodyFrameSubscription != null) { _bodyFrameSubscription.Dispose(); }
            if (_hdFaceTrackingSubscription != null) { _hdFaceTrackingSubscription.Dispose(); }
            if (_kinectSensor != null) { _kinectSensor.Close(); }
        }

        ~KinectTrackingService()
        {
            Dispose(false);
        }
        #endregion
    }

}
