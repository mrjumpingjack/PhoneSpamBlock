using sipdotnet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using NAudio.Wave;
using System.Net;
using System.Text.RegularExpressions;
using PhoneSpamBlock;

namespace LinphoneBot
{
    public class Program
    {
        private static Mutex mutex = null;


        public static List<string> Blacklist = new List<string>();
        public static List<string> Whitelist = new List<string>();

        public static string APIURL;
        public static string User;
        public static string Password;
        public static string Server;


        public static string Querry;


        public static WaveOut waveOut = new WaveOut();
        public static Phone phone;

        public static Call AccCall;

        static DateTime CallStart;

        public static TCPServer TCPserver;

        public static void Main(string[] args)
        {
            string appName = AppDomain.CurrentDomain.FriendlyName;
            bool createdNew;


            mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                return;
            }

            TCPserver = new TCPServer();
            TCPserver.BlockCall += Server_BlockCall;
            TCPserver.Start();

            APIURL = args.Any(c => c.ToLower().StartsWith("-api")) ?
                     args[args.ToList().IndexOf(args.First(cmd => cmd.ToLower().StartsWith("-api"))) + 1] : "";

            User = args.Any(c => c.ToLower().StartsWith("-user")) ?
                     args[args.ToList().IndexOf(args.First(cmd => cmd.ToLower().StartsWith("-user"))) + 1] : "";

            Password = args.Any(c => c.ToLower().StartsWith("-pw")) ?
                     args[args.ToList().IndexOf(args.First(cmd => cmd.ToLower().StartsWith("-pw"))) + 1] : "";

            Server = args.Any(c => c.ToLower().StartsWith("-server")) ?
                    args[args.ToList().IndexOf(args.First(cmd => cmd.ToLower().StartsWith("-server"))) + 1] : "";

            Querry = args.Any(c => c.ToLower().StartsWith("-regex")) ?
                    args[args.ToList().IndexOf(args.First(cmd => cmd.ToLower().StartsWith("-regex"))) + 1] : "";



            Console.WriteLine("-api: " + APIURL);
            Console.WriteLine("-user: " + User);
            Console.WriteLine("-pw: " + Password);
            Console.WriteLine("-server: " + Server);
            Console.WriteLine("-regex: " + Querry);





            bool Setup = true;

            if (String.IsNullOrEmpty(APIURL) || String.IsNullOrEmpty(User) || String.IsNullOrEmpty(Password) || String.IsNullOrEmpty(Server) || String.IsNullOrEmpty(Querry))
            {
                Console.WriteLine("All config fields have to be set [-api, -user, -pw, -server, -regex]");
                Console.WriteLine("Canceling");
                Setup = false;
            }



            if (Setup)
            {
                Account account = new Account(User, Password, Server);
                phone = new Phone(account);

                Thread ll = new Thread(() => { LoadLists(); });
                ll.IsBackground = true;
                ll.Start();

                ////


                phone.PhoneConnectedEvent += delegate ()
                {
                    Console.WriteLine("Phone registered");
                };

                phone.CallActiveEvent += delegate (Call call)
                {
                    Console.WriteLine("Call activ");
                    AccCall = call;
                };

                phone.CallCompletedEvent += delegate (Call call)
                {
                    phone.TerminateCall(call);
                    var now = DateTime.Now;
                    Console.WriteLine("Call ended. Duration (in seconds): " + ((now - CallStart).TotalSeconds> 1000 || (now - CallStart).TotalSeconds < 1 ? new TimeSpan(0) : now - CallStart).TotalSeconds);

                    waveOut.Stop();
                };

                phone.ErrorEvent += Phone_ErrorEvent;

                phone.Connect();

                phone.IncomingCallEvent += delegate (Call call)
                {
                    HandleCall(call);
                };
            }
            else
                Console.ReadLine();
        }

