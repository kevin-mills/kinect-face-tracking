using System;

namespace KinectFaceTracking
{
    public abstract class DisposableBase : IDisposable
    {
        protected abstract void Dispose(bool disposing);

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
