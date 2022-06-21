using libcamenmCore;
using Monitor.Utils;
using NAudio.Wave;
using OpenCvSharp;
using Prism.Mvvm;
using Reactive.Bindings;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Monitor.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        public ReactiveCollection<Resolution> Eizous { get; } = new ReactiveCollection<Resolution>();

        public ReactiveCommand<Resolution> SetEizouCommand { get; }
        public ReactiveCommand<WaveInCapabilities> SetWaveInCommand { get; }
        public ReactiveCommand<WaveOutCapabilities> SetWaveOutCommand { get; }

        public ReactiveCommand SetThreadExecutionStateCommand { get; }

        public ReactivePropertySlim<bool> IsDisabledScreenSaver { get; } = new ReactivePropertySlim<bool>();

        public ReactivePropertySlim<Resolution> Eizou { get; } = new ReactivePropertySlim<Resolution>();

        public ReactivePropertySlim<Mat> Image { get; } = new ReactivePropertySlim<Mat>();

        public ReactiveCollection<WaveInCapabilities> WaveInCapabilities { get; } = new ReactiveCollection<WaveInCapabilities>();

        public ReactiveCollection<WaveOutCapabilities> WaveOutCapabilities { get; } = new ReactiveCollection<WaveOutCapabilities>();

        public ReactivePropertySlim<WaveInCapabilities> WaveInCapability { get; } = new ReactivePropertySlim<WaveInCapabilities>();
        public ReactivePropertySlim<WaveOutCapabilities> WaveOutCapability { get; } = new ReactivePropertySlim<WaveOutCapabilities>();
 
        public ReactivePropertySlim<double> CPURate { get; } = new ReactivePropertySlim<double>();

        private DispatcherTimer dispatcherTimer = new DispatcherTimer();
 
        private DispatcherTimer dispatcherTimer2 = new DispatcherTimer();

        private VideoCapture vc;

        private BufferedWaveProvider bufferedWaveProvider;

        private int waveInDeviceNumber;
        private int waveOutDeviceNumber;

        private WaveIn waveIn = new WaveIn();

        private WaveOut waveOut = new WaveOut();
 
        private PerformanceCounter CPUUsage = new PerformanceCounter();


        public MainWindowViewModel()
        {
            try
            {
                var enumDeviceList = DeviceEnumerator.EnumVideoInputDevice();
                int cameraNumber = 0;
                foreach (var device in enumDeviceList)
                {
                    Eizous.AddRange(DeviceEnumerator.GetAllAvailableResolution(cameraNumber++, device));
                }
            }
            catch (COMException)
            {
                throw;
            }
            SetEizouCommand = new ReactiveCommand<Resolution>().WithSubscribe((resolution) =>
            {
                if (dispatcherTimer is not null)
                {
                    dispatcherTimer.Stop();
                    dispatcherTimer.Tick -= DispatcherTimer_Tick;
                }
                if (vc is not null)
                {
                    vc.Dispose();
                }
                Eizou.Value = resolution;
                dispatcherTimer.Interval = TimeSpan.FromMilliseconds(16.7);
                dispatcherTimer.Tick += DispatcherTimer_Tick;
                Environment.SetEnvironmentVariable("OPENCV_VIDEOIO_MSMF_ENABLE_HW_TRANSFORMS", "0");
                vc = new VideoCapture(Eizou.Value.CameraNumber, VideoCaptureAPIs.MSMF);
                vc.Set(OpenCvSharp.VideoCaptureProperties.FourCC, OpenCvSharp.VideoWriter.FourCC("HEVC"));
                vc.Set(OpenCvSharp.VideoCaptureProperties.Fps, 60);
                vc.Set(VideoCaptureProperties.FrameWidth, Eizou.Value.Width);
                vc.Set(VideoCaptureProperties.FrameHeight, Eizou.Value.Height);
                dispatcherTimer.Start();
            });
            SetWaveInCommand = new ReactiveCommand<WaveInCapabilities>().WithSubscribe((waveInCaps) =>
            {
                WaveInCapability.Value = waveInCaps;
                int waveInDevices = WaveIn.DeviceCount;
                for (int waveInDevice = 0; waveInDevice < waveInDevices; waveInDevice++)
                {
                    WaveInCapabilities deviceInfo = WaveIn.GetCapabilities(waveInDevice);
                    if (deviceInfo.Equals(waveInCaps))
                    {
                        waveIn.StopRecording();
                        waveOut.Stop();
                        waveIn.DataAvailable -= WaveIn_DataAvailable;

                        waveIn = new WaveIn();

                        waveIn.DeviceNumber = waveInDeviceNumber = waveInDevice;

                        waveIn.DataAvailable += WaveIn_DataAvailable;
                        waveIn.WaveFormat = new WaveFormat(48000, 2);
                        bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat);
                        bufferedWaveProvider.DiscardOnBufferOverflow = true;
                        waveOut.Init(bufferedWaveProvider);
                        waveIn.StartRecording();
                        waveOut.Play();
                    }
                }
            });
            SetWaveOutCommand = new ReactiveCommand<WaveOutCapabilities>().WithSubscribe((waveOutCaps) =>
            {
                WaveOutCapability.Value = waveOutCaps;
                int waveOutDevices = WaveOut.DeviceCount;
                for (int waveOutDevice = 0; waveOutDevice < waveOutDevices; waveOutDevice++)
                {
                    WaveOutCapabilities deviceInfo = WaveOut.GetCapabilities(waveOutDevice);
                    if (deviceInfo.Equals(waveOutCaps))
                    {
                        waveIn.StopRecording();
                        waveOut.Stop();
                        waveIn.DataAvailable -= WaveIn_DataAvailable;

                        waveOut = new WaveOut();

                        waveOut.DeviceNumber = waveOutDeviceNumber = waveOutDevice;

                        waveIn.DataAvailable += WaveIn_DataAvailable;
                        waveIn.WaveFormat = new WaveFormat(48000, 2);
                        bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat);
                        bufferedWaveProvider.DiscardOnBufferOverflow = true;
                        waveOut.Init(bufferedWaveProvider);
                        waveIn.StartRecording();
                        waveOut.Play();
                    }
                }
            });
            SetThreadExecutionStateCommand = new ReactiveCommand().WithSubscribe(() =>
            {
                IsDisabledScreenSaver.Value = !IsDisabledScreenSaver.Value;
                if (IsDisabledScreenSaver.Value)
                {
                    NativeMethods.SetThreadExecutionState(NativeMethods.EXECUTION_STATE.ES_DISPLAY_REQUIRED | NativeMethods.EXECUTION_STATE.ES_CONTINUOUS);
                }
                else
                {
                    NativeMethods.SetThreadExecutionState(NativeMethods.EXECUTION_STATE.ES_CONTINUOUS);
                }
            });

            int waveInDevices = WaveIn.DeviceCount;
            for (int waveInDevice = 0; waveInDevice < waveInDevices; waveInDevice++)
            {
                WaveInCapabilities deviceInfo = WaveIn.GetCapabilities(waveInDevice);
                WaveInCapabilities.Add(deviceInfo);
                if (waveInDevice == 0)
                {
                    WaveInCapability.Value = deviceInfo;
                }
            }

            int waveOutDevices = WaveOut.DeviceCount;
            for (int waveOutDevice = 0; waveOutDevice < waveOutDevices; waveOutDevice++)
            {
                WaveOutCapabilities deviceInfo = WaveOut.GetCapabilities(waveOutDevice);
                WaveOutCapabilities.Add(deviceInfo);
                if (waveOutDevice == 0)
                {
                    WaveOutCapability.Value = deviceInfo;
                }
            }

            waveIn.DataAvailable += WaveIn_DataAvailable;
            waveIn.WaveFormat = new WaveFormat(48000, 2);
            bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat);
            bufferedWaveProvider.DiscardOnBufferOverflow = true;
            waveOut.Init(bufferedWaveProvider);
            waveIn.StartRecording();
            waveOut.Play(); 
            
            CPUUsage.CategoryName = "Process";
            CPUUsage.CounterName = "% Processor Time";
            CPUUsage.InstanceName = Process.GetCurrentProcess().ProcessName;
            CPUUsage.ReadOnly = true;
            
            dispatcherTimer2.Interval = TimeSpan.FromSeconds(1);
            dispatcherTimer2.Tick += DispatcherTimer2_Tick;
            dispatcherTimer2.Start();
        }

        private void DispatcherTimer2_Tick(object? sender, EventArgs e)
        {
            CPURate.Value = CPUUsage.NextValue() / Environment.ProcessorCount;
        }

        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }

        private void DispatcherTimer_Tick(object? sender, EventArgs e)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var mat = new Mat();
            vc.Grab();
            vc.Retrieve(mat);
            Image.Value = mat;
            stopwatch.Stop();
            Cv2.WaitKey((int)(17 - stopwatch.ElapsedMilliseconds));
        }
    }
}
