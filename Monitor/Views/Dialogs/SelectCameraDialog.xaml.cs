

using DirectShowLib;
using libcamenmCore;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Monitor.View.Dialogs
{
    /// <summary>
    /// Interaction logic for SelectCameraDialog.xaml
    /// </summary>
    public partial class SelectCameraDialog : Window
    {
        public SelectCameraDialog()
        {
            InitializeComponent();

            try
            {
                _enumDeviceList = DeviceEnumerator.EnumVideoInputDevice();
                int cameraNumber = 0;
                _enumResolutionList = new List<Resolution>();
                foreach (var device in _enumDeviceList)
                {
                    var resolutions = DeviceEnumerator.GetAllAvailableResolution(cameraNumber++, device);
                    _enumResolutionList.AddRange(resolutions);
                }
            }
            catch (COMException)
            {
                throw;
            }

            this.DataContext = _enumResolutionList;
        }

        private void Button_OK_Click(object sender, RoutedEventArgs e)
        {
            if (ListBox_CameraDevices.SelectedIndex == -1)
            {
                MessageBox.Show("�J�����ԍ���I�����Ă��������B");
                return;
            }
            _selectedCameraNumber = (ListBox_CameraDevices.SelectedItem as Resolution).CameraNumber;
            _selectedCameraDeviceName = (ListBox_CameraDevices.SelectedItem as Resolution).DsDevice.Name;
            _selectedResolution = ListBox_CameraDevices.SelectedItem as Resolution;
            DialogResult = true;
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ListBoxItem_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ListBoxItem item = sender as ListBoxItem;
            if (item != null)
            {
                _selectedCameraNumber = (ListBox_CameraDevices.SelectedItem as Resolution).CameraNumber;
                _selectedCameraDeviceName = (ListBox_CameraDevices.SelectedItem as Resolution).DsDevice.Name;
                _selectedResolution = ListBox_CameraDevices.SelectedItem as Resolution;
                var listBoxItemContent = item.Content as Resolution;
                if (!listBoxItemContent.DsDevice.Name.Equals(_selectedCameraDeviceName))
                {
                    throw new Exception("�J�����ԍ����f�o�C�X���ɑΉ����Ă��܂���B");
                }
                DialogResult = true;
            }
        }

        private IEnumerable<DsDevice> _enumDeviceList;

        private List<Resolution> _enumResolutionList;

        private int _selectedCameraNumber;
        public int SelectedCameraNumber { get { return _selectedCameraNumber; } }

        private string _selectedCameraDeviceName;
        public string SelectedCameraDeviceName { get { return _selectedCameraDeviceName; } }

        private Resolution _selectedResolution;
        public Resolution SelectedResolution { get { return _selectedResolution; } }
    }
}
