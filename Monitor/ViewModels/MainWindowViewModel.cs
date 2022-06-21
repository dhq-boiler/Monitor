using libcamenmCore;
using Monitor.Utils;
using NAudio.Wave;
using OpenCvSharp;
using Prism.Mvvm;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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

        private DispatcherTimer dispatcherTimer = new DispatcherTimer();

        private VideoCapture vc;

        private BufferedWaveProvider bufferedWaveProvider;

        private int waveInDeviceNumber;
        private int waveOutDeviceNumber;

        private WaveIn waveIn = new WaveIn();

        private WaveOut waveOut = new WaveOut();


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
                Eizou.Value = resolution;
                dispatcherTimer.Interval = TimeSpan.FromTicks(1);
                dispatcherTimer.Tick += DispatcherTimer_Tick;
                vc = new VideoCapture(Eizou.Value.CameraNumber);
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
                    NativeMethods.SetThreadExecutionState(NativeMethods.EXECUTION_STATE.ES_AWAYMODE_REQUIRED | NativeMethods.EXECUTION_STATE.ES_CONTINUOUS);
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
            }

            int waveOutDevices = WaveOut.DeviceCount;
            for (int waveOutDevice = 0; waveOutDevice < waveOutDevices; waveOutDevice++)
            {
                WaveOutCapabilities deviceInfo = WaveOut.GetCapabilities(waveOutDevice);
                WaveOutCapabilities.Add(deviceInfo);
            }

            waveIn.DataAvailable += WaveIn_DataAvailable;
            waveIn.WaveFormat = new WaveFormat(48000, 2);
            bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat);
            bufferedWaveProvider.DiscardOnBufferOverflow = true;
            waveOut.Init(bufferedWaveProvider);
            waveIn.StartRecording();
            waveOut.Play();
        }

        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }

        private void DispatcherTimer_Tick(object? sender, EventArgs e)
        {
            if (Image.Value is not null)
            {
                var disposing = Image.Value;
                Task.Factory.StartNew(() =>
                {
                    disposing.Dispose();
                });
            }
            Image.Value = vc.RetrieveMat();
        }
    }
}
