Observable Kinect is an Rx wrapper and facade for the Kinect for Windows SDK which must be installed in order for it to work. 

Initially Observable Kinect is being designed to offer the same functionality as the SDK with IObservable<EventArgs> rather than the standard Kinect SDK events. The end goal is to not only have the same events but also make it easier to get started with the Kinect and perform common tasks.

Pull requests will be gladly accepted.

## Examples
Saving a picture from the Kinect's RGB camera

```csharp
KinectSensor sensor = KinectSensor.Start(RuntimeOptions.UseColor);	//Get the sensor and tell it you want to use the color camera
sensor.StartVideoFrames(ImageResolution.Resolution1280x1024);       //Tell it to turn the camera on and return images at 1280x1024

//Beware of actually running this code, you'll generate a lot of bitmaps in a very short time unless you throttle the observable
sensor
	.VideoFrames                          //Listen to the VideoFrames observable
	.Select(ifrea => ifrea.ImageFrame)    //Take the ImageFrame from the event args
	.Subscribe(this.SaveFrame);           //Every time an image is captured save it
	
//Just converts the image byte array returned by the Kinect into a bitmap file
void SaveFrame(ImageFrame frame)
{
	var filePath = frame.Timestamp.ToString() + ".bmp";

	using (var bitmap = new Bitmap(frame.Image.Width, frame.Image.Height, PixelFormat.Format32bppRgb))
	{
		var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);

		var data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);
		Marshal.Copy(frame.Image.Bits, 0, data.Scan0, frame.Image.Bits.Length);
		bitmap.UnlockBits(data);

		using (var stream = new FileStream(filePath, FileMode.Create))
			bitmap.Save(stream, ImageFormat.Bmp);
	}
}
```

Responding to speech - note you'll need the [Microsoft Speech SDK](https://www.microsoft.com/download/en/details.aspx?id=14373) in addition to the [Kinect SDK](http://kinectforwindows.org/).

```csharp
SpeechObserverHost speechHost = new SpeechObserverHost("red", "green", "yellow");  //The strings are a convenient shortcut but you can also use a full Grammar
speechHost
	.SpeechRecognized
	.Where(e => e.Result != null && e.Result.Confidence > 0.9)  //We really only want the results where it's pretty confident or we'll get a lot of false positives
	.Subscribe(OnSpeechRecognized);
	
void OnSpeechRecognized(SpeechRecognizedEventArgs e)
{
	Console.WriteLine("SpeechRecognized: \"{0}\" with an confidence of {1}", e.Result.Text, e.Result.Confidence);
}
```