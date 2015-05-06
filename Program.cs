using System;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace Netduino_Test_Application
{
   public class Program
   {
      const int NUM_MICROPHONES = 4;
      const int ROC_THRESHOLD = 700;

      const int TOP_LEFT = 0;
      const int TOP_RIGHT = 1;
      const int BOTTOM_LEFT = 2;
      const int BOTTOM_RIGHT = 3;

      public static int ButtonPushedCount = 0;
      private static EmailClient myEmailClient;
      private static NetworkServer myNetworkServer;

      private static MicrophoneInput[] Microphones = new MicrophoneInput[NUM_MICROPHONES];

      private static bool lookingForPeak = false;

      public class MicrophoneInput
      {
         const int SAMPLES_PER_READING = 5;
         const int WAVE_MIDPOINT = 2048;

         public int CurrentReading = 0;
         public int LastReading = 0;

         private int[] MicSamples = new int[SAMPLES_PER_READING];

         private int sampleIndex = 0;
         private AnalogInput inputChannel = null;

         public MicrophoneInput(Cpu.AnalogChannel analogPin)
         {
            inputChannel = new AnalogInput(analogPin, 12);
         }

         public int RateOfChange
         {
            get { return (CurrentReading - LastReading); }
         }

         public int ReadSample()
         {
            //We don't care about the range of the value so for an average we can
            //simply take the sum of the last five values

            //Record last reading
            LastReading = CurrentReading;

            //Read and store new value
            MicSamples[sampleIndex] = System.Math.Abs(inputChannel.ReadRaw() - WAVE_MIDPOINT);

            //Add in the last value
            CurrentReading += MicSamples[sampleIndex];

            //Subtract the oldest value
            CurrentReading -= MicSamples[(sampleIndex + 1) % SAMPLES_PER_READING];

            //Reset the index for the wraparound
            if (++sampleIndex >= SAMPLES_PER_READING)
               sampleIndex = 0;

            return CurrentReading;
         }
      }


      public static void Main()
      {
         // write your code here
         OutputPort led = new OutputPort(Pins.ONBOARD_LED, false);

         InterruptPort pushButton = new InterruptPort(Pins.ONBOARD_BTN, false, Port.ResistorMode.Disabled, Port.InterruptMode.InterruptEdgeHigh);
         pushButton.OnInterrupt += new NativeEventHandler(pushButton_OnInterrupt);

         Microphones[TOP_LEFT] = new MicrophoneInput(AnalogChannels.ANALOG_PIN_A3);
         Microphones[TOP_RIGHT] = new MicrophoneInput(AnalogChannels.ANALOG_PIN_A2);
         Microphones[BOTTOM_LEFT] = new MicrophoneInput(AnalogChannels.ANALOG_PIN_A1);
         Microphones[BOTTOM_RIGHT] = new MicrophoneInput(AnalogChannels.ANALOG_PIN_A0);

         //Timer tmrInterrupt = new Timer(new TimerCallback(tmrInterrupt_Expired), mikeInput, 20, 50);

         //myNetworkServer = new NetworkServer();
         //myNetworkServer.ErrorOccurred += new NetworkServer.ErrorHandler(myNetworkServer_ErrorOccurred);
         //myNetworkServer.Start();

         //myEmailClient = new EmailClient();

         while (true)
         {
            //led.Write(true);
            //Thread.Sleep(250);
            //led.Write(false);
            //Thread.Sleep(250);
            MonitorMicrophones();

            Thread.Sleep(10);
         }
      }

      static void myNetworkServer_ErrorOccurred(string errorMessage)
      {
         //myEmailClient.SendEmail("mail.tech-logic.com", "apage@tech-logic.com", "netduino@tech-logic.com", "Netduino Server Exception", errorMessage);
      }

      static void pushButton_OnInterrupt(uint data1, uint data2, DateTime time)
      {
         if (data2 != 0)
         {
            ButtonPushedCount++;
            string msg = "Button Pushed " + Program.ButtonPushedCount + " Time(s)";

            //myEmailClient.SendEmail("mail.tech-logic.com", "apage@tech-logic.com", "netduino@tech-logic.com", "Netduino Status", msg);
         }
      }

      static string BuildWebServerMessage()
      {
         //Put the temp samples into the message
         string msg = "";
         for (int i = 0; i < NUM_MICROPHONES; i++)
            msg += Microphones[i].CurrentReading + ", ";

         return msg;
      }

      static void MonitorMicrophones()
      {
         int largestValue = 0;
         int micIndex = 0;

         //Go through all the microphones, perform a read and keep track of the largest sample
         for (int i = 0; i < NUM_MICROPHONES; i++)
         {
            Microphones[i].ReadSample();
            if (Microphones[i].CurrentReading > largestValue)
            {
               largestValue = Microphones[i].CurrentReading;
               micIndex = i;
            }
         }

         //We only care about the rate of change for the microphone with the largest signal
         //If the rate of change exceeds the trigger point, then set a latch that we are 
         //waiting for the rate of change to go negative so that we know the previous value
         //was the peak.
         if (!lookingForPeak)
         {
            if (Microphones[micIndex].RateOfChange > ROC_THRESHOLD)
            {
               lookingForPeak = true;
            }
         }
         else
         {
            if (Microphones[micIndex].RateOfChange < 0)
            {
               lookingForPeak = false;

               //We found the peak so do our calculation using the last value of each
               //microphone and report the position
               int xyHome = Microphones[TOP_LEFT].LastReading;
               int x2 = Microphones[TOP_RIGHT].LastReading;
               int y2 = Microphones[BOTTOM_LEFT].LastReading;
               int xyFar = Microphones[BOTTOM_RIGHT].LastReading;

               //Take the ratio of xyHome to x2 for the X Position
               float xRatio = (float)xyHome / x2;

               //Take the ratio of xyHome to y2 for the Y Position
               float yRatio = (float)xyHome / y2;

               //Build the position message and email it out
               string msg = "TopLeft:" + xyHome + ", TopRight:" + x2 + ", BottomLeft:" + y2 + ", X Ratio: " + xRatio + ", Y Ratio: " + yRatio;

               //myEmailClient.SendEmail("mail.tech-logic.com", "apage@tech-logic.com", "netduino@tech-logic.com", "Netduino Status", msg);
               //myNetworkServer.SendMessage(msg);
               Debug.Print(msg);
            }
         }
      }

      static void tmrInterrupt_Expired(object state)
      {
         try
         {
            MonitorMicrophones();
         }
         catch (Exception ex)
         {
            //myEmailClient.SendEmail("mail.tech-logic.com", "apage@tech-logic.com", "netduino@tech-logic.com", "Netduino Exception", ex.ToString());
         }
      }
   }
}
