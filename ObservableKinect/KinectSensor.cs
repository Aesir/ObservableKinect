using System;
using System.Diagnostics.Contracts;
using System.Reactive.Linq;
using Microsoft.Research.Kinect.Nui;

namespace ObservableKinect
{
	/// <summary>
	/// Provides an interface to a single Kinect sensor with IObservables for all of its events
	/// </summary>
	public class KinectSensor
	{
		#region Factory

		private static readonly KinectSensor[] ourKinectSensors = new KinectSensor[Runtime.Kinects.Count];

		/// <summary>
		/// Starts the Kinect with the specified options.
		/// </summary>
		/// <param name="options">The options to run the Kinect with as a set of flags.</param>
		/// <param name="kinectIndex">Index of the Kinect device you want to start, unless you have multiple Kinects attached you want to use the default value of 0.</param>
		/// <returns>A sensor instance which will allow you to operate on the Kinect that was just started.</returns>
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

		/// <summary>
		/// Initializes a new instance of the <see cref="KinectSensor"/> class and initializes the attached Kinect device.
		/// </summary>
		/// <param name="index">The index of the sensor to attach to.</param>
		/// <param name="options">The options to apply to the sensor to start with.</param>
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

		/// <summary>
		/// Gets the device if you need direct access. Bad things will probably happen if you try to manage it as well.
		/// </summary>
		public Runtime Device
		{
			get { return Runtime.Kinects[myIndex]; }
		}

		/// <summary>
		/// Gets an Observable that produces a value every time a depth frame is captured.
		/// </summary>
		public IObservable<ImageFrameReadyEventArgs> DepthFrames
		{
			get
			{
				return _DepthFrames;
			}
		}
		private readonly IObservable<ImageFrameReadyEventArgs> _DepthFrames;

		/// <summary>
		/// Gets a value indicating whether depth frames are being acquired.
		/// </summary>
		/// <value><c>true</c> if depth frames are being acquired; otherwise, <c>false</c>.</value>
		public bool DepthFramesRunning { get { return _DepthFramesRunning; } }
		private bool _DepthFramesRunning;

		/// <summary>
		/// Gets an Observable that produces a value every time a skeleton frame is captured.
		/// </summary>
		public IObservable<SkeletonFrameReadyEventArgs> SkeletonFrames
		{
			get
			{
				return _SkeletonFrames;
			}
		}
		private readonly IObservable<SkeletonFrameReadyEventArgs> _SkeletonFrames;

		/// <summary>
		/// Gets an Observable that produces a value every time a video frame is captured.
		/// </summary>
		public IObservable<ImageFrameReadyEventArgs> VideoFrames
		{
			get
			{
				return _VideoFrames;
			}
		}
		private readonly IObservable<ImageFrameReadyEventArgs> _VideoFrames;

		/// <summary>
		/// Gets a value indicating whether video frames are being acquired.
		/// </summary>
		/// <value><c>true</c> if video frames are being acquired; otherwise, <c>false</c>.</value>
		public bool VideoFramesRunning { get { return _VideoFramesRunning; } }
		private bool _VideoFramesRunning;

		/// <summary>
		/// Starts acquisition of depth frames.
		/// </summary>
		/// <param name="resolution">The resolution to use for the depth camera.</param>
		/// <param name="includePlayerIndex">If set to <c>true</c> it includes the player index (id of tracked skeleton) in the event arguments.</param>
		/// <param name="numBuffers">The number of image buffers to use. A larger number results in smoother playback but more latency as well. Default is 2.</param>
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

		/// <summary>
		/// Starts acquisition of video frames.
		/// </summary>
		/// <param name="resolution">The resolution to use for the video camera.</param>
		/// <param name="type">The type of image to acquire between RGB and YAV.</param>
		/// <param name="numBuffers">The number of image buffers to use. A larger number results in smoother playback but more latency as well. Default is 2.</param>
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