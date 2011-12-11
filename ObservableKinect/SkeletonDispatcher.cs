using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Research.Kinect.Nui;

namespace ObservableKinect
{
	/// <summary>
	/// Takes an IObservable over a collection of SkeletonData and parses it to show new
	/// skeletons and provide an IObservable for each skeleton tracked
	/// </summary>
	internal class SkeletonDispatcher
	{
		private readonly Dictionary<int, ISubject<SkeletonData>> myLivingingSkeletons = new Dictionary<int, ISubject<SkeletonData>>();
		private readonly IDisposable mySkeletonSubscription;

		[ContractInvariantMethod]
		private void ObjectInvariant()
		{
			Contract.Invariant(myLivingingSkeletons != null);
			Contract.Invariant(mySkeletonSubscription != null);
			Contract.Invariant(_NewSkeletons != null);
		}

		public SkeletonDispatcher(IObservable<IEnumerable<SkeletonData>> skeletons)
		{
			Contract.Requires(skeletons != null);

			mySkeletonSubscription =
				skeletons
				.Select(sds => sds.Where(sd => sd.TrackingState != SkeletonTrackingState.NotTracked))
				.Subscribe(OnSkeletonsProduced);
		}

		private void OnSkeletonsProduced(IEnumerable<SkeletonData> skeletons)
		{
			Contract.Requires(skeletons != null);

			var newSkeltons = new List<IObservable<SkeletonData>>();
			var deadSkeletonIds = myLivingingSkeletons.Keys.ToList();

			//Publish for living skeletons and create new living skeletons
			foreach (var skeleton in skeletons)
			{
				ISubject<SkeletonData> skeletonSubject;
				if (myLivingingSkeletons.TryGetValue(skeleton.TrackingID, out skeletonSubject))
				{
					deadSkeletonIds.Remove(skeleton.TrackingID);
				}
				else
				{
					skeletonSubject = new Subject<SkeletonData>();
					myLivingingSkeletons.Add(skeleton.TrackingID, skeletonSubject);

					newSkeltons.Add(skeletonSubject);
				}
				skeletonSubject.OnNext(skeleton);
			}

			//Send OnCompleted for any Skeletons in myLivingSkeletons that didn't get updated
			foreach (var deadId in deadSkeletonIds)
			{
				var newlyDeadSkeleton = myLivingingSkeletons[deadId];
				newlyDeadSkeleton.OnCompleted();
				myLivingingSkeletons.Remove(deadId);
			}

			//Push out any new skeletons so people can subscribe to them
			if (newSkeltons.Any())
			{
				_NewSkeletons.OnNext(newSkeltons);
			}
		}

		public IObservable<IEnumerable<IObservable<SkeletonData>>> NewSkeletons
		{
			get
			{
				Contract.Ensures(Contract.Result<IObservable<IEnumerable<IObservable<SkeletonData>>>>() != null);

				return _NewSkeletons;
			}
		}
		private readonly ISubject<IEnumerable<IObservable<SkeletonData>>> _NewSkeletons = new Subject<IEnumerable<IObservable<SkeletonData>>>();
	}
}