        private static void HandleCall(Call call)
        {
            var number = call.GetFrom().ToLower().Split('@')[0].Replace("sip:", "");

            Console.WriteLine(number + " is calling.");

            if (CheckIfSpam(number))
            {
                Console.WriteLine("It's spam...");
                LogCall(number);
                CallStart = DateTime.Now;

                string name = DateTime.Now.ToString().Replace(".", "_").Replace(":", "-").Replace("/", "_") + "_" + number + ".wav";

                Console.WriteLine("Will be saved as: " + name);
                phone.ReceiveCallAndRecord(call, name);
                Thread.Sleep(500);
                PlaySounds(number);
            }
            else
            {
                Console.WriteLine("It's a real call!");

                Thread waitforUserBlock = new Thread(() =>
                {
                    CallBlock.Add(call, false);

                    TCPserver.Broadcast(call.GetFrom().Split(new char[]{ ':', '@' })[1]);

                    bool block = false;

                    var cs = call.GetState();

                    while (call.GetState() == Call.CallState.Loading && !block)
                    {
                        if (CallBlock.ContainsKey(call))
                            block = CallBlock[call];
                    }

                    if (call.GetState() == Call.CallState.Loading)
                    {
                        Console.WriteLine("User blocked call!");
                        LogCall(number);
                        CallStart = DateTime.Now;

                        string name = DateTime.Now.ToString().Replace(".", "_").Replace(":", "-").Replace("/", "_") + "_" + number + ".wav";

                        Console.WriteLine("Will be saved as: " + name);
                        phone.ReceiveCallAndRecord(call, name);
                        Thread.Sleep(500);
                        PlaySounds(number);

                        using (StreamReader sr = new StreamReader("blacklist.txt"))
                        {
                            if (sr.ReadToEnd().Contains(number))
                                using (StreamWriter sw = new StreamWriter("blacklist.txt", true))
                                {
                                    sw.WriteLine(number);
                                }
                        }
                    }

                    CallBlock.Remove(call);
                });
                waitforUserBlock.IsBackground = true;
                waitforUserBlock.Start();
            }
        }


        public static Dictionary<Call, bool> CallBlock = new Dictionary<Call, bool>();

        private static void Server_BlockCall(object sender, string e)
        {
            if (CallBlock.Keys.Any(Call => Call.GetFrom().Contains(e)))
            {
                CallBlock[CallBlock.Keys.First(Call => Call.GetFrom().Contains( e))] = true;
            }
        }

        private static void LoadLists()
        {
            Blacklist = new List<string>();
            Whitelist = new List<string>();

            while (true)
            {
                Console.WriteLine("Loading Lists...:");
                if (File.Exists("blacklist.txt"))
                {
                    using (StreamReader sr = new StreamReader("blacklist.txt"))
                    {
                        foreach (var item in sr.ReadToEnd().Split(new char[] { ',', ';', ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList())
                        {
                            if (!Blacklist.Contains(item))
                            {
                                Blacklist.Add(item);
                                Console.WriteLine(item);
                            }
                        }
                        
                    }
                }

                if (File.Exists("whitelist.txt"))
                {
                    using (StreamReader sr = new StreamReader("whitelist.txt"))
                    {
                        foreach (var item in sr.ReadToEnd().Split(new char[] { ',', ';', ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList())
                        {
                            if (!Whitelist.Contains(item))
                            {
                                Whitelist.Add(item);
                                Console.WriteLine(item);
                            }
                        }
                    }
                }

                Thread.Sleep(1000 * 60 * 10);
            }
        }



        public static void OutputDevice_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            phone.TerminateCall(AccCall);
        }

        public static void Phone_ErrorEvent(Call call, Phone.Error error)
        {

        }

        public static void LogCall(string phonenumber)
        {
            using (StreamWriter sw = new StreamWriter("Log.txt", true))
            {
                sw.WriteLine(DateTime.Now + ": " + phonenumber);
            }
        }

        public static bool CheckIfSpam(string phonenumber)
        {
            try
            {
                WebRequest webRequest = WebRequest.Create(APIURL + phonenumber);

                WebResponse webResponse = webRequest.GetResponse();
                using (StreamReader sr = new StreamReader(webResponse.GetResponseStream()))
                {
                    String sc = sr.ReadToEnd();

                    if (Whitelist.Contains(phonenumber) || Whitelist.Where(wl => wl.Contains('*')).Any(w => phonenumber.Contains(w.Trim('*'))))
                        return false;

                    if (Blacklist.Contains(phonenumber) || Blacklist.Where(bl => bl.Contains('*')).Any(b => phonenumber.Contains(b.Trim('*'))))
                        return true;

                    if (Regex.IsMatch(sc, Querry))
                        return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return false;
        }

        public static void PlaySounds(string number)
        {
            try
            {
                
                bool numberdirexists = false;

                string audiofilepath = "";

                foreach (string dir in Directory.GetDirectories(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds")))
                {
                    if (number.Contains(dir))
                    {
                        var files = Directory.GetFiles(dir);

                        audiofilepath = files[new Random(DateTime.Now.Second).Next(files.Length - 1)];
                        numberdirexists = true;
                        break;
                    }
                }


                if (!numberdirexists || audiofilepath == "")
                    audiofilepath = GetRandomSoundSenario();


                Console.WriteLine("Playing:" + audiofilepath);

                WaveFileReader waveReader = new NAudio.Wave.WaveFileReader(audiofilepath);
                waveOut = new WaveOut();
                waveOut.PlaybackStopped += OutputDevice_PlaybackStopped;
                waveOut.Init(waveReader);
                waveOut.Play();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static string GetRandomSoundSenario()
        {
            Random random = new Random(DateTime.Now.Second);
            var files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "Sounds", "*.wav", SearchOption.AllDirectories);
            var f = files[random.Next(0, files.Length - 1)];
            return f;
        }

    }
}