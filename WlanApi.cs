using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NativeWifi
{
    public class WlanClient
    {
        public class WlanInterface
        {
            private WlanClient client;
            private Wlan.WlanInterfaceInfo info;

            public delegate void WlanNotificationEventHandler(Wlan.WlanNotificationData notifyData);

            public delegate void WlanConnectionNotificationEventHandler(Wlan.WlanNotificationData notifyData, Wlan.WlanConnectionNotificationData connNotifyData);

            public delegate void WlanReasonNotificationEventHandler(Wlan.WlanNotificationData notifyData, Wlan.WlanReasonCode reasonCode);

            public event WlanNotificationEventHandler WlanNotification;

            public event WlanConnectionNotificationEventHandler WlanConnectionNotification;

            public event WlanReasonNotificationEventHandler WlanReasonNotification;

            private bool queueEvents;
            private AutoResetEvent eventQueueFilled = new AutoResetEvent(false);
            private Queue<object> eventQueue = new Queue<object>();

            private struct WlanConnectionNotificationEventData
            {
                public Wlan.WlanNotificationData notifyData;
                public Wlan.WlanConnectionNotificationData connNotifyData;
            }
            private struct WlanReasonNotificationData
            {
                public Wlan.WlanNotificationData notifyData;
                public Wlan.WlanReasonCode reasonCode;
            }

            internal WlanInterface(WlanClient client, Wlan.WlanInterfaceInfo info)
            {
                this.client = client;
                this.info = info;
            }
            private void SetInterfaceInt(Wlan.WlanIntfOpcode opCode, int value)
            {
                IntPtr valuePtr = Marshal.AllocHGlobal(sizeof(int));
                Marshal.WriteInt32(valuePtr, value);
                try
                {
                    Wlan.ThrowIfError(
                        Wlan.WlanSetInterface(client.clientHandle, info.interfaceGuid, opCode, sizeof(int), valuePtr, IntPtr.Zero));
                }
                finally
                {
                    Marshal.FreeHGlobal(valuePtr);
                }
            }

            private int GetInterfaceInt(Wlan.WlanIntfOpcode opCode)
            {
                IntPtr valuePtr;
                int valueSize;
                Wlan.WlanOpcodeValueType opcodeValueType;
                Wlan.ThrowIfError(
                    Wlan.WlanQueryInterface(client.clientHandle, info.interfaceGuid, opCode, IntPtr.Zero, out valueSize, out valuePtr, out opcodeValueType));
                try
                {
                    return Marshal.ReadInt32(valuePtr);
                }
                finally
                {
                    Wlan.WlanFreeMemory(valuePtr);
                }
            }

            public bool Autoconf
            {
                get
                {
                    return GetInterfaceInt(Wlan.WlanIntfOpcode.AutoconfEnabled) != 0;
                }
                set
                {
                    SetInterfaceInt(Wlan.WlanIntfOpcode.AutoconfEnabled, value ? 1 : 0);
                }
            }

            public Wlan.Dot11BssType BssType
            {
                get
                {
                    return (Wlan.Dot11BssType)GetInterfaceInt(Wlan.WlanIntfOpcode.BssType);
                }
                set
                {
                    SetInterfaceInt(Wlan.WlanIntfOpcode.BssType, (int)value);
                }
            }

            public Wlan.WlanInterfaceState InterfaceState
            {
                get
                {
                    return (Wlan.WlanInterfaceState)GetInterfaceInt(Wlan.WlanIntfOpcode.InterfaceState);
                }
            }

            public int Channel
            {
                get
                {
                    return GetInterfaceInt(Wlan.WlanIntfOpcode.ChannelNumber);
                }
            }

            public int RSSI
            {
                get
                {
                    return GetInterfaceInt(Wlan.WlanIntfOpcode.RSSI);
                }
            }

            public Wlan.Dot11OperationMode CurrentOperationMode
            {
                get
                {
                    return (Wlan.Dot11OperationMode)GetInterfaceInt(Wlan.WlanIntfOpcode.CurrentOperationMode);
                }
            }
            public Wlan.WlanConnectionAttributes CurrentConnection
            {
                get
                {
                    int valueSize;
                    IntPtr valuePtr;
                    Wlan.WlanOpcodeValueType opcodeValueType;
                    Wlan.ThrowIfError(
                        Wlan.WlanQueryInterface(client.clientHandle, info.interfaceGuid, Wlan.WlanIntfOpcode.CurrentConnection, IntPtr.Zero, out valueSize, out valuePtr, out opcodeValueType));
                    try
                    {
                        return (Wlan.WlanConnectionAttributes)Marshal.PtrToStructure(valuePtr, typeof(Wlan.WlanConnectionAttributes));
                    }
                    finally
                    {
                        Wlan.WlanFreeMemory(valuePtr);
                    }
                }
            }

            public void Scan()
            {
                Wlan.ThrowIfError(
                    Wlan.WlanScan(client.clientHandle, info.interfaceGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero));
            }
            private Wlan.WlanAvailableNetwork[] ConvertAvailableNetworkListPtr(IntPtr availNetListPtr)
            {
                Wlan.WlanAvailableNetworkListHeader availNetListHeader = (Wlan.WlanAvailableNetworkListHeader)Marshal.PtrToStructure(availNetListPtr, typeof(Wlan.WlanAvailableNetworkListHeader));
                long availNetListIt = availNetListPtr.ToInt64() + Marshal.SizeOf(typeof(Wlan.WlanAvailableNetworkListHeader));
                Wlan.WlanAvailableNetwork[] availNets = new Wlan.WlanAvailableNetwork[availNetListHeader.numberOfItems];
                for (int i = 0; i < availNetListHeader.numberOfItems; ++i)
                {
                    availNets[i] = (Wlan.WlanAvailableNetwork)Marshal.PtrToStructure(new IntPtr(availNetListIt), typeof(Wlan.WlanAvailableNetwork));
                    availNetListIt += Marshal.SizeOf(typeof(Wlan.WlanAvailableNetwork));
                }
                return availNets;
            }

            public Wlan.WlanAvailableNetwork[] GetAvailableNetworkList(Wlan.WlanGetAvailableNetworkFlags flags)
            {
                IntPtr availNetListPtr;
                Wlan.ThrowIfError(
                    Wlan.WlanGetAvailableNetworkList(client.clientHandle, info.interfaceGuid, flags, IntPtr.Zero, out availNetListPtr));
                try
                {
                    return ConvertAvailableNetworkListPtr(availNetListPtr);
                }
                finally
                {
                    Wlan.WlanFreeMemory(availNetListPtr);
                }
            }

            private Wlan.WlanBssEntry[] ConvertBssListPtr(IntPtr bssListPtr)
            {
                Wlan.WlanBssListHeader bssListHeader = (Wlan.WlanBssListHeader)Marshal.PtrToStructure(bssListPtr, typeof(Wlan.WlanBssListHeader));
                long bssListIt = bssListPtr.ToInt64() + Marshal.SizeOf(typeof(Wlan.WlanBssListHeader));
                Wlan.WlanBssEntry[] bssEntries = new Wlan.WlanBssEntry[bssListHeader.numberOfItems];
                for (int i = 0; i < bssListHeader.numberOfItems; ++i)
                {
                    bssEntries[i] = (Wlan.WlanBssEntry)Marshal.PtrToStructure(new IntPtr(bssListIt), typeof(Wlan.WlanBssEntry));
                    bssListIt += Marshal.SizeOf(typeof(Wlan.WlanBssEntry));
                }
                return bssEntries;
            }

            public Wlan.WlanBssEntry[] GetNetworkBssList()
            {
                IntPtr bssListPtr;
                Wlan.ThrowIfError(
                    Wlan.WlanGetNetworkBssList(client.clientHandle, info.interfaceGuid, IntPtr.Zero, Wlan.Dot11BssType.Any, false, IntPtr.Zero, out bssListPtr));
                try
                {
                    return ConvertBssListPtr(bssListPtr);
                }
                finally
                {
                    Wlan.WlanFreeMemory(bssListPtr);
                }
            }

            public Wlan.WlanBssEntry[] GetNetworkBssList(Wlan.Dot11Ssid ssid, Wlan.Dot11BssType bssType, bool securityEnabled)
            {
                IntPtr ssidPtr = Marshal.AllocHGlobal(Marshal.SizeOf(ssid));
                Marshal.StructureToPtr(ssid, ssidPtr, false);
                try
                {
                    IntPtr bssListPtr;
                    Wlan.ThrowIfError(
                        Wlan.WlanGetNetworkBssList(client.clientHandle, info.interfaceGuid, ssidPtr, bssType, securityEnabled, IntPtr.Zero, out bssListPtr));
                    try
                    {
                        return ConvertBssListPtr(bssListPtr);
                    }
                    finally
                    {
                        Wlan.WlanFreeMemory(bssListPtr);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(ssidPtr);
                }
            }

            protected void Connect(Wlan.WlanConnectionParameters connectionParams)
            {
                Wlan.ThrowIfError(
                    Wlan.WlanConnect(client.clientHandle, info.interfaceGuid, ref connectionParams, IntPtr.Zero));
            }
            public void Connect(Wlan.WlanConnectionMode connectionMode, Wlan.Dot11BssType bssType, string profile)
            {
                Wlan.WlanConnectionParameters connectionParams = new Wlan.WlanConnectionParameters();
                connectionParams.wlanConnectionMode = connectionMode;
                connectionParams.profile = profile;
                connectionParams.dot11BssType = bssType;
                connectionParams.flags = 0;
                Connect(connectionParams);
            }

            public bool ConnectSynchronously(Wlan.WlanConnectionMode connectionMode, Wlan.Dot11BssType bssType, string profile, int connectTimeout)
            {
                queueEvents = true;
                try
                {
                    Connect(connectionMode, bssType, profile);
                    while (queueEvents && eventQueueFilled.WaitOne(connectTimeout, true))
                    {
                        lock (eventQueue)
                        {
                            while (eventQueue.Count != 0)
                            {
                                object e = eventQueue.Dequeue();
                                if (e is WlanConnectionNotificationEventData)
                                {
                                    WlanConnectionNotificationEventData wlanConnectionData = (WlanConnectionNotificationEventData)e;
                                    // Check 
                                    if (wlanConnectionData.notifyData.notificationSource == Wlan.WlanNotificationSource.ACM)
                                    {
                                        switch ((Wlan.WlanNotificationCodeAcm)wlanConnectionData.notifyData.notificationCode)
                                        {
                                            case Wlan.WlanNotificationCodeAcm.ConnectionComplete:
                                                if (wlanConnectionData.connNotifyData.profileName == profile)
                                                    return true;
                                                break;
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                finally
                {
                    queueEvents = false;
                    eventQueue.Clear();
                }
                return false; // timeout "connection complete"
            }

            public void Connect(Wlan.WlanConnectionMode connectionMode, Wlan.Dot11BssType bssType, Wlan.Dot11Ssid ssid, Wlan.WlanConnectionFlags flags)
            {
                Wlan.WlanConnectionParameters connectionParams = new Wlan.WlanConnectionParameters();
                connectionParams.wlanConnectionMode = connectionMode;
                connectionParams.dot11SsidPtr = Marshal.AllocHGlobal(Marshal.SizeOf(ssid));
                Marshal.StructureToPtr(ssid, connectionParams.dot11SsidPtr, false);
                connectionParams.dot11BssType = bssType;
                connectionParams.flags = flags;
                Connect(connectionParams);
                Marshal.DestroyStructure(connectionParams.dot11SsidPtr, ssid.GetType());
                Marshal.FreeHGlobal(connectionParams.dot11SsidPtr);
            }

            public void DeleteProfile(string profileName)
            {
                Wlan.ThrowIfError(
                    Wlan.WlanDeleteProfile(client.clientHandle, info.interfaceGuid, profileName, IntPtr.Zero));
            }

            public Wlan.WlanReasonCode SetProfile(Wlan.WlanProfileFlags flags, string profileXml, bool overwrite)
            {
                Wlan.WlanReasonCode reasonCode;
                Wlan.ThrowIfError(
                        Wlan.WlanSetProfile(client.clientHandle, info.interfaceGuid, flags, profileXml, null, overwrite, IntPtr.Zero, out reasonCode));
                return reasonCode;
            }

            public string GetProfileXml(string profileName)
            {
                IntPtr profileXmlPtr;
                Wlan.WlanProfileFlags flags;
                Wlan.WlanAccess access;
                Wlan.ThrowIfError(
                    Wlan.WlanGetProfile(client.clientHandle, info.interfaceGuid, profileName, IntPtr.Zero, out profileXmlPtr, out flags,
                                   out access));
                try
                {
                    return Marshal.PtrToStringUni(profileXmlPtr);
                }
                finally
                {
                    Wlan.WlanFreeMemory(profileXmlPtr);
                }
            }

            public Wlan.WlanProfileInfo[] GetProfiles()
            {
                IntPtr profileListPtr;
                Wlan.ThrowIfError(
                    Wlan.WlanGetProfileList(client.clientHandle, info.interfaceGuid, IntPtr.Zero, out profileListPtr));
                try
                {
                    Wlan.WlanProfileInfoListHeader header = (Wlan.WlanProfileInfoListHeader)Marshal.PtrToStructure(profileListPtr, typeof(Wlan.WlanProfileInfoListHeader));
                    Wlan.WlanProfileInfo[] profileInfos = new Wlan.WlanProfileInfo[header.numberOfItems];
                    long profileListIterator = profileListPtr.ToInt64() + Marshal.SizeOf(header);
                    for (int i = 0; i < header.numberOfItems; ++i)
                    {
                        Wlan.WlanProfileInfo profileInfo = (Wlan.WlanProfileInfo)Marshal.PtrToStructure(new IntPtr(profileListIterator), typeof(Wlan.WlanProfileInfo));
                        profileInfos[i] = profileInfo;
                        profileListIterator += Marshal.SizeOf(profileInfo);
                    }
                    return profileInfos;
                }
                finally
                {
                    Wlan.WlanFreeMemory(profileListPtr);
                }
            }

            internal void OnWlanConnection(Wlan.WlanNotificationData notifyData, Wlan.WlanConnectionNotificationData connNotifyData)
            {
                if (WlanConnectionNotification != null)
                    WlanConnectionNotification(notifyData, connNotifyData);

                if (queueEvents)
                {
                    WlanConnectionNotificationEventData queuedEvent = new WlanConnectionNotificationEventData();
                    queuedEvent.notifyData = notifyData;
                    queuedEvent.connNotifyData = connNotifyData;
                    EnqueueEvent(queuedEvent);
                }
            }

            internal void OnWlanReason(Wlan.WlanNotificationData notifyData, Wlan.WlanReasonCode reasonCode)
            {
                if (WlanReasonNotification != null)
                    WlanReasonNotification(notifyData, reasonCode);
                if (queueEvents)
                {
                    WlanReasonNotificationData queuedEvent = new WlanReasonNotificationData();
                    queuedEvent.notifyData = notifyData;
                    queuedEvent.reasonCode = reasonCode;
                    EnqueueEvent(queuedEvent);
                }
            }

            internal void OnWlanNotification(Wlan.WlanNotificationData notifyData)
            {
                if (WlanNotification != null)
                    WlanNotification(notifyData);
            }

            private void EnqueueEvent(object queuedEvent)
            {
                lock (eventQueue)
                    eventQueue.Enqueue(queuedEvent);
                eventQueueFilled.Set();
            }

            public NetworkInterface NetworkInterface
            {
                get
                {
                    foreach (NetworkInterface netIface in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        Guid netIfaceGuid = new Guid(netIface.Id);
                        if (netIfaceGuid.Equals(info.interfaceGuid))
                        {
                            return netIface;
                        }
                    }
                    return null;
                }
            }

            public Guid InterfaceGuid
            {
                get { return info.interfaceGuid; }
            }

            public string InterfaceDescription
            {
                get { return info.interfaceDescription; }
            }

            public string InterfaceName
            {
                get { return NetworkInterface.Name; }
            }
        }

        private IntPtr clientHandle;
        private uint negotiatedVersion;
        private Wlan.WlanNotificationCallbackDelegate wlanNotificationCallback;
        private Dictionary<Guid, WlanInterface> ifaces = new Dictionary<Guid, WlanInterface>();

        public WlanClient()
        {
            Wlan.ThrowIfError(
                Wlan.WlanOpenHandle(Wlan.WLAN_CLIENT_VERSION_XP_SP2, IntPtr.Zero, out negotiatedVersion, out clientHandle));
            try
            {
                Wlan.WlanNotificationSource prevSrc;
                wlanNotificationCallback = new Wlan.WlanNotificationCallbackDelegate(OnWlanNotification);
                Wlan.ThrowIfError(
                    Wlan.WlanRegisterNotification(clientHandle, Wlan.WlanNotificationSource.All, false, wlanNotificationCallback, IntPtr.Zero, IntPtr.Zero, out prevSrc));
            }
            catch
            {
                Wlan.WlanCloseHandle(clientHandle, IntPtr.Zero);
                throw;
            }
        }
        ~WlanClient()
        {
            Wlan.WlanCloseHandle(clientHandle, IntPtr.Zero);
        }
        private Wlan.WlanConnectionNotificationData? ParseWlanConnectionNotification(ref Wlan.WlanNotificationData notifyData)
        {
            int expectedSize = Marshal.SizeOf(typeof(Wlan.WlanConnectionNotificationData));
            if (notifyData.dataSize < expectedSize)
                return null;

            Wlan.WlanConnectionNotificationData connNotifyData =
                (Wlan.WlanConnectionNotificationData)
                Marshal.PtrToStructure(notifyData.dataPtr, typeof(Wlan.WlanConnectionNotificationData));
            if (connNotifyData.wlanReasonCode == Wlan.WlanReasonCode.Success)
            {
                IntPtr profileXmlPtr = new IntPtr(
                    notifyData.dataPtr.ToInt64() +
                    Marshal.OffsetOf(typeof(Wlan.WlanConnectionNotificationData), "profileXml").ToInt64());
                connNotifyData.profileXml = Marshal.PtrToStringUni(profileXmlPtr);
            }
            return connNotifyData;
        }
        private void OnWlanNotification(ref Wlan.WlanNotificationData notifyData, IntPtr context)
        {
            WlanInterface wlanIface = ifaces.ContainsKey(notifyData.interfaceGuid) ? ifaces[notifyData.interfaceGuid] : null;

            switch (notifyData.notificationSource)
            {
                case Wlan.WlanNotificationSource.ACM:
                    switch ((Wlan.WlanNotificationCodeAcm)notifyData.notificationCode)
                    {
                        case Wlan.WlanNotificationCodeAcm.ConnectionStart:
                        case Wlan.WlanNotificationCodeAcm.ConnectionComplete:
                        case Wlan.WlanNotificationCodeAcm.ConnectionAttemptFail:
                        case Wlan.WlanNotificationCodeAcm.Disconnecting:
                        case Wlan.WlanNotificationCodeAcm.Disconnected:
                            Wlan.WlanConnectionNotificationData? connNotifyData = ParseWlanConnectionNotification(ref notifyData);
                            if (connNotifyData.HasValue)
                                if (wlanIface != null)
                                    wlanIface.OnWlanConnection(notifyData, connNotifyData.Value);
                            break;
                        case Wlan.WlanNotificationCodeAcm.ScanFail:
                            {
                                try
                                {
                                    int expectedSize = Marshal.SizeOf(typeof(Wlan.WlanReasonCode));
                                    if (notifyData.dataSize >= expectedSize)
                                    {
                                        Wlan.WlanReasonCode reasonCode = (Wlan.WlanReasonCode)Marshal.ReadInt32(notifyData.dataPtr);
                                        if (wlanIface != null)
                                            wlanIface.OnWlanReason(notifyData, reasonCode);
                                    }
                                }
                                catch { }
                            }
                            break;
                    }
                    break;
                case Wlan.WlanNotificationSource.MSM:
                    switch ((Wlan.WlanNotificationCodeMsm)notifyData.notificationCode)
                    {
                        case Wlan.WlanNotificationCodeMsm.Associating:
                        case Wlan.WlanNotificationCodeMsm.Associated:
                        case Wlan.WlanNotificationCodeMsm.Authenticating:
                        case Wlan.WlanNotificationCodeMsm.Connected:
                        case Wlan.WlanNotificationCodeMsm.RoamingStart:
                        case Wlan.WlanNotificationCodeMsm.RoamingEnd:
                        case Wlan.WlanNotificationCodeMsm.Disassociating:
                        case Wlan.WlanNotificationCodeMsm.Disconnected:
                        case Wlan.WlanNotificationCodeMsm.PeerJoin:
                        case Wlan.WlanNotificationCodeMsm.PeerLeave:
                        case Wlan.WlanNotificationCodeMsm.AdapterRemoval:
                            Wlan.WlanConnectionNotificationData? connNotifyData = ParseWlanConnectionNotification(ref notifyData);
                            if (connNotifyData.HasValue)
                                if (wlanIface != null)
                                    wlanIface.OnWlanConnection(notifyData, connNotifyData.Value);
                            break;
                    }
                    break;
            }

            if (wlanIface != null)
                wlanIface.OnWlanNotification(notifyData);
        }
        public WlanInterface[] Interfaces
        {
            get
            {
                IntPtr ifaceList;
                Wlan.ThrowIfError(
                    Wlan.WlanEnumInterfaces(clientHandle, IntPtr.Zero, out ifaceList));
                try
                {
                    Wlan.WlanInterfaceInfoListHeader header =
                        (Wlan.WlanInterfaceInfoListHeader)Marshal.PtrToStructure(ifaceList, typeof(Wlan.WlanInterfaceInfoListHeader));
                    Int64 listIterator = ifaceList.ToInt64() + Marshal.SizeOf(header);
                    WlanInterface[] interfaces = new WlanInterface[header.numberOfItems];
                    List<Guid> currentIfaceGuids = new List<Guid>();
                    for (int i = 0; i < header.numberOfItems; ++i)
                    {
                        Wlan.WlanInterfaceInfo info =
                            (Wlan.WlanInterfaceInfo)Marshal.PtrToStructure(new IntPtr(listIterator), typeof(Wlan.WlanInterfaceInfo));
                        listIterator += Marshal.SizeOf(info);
                        WlanInterface wlanIface;
                        currentIfaceGuids.Add(info.interfaceGuid);
                        if (ifaces.ContainsKey(info.interfaceGuid))
                            wlanIface = ifaces[info.interfaceGuid];
                        else
                            wlanIface = new WlanInterface(this, info);
                        interfaces[i] = wlanIface;
                        ifaces[info.interfaceGuid] = wlanIface;
                    }

                    Queue<Guid> deadIfacesGuids = new Queue<Guid>();
                    foreach (Guid ifaceGuid in ifaces.Keys)
                    {
                        if (!currentIfaceGuids.Contains(ifaceGuid))
                            deadIfacesGuids.Enqueue(ifaceGuid);
                    }
                    while (deadIfacesGuids.Count != 0)
                    {
                        Guid deadIfaceGuid = deadIfacesGuids.Dequeue();
                        ifaces.Remove(deadIfaceGuid);
                    }

                    return interfaces;
                }
                finally
                {
                    Wlan.WlanFreeMemory(ifaceList);
                }
            }
        }
        public string GetStringForReasonCode(Wlan.WlanReasonCode reasonCode)
        {
            StringBuilder sb = new StringBuilder(1024); // the 1024 size 
            Wlan.ThrowIfError(
                Wlan.WlanReasonCodeToString(reasonCode, sb.Capacity, sb, IntPtr.Zero));
            return sb.ToString();
        }
    }
}