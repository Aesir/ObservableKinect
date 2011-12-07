using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Microsoft.Research.Kinect.Audio;
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;

namespace ObservableKinect.Speech
{
	/// <summary>
	/// Hosts observers for the various events that come from the Kinect's speech engine
	/// </summary>
	public class SpeechObserverHost
		: IDisposable
	{
		private const string RECOGNIZER_ID = "SR_MS_en-US_Kinect_10.0";

		private static readonly KinectAudioSource ourKinectSource;
		private static readonly SpeechRecognitionEngine ourSpeechEngine;
		private static int ourActiveHostsCounter = 0;
		private static readonly object ourCounterLock = new object();

		[ContractInvariantMethod]
		private void ObjectInvariant()
		{
			//Statics
			Contract.Invariant(ourActiveHostsCounter >= 0);
			Contract.Invariant(ourKinectSource != null);
			Contract.Invariant(ourSpeechEngine != null);
			Contract.Invariant(ourCounterLock != null);

			//Instance
			Contract.Invariant(myGrammars != null);
			Contract.Invariant(_SpeechHypothesized != null);
			Contract.Invariant(_SpeechRecognized != null);
			Contract.Invariant(_SpeechRecognitionRejected != null);
			Contract.Invariant(_SpeechDetected != null);
		}

		private readonly ISet<Grammar> myGrammars;

		#region IObservable Properties

		///<summary>
		///Recognized a word or words that may be a component of multiple complete phrases.
		///</summary>
		public IObservable<SpeechHypothesizedEventArgs> SpeechHypothesized
		{
			get
			{
				Contract.Ensures(Contract.Result<IObservable<SpeechHypothesizedEventArgs>>() != null);

				return _SpeechHypothesized;
			}
		}
		private readonly IObservable<SpeechHypothesizedEventArgs> _SpeechHypothesized;

		///<summary>
		///Receives input that matches any of its grammars.
		///</summary>
		public IObservable<SpeechRecognizedEventArgs> SpeechRecognized
		{
			get
			{
				Contract.Ensures(Contract.Result<IObservable<SpeechRecognizedEventArgs>>() != null);

				return _SpeechRecognized;
			}
		}
		private readonly IObservable<SpeechRecognizedEventArgs> _SpeechRecognized;

		///<summary>
		///Receives input that does not match any of its grammars.
		///</summary>
		public IObservable<SpeechRecognitionRejectedEventArgs> SpeechRecognitionRejected
		{
			get
			{
				Contract.Ensures(Contract.Result<IObservable<SpeechRecognitionRejectedEventArgs>>() != null);

				return _SpeechRecognitionRejected;
			}
		}
		private readonly IObservable<SpeechRecognitionRejectedEventArgs> _SpeechRecognitionRejected;

		///<summary>
		///Raised when speech can be identified.
		///</summary>
		public IObservable<SpeechDetectedEventArgs> SpeechDetected
		{
			get
			{
				Contract.Ensures(Contract.Result<IObservable<SpeechDetectedEventArgs>>() != null);

				return _SpeechDetected;
			}
		}
		private readonly IObservable<SpeechDetectedEventArgs> _SpeechDetected;

		#endregion IObservable Properties

		static SpeechObserverHost()
		{
			ourSpeechEngine = new SpeechRecognitionEngine(RECOGNIZER_ID);
			ourKinectSource = new KinectAudioSource
			{
				FeatureMode = true,
				AutomaticGainControl = true,
				SystemMode = SystemMode.OptibeamArrayOnly,
				MicArrayMode = MicArrayMode.MicArrayAdaptiveBeam
			};

			ourSpeechEngine.SetInputToAudioStream(ourKinectSource.Start(), new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SpeechObserverHost"/> class.
		/// </summary>
		/// <param name="keyWords">The keywords you want the speech engine to recognize.</param>
		public SpeechObserverHost(IEnumerable<string> keyWords)
			: this(keyWords.ToArray())
		{
			Contract.Requires(keyWords != null);
			Contract.Requires(keyWords.Any());
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SpeechObserverHost"/> class.
		/// </summary>
		/// <param name="keyWords">The keywords you want the speech engine to recognize.</param>
		public SpeechObserverHost(params string[] keyWords)
			: this(new Grammar(new Choices(keyWords.ToArray()).ToGrammarBuilder()))
		{
			Contract.Requires(keyWords != null);
			Contract.Requires(keyWords.Any());
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SpeechObserverHost"/> class.
		/// </summary>
		/// <param name="grammars">The grammars you want the speech engine to recognize.</param>
		public SpeechObserverHost(params Grammar[] grammars)
			: this((IEnumerable<Grammar>)grammars)
		{
			Contract.Requires(grammars != null);
			Contract.Requires(grammars.Any());
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SpeechObserverHost"/> class.
		/// </summary>
		/// <param name="grammars">The grammars you want the speech engine to recognize.</param>
		public SpeechObserverHost(IEnumerable<Grammar> grammars)
		{
			Contract.Requires(grammars != null);
			Contract.Requires(grammars.Any());

			myGrammars = new HashSet<Grammar>(grammars);
			foreach (var grammar in myGrammars)
			{
				ourSpeechEngine.LoadGrammarAsync(grammar);
			}

			lock (ourCounterLock)
			{
				if (ourActiveHostsCounter == 0)
				{
					ourSpeechEngine.RecognizeAsync(RecognizeMode.Multiple);
				}
				ourActiveHostsCounter++;
			}

			_SpeechHypothesized = EventFilterAndArgsSelector(Observable.FromEventPattern<SpeechHypothesizedEventArgs>(ourSpeechEngine, "SpeechHypothesized"));
			_SpeechRecognized = EventFilterAndArgsSelector(Observable.FromEventPattern<SpeechRecognizedEventArgs>(ourSpeechEngine, "SpeechRecognized"));
			_SpeechRecognitionRejected = EventFilterAndArgsSelector(Observable.FromEventPattern<SpeechRecognitionRejectedEventArgs>(ourSpeechEngine, "SpeechRecognitionRejected"));
			_SpeechDetected = Observable.FromEventPattern<SpeechDetectedEventArgs>(ourSpeechEngine, "SpeechDetected").Select(ep => ep.EventArgs);
		}

		private IObservable<T> EventFilterAndArgsSelector<T>(IObservable<EventPattern<T>> xs) where T : RecognitionEventArgs
		{
			Contract.Requires(xs != null);
			Contract.Ensures(Contract.Result<IObservable<T>>() != null);

			return xs.Where(ep => myGrammars.Contains(ep.EventArgs.Result.Grammar)).Select(ep => ep.EventArgs);
		}

		#region IDisposable

		private bool disposed = false;

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);

			GC.SuppressFinalize(this);
		}

		///<summary>
		/// Dispose(bool disposing) executes in two distinct scenarios.
		/// If disposing equals true, the method has been called directly
		/// or indirectly by a user's code. Managed and unmanaged resources
		/// can be disposed.
		/// If disposing equals false, the method has been called by the
		/// runtime from inside the finalizer and you should not reference
		/// other objects. Only unmanaged resources can be disposed.
		/// </summary>
		protected virtual void Dispose(bool disposing)
		{
			if (!this.disposed)
			{
				if (disposing)
				{
					lock (ourCounterLock)
					{
						ourActiveHostsCounter--;
						if (ourActiveHostsCounter == 0)
						{
							ourSpeechEngine.RecognizeAsyncStop();
						}
					}

					foreach (var grammar in myGrammars)
					{
						ourSpeechEngine.UnloadGrammar(grammar);
					}
					myGrammars.Clear();
				}
				disposed = true;
			}
		}

		/// <summary>
		/// Releases unmanaged resources and performs other cleanup operations before the
		/// <see cref="SpeechObserverHost"/> is reclaimed by garbage collection.
		/// </summary>
		~SpeechObserverHost()
		{
			Dispose(false);
		}

		#endregion IDisposable
	}
}