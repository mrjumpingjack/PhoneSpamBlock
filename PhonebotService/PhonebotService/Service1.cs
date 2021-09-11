using NAudio.Wave;
using sipdotnet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PhonebotService
{
    public partial class Service1 : ServiceBase
    {
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



        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
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



            //Console.WriteLine("-api: " + APIURL);
            //Console.WriteLine("-user: " + User);
            //Console.WriteLine("-pw: " + Password);
            //Console.WriteLine("-server: " + Server);
            ////Console.WriteLine("-regex: " + Querry);





            bool Setup = true;

            if (String.IsNullOrEmpty(APIURL) || String.IsNullOrEmpty(User) || String.IsNullOrEmpty(Password) || String.IsNullOrEmpty(Server) || String.IsNullOrEmpty(Querry))
            {
                //Console.WriteLine("All config fields have to be set [-api, -user, -pw, -server, -regex]");
                //Console.WriteLine("Canceling");
                Setup = false;
            }



            if (Setup)
            {
                Account account = new Account(User, Password, Server);
                phone = new Phone(account);


                if (File.Exists("blacklist.txt"))
                {
                    using (StreamReader sr = new StreamReader("blacklist.txt"))
                    {
                        Blacklist.AddRange(sr.ReadToEnd().Split(new char[] { ',', ';', ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList());
                    }
                }

                if (File.Exists("whitelist.txt"))
                {
                    using (StreamReader sr = new StreamReader("whitelist.txt"))
                    {
                        Whitelist.AddRange(sr.ReadToEnd().Split(new char[] { ',', ';', ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList());
                    }
                }

                //Console.WriteLine("Whitelist:");
                foreach (var item in Whitelist)
                {
                    //Console.WriteLine(item);
                }

                //Console.WriteLine("Blacklist:");
                foreach (var item in Blacklist)
                {
                    //Console.WriteLine(item);
                }

                ////


                phone.PhoneConnectedEvent += delegate ()
                {
                    //Console.WriteLine("Phone registered");
                };

                phone.CallActiveEvent += delegate (Call call)
                {
                    //Console.WriteLine("Call activ");
                    AccCall = call;
                };

                phone.CallCompletedEvent += delegate (Call call)
                {
                    phone.TerminateCall(call);
                    //Console.WriteLine("Call ended");

                    waveOut.Stop();
                };

                phone.ErrorEvent += Phone_ErrorEvent;

                phone.Connect();

                phone.IncomingCallEvent += delegate (Call call)
                {
                    var number = call.GetFrom().ToLower().Split('@')[0].Replace("sip:", "");

                    //Console.WriteLine(number + " is calling.");

                    if (CheckIfSpam(number))
                    {
                        //Console.WriteLine("It's spam...");
                        LogCall(number);
                        phone.ReceiveCallAndRecord(call, DateTime.Now.ToString().Replace(".", "_").Replace(":", "-") + "_" + number + ".wav");
                        Thread.Sleep(500);
                        PlaySounds(number);
                    }
                    else
                    {
                        //Console.WriteLine("It's a real call!");
                    }
                };
            }
            //else
            //    Console.ReadLine();

        }

        protected override void OnStop()
        {
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

            return false;
        }

        public static void PlaySounds(string number)
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


            WaveFileReader waveReader = new NAudio.Wave.WaveFileReader(audiofilepath);
            waveOut = new WaveOut();
            waveOut.PlaybackStopped += OutputDevice_PlaybackStopped;
            waveOut.Init(waveReader);
            waveOut.Play();
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
