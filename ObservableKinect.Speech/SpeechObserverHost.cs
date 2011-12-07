using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Microsoft.Research.Kinect.Audio;
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;

namespace ObservableKinect.Speech
{
	public class SpeechObserverHost
		: IDisposable
	{
		private const string RECOGNIZER_ID = "SR_MS_en-US_Kinect_10.0";

		private static readonly KinectAudioSource ourKinectSource;
		private static readonly SpeechRecognitionEngine ourSpeechEngine;
		private static int ourActiveHostsCounter = 0;
		private static readonly object ourCounterLock = new object();

		private readonly ISet<Grammar> myGrammars;

		#region IObservable Properties

		///<summary>
		///Recognized a word or words that may be a component of multiple complete phrases.
		///</summary>
		public IObservable<SpeechHypothesizedEventArgs> SpeechHypothesized
		{
			get
			{
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

		public SpeechObserverHost(IEnumerable<string> keyWords)
			: this(keyWords.ToArray())
		{
		}

		public SpeechObserverHost(params string[] keyWords)
			: this(new Grammar(new Choices(keyWords.ToArray()).ToGrammarBuilder()))
		{
		}

		public SpeechObserverHost(params Grammar[] grammars)
			: this((IEnumerable<Grammar>)grammars)
		{ }

		public SpeechObserverHost(IEnumerable<Grammar> grammars)
		{
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
			return xs.Where(ep => myGrammars.Contains(ep.EventArgs.Result.Grammar)).Select(ep => ep.EventArgs);
		}

		#region IDisposable

		private bool disposed = false;

		public void Dispose()
		{
			Dispose(true);

			GC.SuppressFinalize(this);
		}

		// Dispose(bool disposing) executes in two distinct scenarios.
		// If disposing equals true, the method has been called directly
		// or indirectly by a user's code. Managed and unmanaged resources
		// can be disposed.
		// If disposing equals false, the method has been called by the
		// runtime from inside the finalizer and you should not reference
		// other objects. Only unmanaged resources can be disposed.
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

		~SpeechObserverHost()
		{
			Dispose(false);
		}

		#endregion IDisposable
	}
}