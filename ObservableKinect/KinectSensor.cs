using System;
using System.Diagnostics.Contracts;
using System.Reactive.Linq;
using Microsoft.Research.Kinect.Nui;

namespace ObservableKinect
{
	public class KinectSensor
	{
		#region Factory

		private static readonly KinectSensor[] ourKinectSensors = new KinectSensor[Runtime.Kinects.Count];

		public static KinectSensor Start(RuntimeOptions options, int kinectIndex = 0)
		{
			Contract.Requires(kinectIndex > 0 && kinectIndex < Runtime.Kinects.Count);

			lock (ourKinectSensors)
			{
				if (ourKinectSensors[kinectIndex] != null)
					AddRuntimeOptions(ourKinectSensors[kinectIndex], options);
				else
					ourKinectSensors[kinectIndex] = new KinectSensor(kinectIndex, options);

				return ourKinectSensors[kinectIndex];
			}
		}

		private static void AddRuntimeOptions(KinectSensor sensor, RuntimeOptions options)
		{
			sensor.myOptions |= options;

			sensor.Device.Uninitialize();
			sensor.Device.Initialize(sensor.myOptions);
		}

		#endregion Factory

		private readonly int myIndex;
		private RuntimeOptions myOptions;

		protected KinectSensor(int index, RuntimeOptions options)
		{
			Contract.Requires(index > 0 && index < Runtime.Kinects.Count);

			myIndex = index;
			myOptions = options;

			this.Device.Initialize(options);

			_DepthFrames = Observable.FromEventPattern<ImageFrameReadyEventArgs>(this.Device, "DepthFrameReady").Select(ep => ep.EventArgs);
			_SkeletonFrames = Observable.FromEventPattern<SkeletonFrameReadyEventArgs>(this.Device, "SkeletonFrameReady").Select(ep => ep.EventArgs);
			_VideoFrames = Observable.FromEventPattern<ImageFrameReadyEventArgs>(this.Device, "VideoFrameReady").Select(ep => ep.EventArgs);
		}

		public Runtime Device
		{
			get { return Runtime.Kinects[myIndex]; }
		}

		public IObservable<ImageFrameReadyEventArgs> DepthFrames
		{
			get
			{
				return _DepthFrames;
			}
		}
		private readonly IObservable<ImageFrameReadyEventArgs> _DepthFrames;

		public bool DepthFramesRunning { get { return _DepthFramesRunning; } }
		private bool _DepthFramesRunning;

		public IObservable<SkeletonFrameReadyEventArgs> SkeletonFrames
		{
			get
			{
				return _SkeletonFrames;
			}
		}
		private readonly IObservable<SkeletonFrameReadyEventArgs> _SkeletonFrames;

		public bool SkeletonFramesRunning { get { return _SkeletonFramesRunning; } }
		private bool _SkeletonFramesRunning;

		public IObservable<ImageFrameReadyEventArgs> VideoFrames
		{
			get
			{
				return _VideoFrames;
			}
		}
		private readonly IObservable<ImageFrameReadyEventArgs> _VideoFrames;

		public bool VideoFramesRunning { get { return _VideoFramesRunning; } }
		private bool _VideoFramesRunning;

		public void StartDepthFrames(ImageResolution resolution, bool includePlayerIndex = false, int numBuffers = 2)
		{
			Contract.Requires(!this.DepthFramesRunning);
			Contract.Requires(numBuffers > 0);

			if (!this.DepthFramesRunning)
			{
				var type = includePlayerIndex ? ImageType.DepthAndPlayerIndex : ImageType.Depth;
				this.Device.DepthStream.Open(ImageStreamType.Depth, numBuffers, resolution, type);
				_DepthFramesRunning = true;
			}
		}

		public void StartVideoFrames(ImageResolution resolution, ImageType type = ImageType.Color, int numBuffers = 2)
		{
			Contract.Requires(!this.VideoFramesRunning);
			Contract.Requires(type == ImageType.Color || type == ImageType.ColorYuv || type == ImageType.ColorYuvRaw);
			Contract.Requires(numBuffers > 0);

			if (!this.VideoFramesRunning)
			{
				this.Device.VideoStream.Open(ImageStreamType.Video, numBuffers, resolution, type);
				_VideoFramesRunning = true;
			}
		}
	}
}