using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Proximity;
using Windows.Networking.Sockets;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.UI.Core;

namespace YH.Bluetooth
{
    public class BluetoothLECode
    {




        //同步锁
        private Boolean asyncLock = false;
        /// <summary>
        /// 服务UUID
        /// </summary>
        public string ServiceGuid { get; set; }
        /// <summary>
        /// 写的UUID
        /// </summary>
        public string WriteCharacteristicGuid { get; set; }
        /// <summary>
        /// 通知的UUID
        /// </summary>
        public string NotifyCharacteristicGuid { get; set; }
        /// <summary>
        /// 蓝牙适配器
        /// </summary>
        private Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher deviceWatcher;
        //定义一个委托
        public delegate void eventRun(MsgType type, string str, byte[] data = null);
        //定义一个事件
        public event eventRun ValueChanged;
        /// <summary>
        /// 存储检测到的设备
        /// </summary>
        private List<BluetoothLEDevice> DeviceList = new List<BluetoothLEDevice>();
        /// <summary>
        /// 获取设备列表委托
        /// </summary>
        public delegate void DeviceWatcher_AddedChangedEvent(string Mac, BluetoothLEDevice DeviceInfo);
        /// <summary>
        /// 获取设备列表事件
        /// </summary>
        public event DeviceWatcher_AddedChangedEvent DeviceWatcher_AddedChanged;
        //存储检测的设备MAC。
        public string CurrentDeviceMAC { get; set; }
        //存储检测到的设备。
        public BluetoothLEDevice CurrentDevice { get; set; }
        //存储检测到的主服务。
        public GattDeviceService CurrentService { get; set; }
        //存储检测到的写特征对象。
        public GattCharacteristic CurrentWriteCharacteristic { get; set; }
        //存储检测到的通知特征对象。
        public GattCharacteristic CurrentNotifyCharacteristic { get; set; }
        private const int CHARACTERISTIC_INDEX = 0;
        //特性通知类型通知启用
        private const GattClientCharacteristicConfigurationDescriptorValue CHARACTERISTIC_NOTIFICATION_TYPE = GattClientCharacteristicConfigurationDescriptorValue.Notify;

        public BluetoothLECode(string serviceGuid, string writeCharacteristicGuid, string notifyCharacteristicGuid)
        {
            this.ServiceGuid = serviceGuid;
            this.WriteCharacteristicGuid = writeCharacteristicGuid;
            this.NotifyCharacteristicGuid = notifyCharacteristicGuid;
        }

        public void StartBleDeviceWatcher()
        {
            this.deviceWatcher = new Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher();
            this.deviceWatcher.ScanningMode = Windows.Devices.Bluetooth.Advertisement.BluetoothLEScanningMode.Active;
            this.deviceWatcher.SignalStrengthFilter.InRangeThresholdInDBm = -80;
            this.deviceWatcher.SignalStrengthFilter.OutOfRangeThresholdInDBm = -90;
            this.deviceWatcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(5000);
            this.deviceWatcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(2000);
            this.deviceWatcher.Received += DeviceWatcher_Received;

            deviceWatcher.Start();
            string msg = "自动发现设备中..";

            ValueChanged(MsgType.NotifyTxt, msg);
        }

        /// <summary>
        /// 搜索蓝牙列表
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void DeviceWatcher_Received(Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher sender, Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementReceivedEventArgs args)
        {
            BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress).Completed = async (asyncInfo, asyncStatus) =>
            {
                if (asyncStatus == AsyncStatus.Completed)
                {
                    if (asyncInfo.GetResults() != null)
                    {
                        BluetoothLEDevice currentDevice = asyncInfo.GetResults();

                        Boolean contain = false;
                        foreach (BluetoothLEDevice device in DeviceList)//过滤重复的设备
                        {
                            if (device.DeviceId == currentDevice.DeviceId)
                            {
                                contain = true;
                            }
                        }
                        if (!contain)
                        {
                            this.DeviceList.Add(currentDevice);
                            this.DeviceWatcher_AddedChanged(currentDevice.DeviceId, currentDevice);
                        }
                    }
                }
            };
        }

        /// <summary>
        /// 停止搜索蓝牙
        /// </summary>
        public void StopBleDeviceWatcher()
        {
            string msg = "自动发现设备停止";
            ValueChanged(MsgType.NotifyTxt, msg);
            this.deviceWatcher.Stop();
        }

        private void DeviceWatcher_Stopped(DeviceWatcher sender, object args)
        {
            string msg = "自动发现设备停止";
            ValueChanged(MsgType.NotifyTxt, msg);
        }

