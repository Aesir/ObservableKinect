using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reactive.Linq;
using Microsoft.Research.Kinect.Nui;

namespace ObservableKinect
{
	/// <summary>
	/// A helper that provides convenient IObservables for skeleton tracking
	/// </summary>
	public class SkeletonTracking
	{
		private readonly KinectSensor mySensor;
		private readonly SkeletonDispatcher mySkeletonTracker;

		[ContractInvariantMethod]
		private void ObjectInvariant()
		{
			Contract.Invariant(mySensor != null);
			Contract.Invariant(mySkeletonTracker != null);
			Contract.Invariant(_SkeletonPresent != null);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SkeletonTracking"/> class using the default Kinect sensor.
		/// </summary>
		public SkeletonTracking()
			: this(KinectSensor.Start(RuntimeOptions.UseSkeletalTracking))
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SkeletonTracking"/> class using the Kinect sensor at the specified index.
		/// </summary>
		/// <param name="sensorIndex">Index of the Kinect sensor to use.</param>
		public SkeletonTracking(int sensorIndex)
			: this(KinectSensor.Start(RuntimeOptions.UseSkeletalTracking, sensorIndex))
		{
			Contract.Requires(sensorIndex >= 0 && sensorIndex < Runtime.Kinects.Count);
		}

		private SkeletonTracking(KinectSensor sensor)
		{
			Contract.Requires(sensor != null);

			mySensor = sensor;
			var skeletonFrames =
				mySensor
				.SkeletonFrames
				.Select(sf => sf.SkeletonFrame);

			//skeletonFrames
			//    .Sample(TimeSpan.FromSeconds(5.0))
			//    .Subscribe(PrintSkeletonFrames);

			_SkeletonPresent = skeletonFrames
				.Select(sf => sf.Skeletons.Any(skel => skel.TrackingState != SkeletonTrackingState.NotTracked))
				.DistinctUntilChanged();

			mySkeletonTracker = new SkeletonDispatcher(skeletonFrames.Select(sf => sf.Skeletons));
		}

		/// <summary>
		/// Gets an Observable that says whether at least one skeleton is present and being tracked by the Kinect sensor
		/// </summary>
		public IObservable<bool> SkeletonPresent
		{
			get
			{
				Contract.Ensures(Contract.Result<IObservable<bool>>() != null);

				return _SkeletonPresent;
			}
		}
		private readonly IObservable<bool> _SkeletonPresent;

		/// <summary>
		/// Gets an observable that pushes a new value whenever there is/are new skeleton(s) being tracked. The values
		/// consist of an observable sequence for each new skeleton, allowing you to subscribe and track the skeleton.
		/// </summary>
		public IObservable<IEnumerable<IObservable<SkeletonData>>> FreshSkeletons
		{
			get
			{
				Contract.Ensures(Contract.Result<IObservable<IEnumerable<IObservable<SkeletonData>>>>() != null);

				return mySkeletonTracker.NewSkeletons;
			}
		}

		private static void PrintSkeletonFrames(SkeletonFrame frame)
		{
			Console.WriteLine("Frame Number: {0}", frame.FrameNumber);
			Console.WriteLine("Quality: {0}", frame.Quality);
			Console.WriteLine("TimeStamp: {0}", frame.TimeStamp);
			foreach (var skeleton in frame.Skeletons.Where(skel => skel.TrackingState == SkeletonTrackingState.Tracked))
			{
				Console.WriteLine();
				Console.WriteLine("\tUserIndex: {0}", skeleton.UserIndex);
				Console.WriteLine("\tTracking Id: {0}", skeleton.TrackingID);
				Console.WriteLine("\tTracking State: {0}", skeleton.TrackingState);
				Console.WriteLine("\tQuality: {0}", skeleton.Quality);
				Console.WriteLine("\tPosition: [W={0}, X={1}, Y={2}, Z={3}]", skeleton.Position.W, skeleton.Position.X, skeleton.Position.Y, skeleton.Position.Z);
			}
			Console.WriteLine();
			Console.WriteLine("===============================================");
			Console.WriteLine();
		}
	}
}