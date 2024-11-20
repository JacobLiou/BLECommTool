using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YH.Bluetooth
{
    public partial class Form2 : Form
    {
        List<BleDeviceInfo> DeviceInfos = new List<BleDeviceInfo>();
        BluetoothLECode bluetooth = null;

        public Form2()
        {
            InitializeComponent();

            CheckForIllegalCrossThreadCalls = false;
            bluetooth = new BluetoothLECode("0000fff0-0000-1000-8000-00805f9b34fb", "0000fff1-0000-1000-8000-00805f9b34fb", "0000fff1-0000-1000-8000-00805f9b34fb");
            bluetooth.ValueChanged += Bluetooth_ValueChanged;
            bluetooth.DeviceWatcher_AddedChanged += Bluetooth_DeviceWatcher_AddedChanged;
            this.Init();
        }

        private void Bluetooth_DeviceWatcher_AddedChanged(string Mac, Windows.Devices.Bluetooth.BluetoothLEDevice DeviceInfo)
        {
            BleDeviceInfo dev = new BleDeviceInfo() { Mac = DeviceInfo.DeviceId, DeviceName = DeviceInfo.Name, DeviceInfo = DeviceInfo };
            this.DeviceInfos.Add(dev);
            this.listboxBleDevice.DisplayMember = "DeviceName";
            this.listboxBleDevice.ValueMember = "Mac";
            this.listboxBleDevice.Items.Add(dev);
        }

        private void Bluetooth_ValueChanged(MsgType type, string str, byte[] data = null)
        {
            this.listboxMessage.Items.Add(str);
        }

        private void Init()
        {
            //this.btnConnect.Enabled = false;
            //this.btnReader.Enabled = false;
            //this.btnSend.Enabled = false;
        }

        public static void RunAsync(Action action)
        {
            ((Action)(delegate ()
            {
                action.Invoke();
            })).BeginInvoke(null, null);
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            if (this.btnSearch.Text == "搜索")
            {
                this.listboxMessage.Items.Clear();
                this.listboxBleDevice.Items.Clear();
                this.DeviceInfos = new List<BleDeviceInfo>();
                this.bluetooth.StartBleDeviceWatcher();
                this.btnSearch.Text = "停止";
            }
            else
            {
                this.bluetooth.StopBleDeviceWatcher();
                this.btnSearch.Text = "搜索";
            }
        }

        /// <summary>
        /// 搜索蓝牙设备列表
        /// </summary>
        private void BleCore_DeviceWatcherChanged(Windows.Devices.Bluetooth.BluetoothLEDevice Device)
        {
            RunAsync(() =>
            {
                this.listboxBleDevice.Items.Add(Device.Name);
                this.btnConnect.Enabled = true;
            });
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (this.listboxBleDevice.SelectedItem != null)
            {
                BleDeviceInfo dev = (BleDeviceInfo)this.listboxBleDevice.SelectedItem;
                this.bluetooth.SelectDeviceFromIdAsync(dev.Mac);
            }
            else
            {
                MessageBox.Show("请选择连接的蓝牙.");
            }
        }
    }

    public class BleDeviceInfo
    {
        public string Mac { get; set; }
        public string DeviceName { get; set; }
        public Windows.Devices.Bluetooth.BluetoothLEDevice DeviceInfo { get; set; }
    }
}