        /// <summary>
        /// 按MAC地址查找系统中配对设备
        /// </summary>
        /// <param name="MAC"></param>
        public async Task SelectDevice(string MAC)
        {
            CurrentDeviceMAC = MAC;
            CurrentDevice = null;
            DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelector()).Completed = async (asyncInfo, asyncStatus) =>
            {
                if (asyncStatus == AsyncStatus.Completed)
                {
                    DeviceInformationCollection deviceInformation = asyncInfo.GetResults();
                    foreach (DeviceInformation di in deviceInformation)
                    {
                        await Matching(di.Id);
                    }
                    if (CurrentDevice == null)
                    {
                        string msg = "没有发现设备";
                        ValueChanged(MsgType.NotifyTxt, msg);
                        StartBleDeviceWatcher();
                    }
                }
            };
        }

        /// <summary>
        /// 按MAC地址直接组装设备ID查找设备
        /// </summary>
        /// <param name="MAC"></param>
        /// <returns></returns>
        public async Task SelectDeviceFromIdAsync(string MAC)
        {
            CurrentDeviceMAC = MAC;
            CurrentDevice = null;
            BluetoothAdapter.GetDefaultAsync().Completed = async (asyncInfo, asyncStatus) =>
            {
                if (asyncStatus == AsyncStatus.Completed)
                {
                    BluetoothAdapter mBluetoothAdapter = asyncInfo.GetResults();
                    byte[] _Bytes1 = BitConverter.GetBytes(mBluetoothAdapter.BluetoothAddress);//ulong转换为byte数组
                    Array.Reverse(_Bytes1);
                    string macAddress = BitConverter.ToString(_Bytes1, 2, 6).Replace('-', ':').ToLower();
                    string Id = "BluetoothLE#BluetoothLE" + macAddress + "-" + MAC;
                    await Matching(MAC);
                }

            };
        }

        private async Task Matching(string Id)
        {

            try
            {
                BluetoothLEDevice.FromIdAsync(Id).Completed = async (asyncInfo, asyncStatus) =>
                {
                    if (asyncStatus == AsyncStatus.Completed)
                    {
                        BluetoothLEDevice bleDevice = asyncInfo.GetResults();
                        //在当前设备变量中保存检测到的设备。
                        CurrentDevice = bleDevice;
                        await Connect();

                    }
                };
            }
            catch (Exception e)
            {
                string msg = "没有发现设备" + e.ToString();
                ValueChanged(MsgType.NotifyTxt, msg);
                StartBleDeviceWatcher();
            }

        }

        private async Task Connect()
        {
            string msg = "正在连接设备<" + CurrentDeviceMAC + ">..";
            ValueChanged(MsgType.NotifyTxt, msg);
            CurrentDevice.ConnectionStatusChanged += CurrentDevice_ConnectionStatusChanged;
            await SelectDeviceService();

        }

        /// <summary>
        /// 主动断开连接
        /// </summary>
        /// <returns></returns>
        public void Dispose()
        {

            CurrentDeviceMAC = null;
            CurrentService?.Dispose();
            CurrentDevice?.Dispose();
            CurrentDevice = null;
            CurrentService = null;
            CurrentWriteCharacteristic = null;
            CurrentNotifyCharacteristic = null;
            ValueChanged(MsgType.NotifyTxt, "主动断开连接");

        }

        private void CurrentDevice_ConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected && CurrentDeviceMAC != null)
            {
                string msg = "设备已断开,自动重连";
                ValueChanged(MsgType.NotifyTxt, msg);
                if (!asyncLock)
                {
                    asyncLock = true;
                    CurrentDevice.Dispose();
                    CurrentDevice = null;
                    CurrentService = null;
                    CurrentWriteCharacteristic = null;
                    CurrentNotifyCharacteristic = null;
                    SelectDeviceFromIdAsync(CurrentDeviceMAC);
                }

            }
            else
            {
                string msg = "设备已连接";
                ValueChanged(MsgType.NotifyTxt, msg);
            }
        }

        /// <summary>
        /// 按GUID 查找主服务
        /// </summary>
        /// <param name="characteristic">GUID 字符串</param>
        /// <returns></returns>
        public async Task SelectDeviceService()
        {
            Guid guid = new Guid(ServiceGuid);
            CurrentDevice.GetGattServicesForUuidAsync(guid).Completed = (asyncInfo, asyncStatus) =>
            {
                if (asyncStatus == AsyncStatus.Completed)
                {
                    try
                    {
                        GattDeviceServicesResult result = asyncInfo.GetResults();
                        string msg = "主服务=" + CurrentDevice.ConnectionStatus;
                        ValueChanged(MsgType.NotifyTxt, msg);
                        if (result.Services.Count > 0)
                        {
                            CurrentService = result.Services[CHARACTERISTIC_INDEX];
                            if (CurrentService != null)
                            {
                                asyncLock = true;
                                GetCurrentWriteCharacteristic();
                                GetCurrentNotifyCharacteristic();

                            }
                        }
                        else
                        {
                            msg = "没有发现服务,自动重试中";
                            ValueChanged(MsgType.NotifyTxt, msg);
                            SelectDeviceService();
                        }
                    }
                    catch (Exception e)
                    {
                        ValueChanged(MsgType.NotifyTxt, "没有发现服务,自动重试中");
                        SelectDeviceService();

                    }
                }
            };
        }

        /// <summary>
        /// 设置写特征对象。
        /// </summary>
        /// <returns></returns>
        public async Task GetCurrentWriteCharacteristic()
        {

            string msg = "";
            Guid guid = new Guid(WriteCharacteristicGuid);
            CurrentService.GetCharacteristicsForUuidAsync(guid).Completed = async (asyncInfo, asyncStatus) =>
            {
                if (asyncStatus == AsyncStatus.Completed)
                {
                    GattCharacteristicsResult result = asyncInfo.GetResults();
                    msg = "特征对象=" + CurrentDevice.ConnectionStatus;
                    ValueChanged(MsgType.NotifyTxt, msg);
                    if (result.Characteristics.Count > 0)
                    {
                        CurrentWriteCharacteristic = result.Characteristics[CHARACTERISTIC_INDEX];
                    }
                    else
                    {
                        msg = "没有发现特征对象,自动重试中";
                        ValueChanged(MsgType.NotifyTxt, msg);
                        await GetCurrentWriteCharacteristic();
                    }
                }
            };
        }


        /// <summary>
        /// 发送数据接口
        /// </summary>
        /// <param name="characteristic"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task Write(byte[] data)
        {
            if (CurrentWriteCharacteristic != null)
            {
                CurrentWriteCharacteristic.WriteValueAsync(CryptographicBuffer.CreateFromByteArray(data), GattWriteOption.WriteWithResponse);
            }

        }

        /// <summary>
        /// 设置通知特征对象。
        /// </summary>
        /// <returns></returns>
        public async Task GetCurrentNotifyCharacteristic()
        {
            string msg = "";
            Guid guid = new Guid(NotifyCharacteristicGuid);
            CurrentService.GetCharacteristicsForUuidAsync(guid).Completed = async (asyncInfo, asyncStatus) =>
            {
                if (asyncStatus == AsyncStatus.Completed)
                {
                    GattCharacteristicsResult result = asyncInfo.GetResults();
                    msg = "特征对象=" + CurrentDevice.ConnectionStatus;
                    ValueChanged(MsgType.NotifyTxt, msg);
                    if (result.Characteristics.Count > 0)
                    {
                        CurrentNotifyCharacteristic = result.Characteristics[CHARACTERISTIC_INDEX];
                        CurrentNotifyCharacteristic.ProtectionLevel = GattProtectionLevel.Plain;
                        CurrentNotifyCharacteristic.ValueChanged += Characteristic_ValueChanged;
                        await EnableNotifications(CurrentNotifyCharacteristic);

                    }
                    else
                    {
                        msg = "没有发现特征对象,自动重试中";
                        ValueChanged(MsgType.NotifyTxt, msg);
                        await GetCurrentNotifyCharacteristic();
                    }
                }
            };
        }

        /// <summary>
        /// 设置特征对象为接收通知对象
        /// </summary>
        /// <param name="characteristic"></param>
        /// <returns></returns>
        public async Task EnableNotifications(GattCharacteristic characteristic)
        {
            string msg = "收通知对象=" + CurrentDevice.ConnectionStatus;
            ValueChanged(MsgType.NotifyTxt, msg);

            characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(CHARACTERISTIC_NOTIFICATION_TYPE).Completed = async (asyncInfo, asyncStatus) =>
            {
                if (asyncStatus == AsyncStatus.Completed)
                {
                    GattCommunicationStatus status = asyncInfo.GetResults();
                    if (status == GattCommunicationStatus.Unreachable)
                    {
                        msg = "设备不可用";
                        ValueChanged(MsgType.NotifyTxt, msg);
                        if (CurrentNotifyCharacteristic != null && !asyncLock)
                        {
                            await EnableNotifications(CurrentNotifyCharacteristic);
                        }
                    }
                    asyncLock = false;
                    msg = "设备连接状态" + status;
                    ValueChanged(MsgType.NotifyTxt, msg);
                }
            };
        }

        private void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            byte[] data;
            CryptographicBuffer.CopyToByteArray(args.CharacteristicValue, out data);
            string str = BitConverter.ToString(data);
            ValueChanged(MsgType.BLEData, str, data);

        }
    }

    public enum MsgType
    {
        NotifyTxt,
        BLEData
    }
}
