using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CallManager
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        System.Net.Sockets.TcpClient clientSocket = new System.Net.Sockets.TcpClient();
        NetworkStream serverStream;

        public string lastCall;


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!clientSocket.Connected || string.IsNullOrEmpty(lastCall) || (LastReport - DateTime.Now).TotalMinutes > 2)
                return;

            Send(lastCall);

            lastCall = string.Empty;
            TBOutput.Text = TBOutput.Text + "(Blocked)";
        }

        private void Send(string msg)
        {
            NetworkStream serverStream = clientSocket.GetStream();
            byte[] outStream = System.Text.Encoding.ASCII.GetBytes(msg + "$");
            serverStream.Write(outStream, 0, outStream.Length);
            serverStream.Flush();
        }

        private System.Windows.Forms.NotifyIcon m_notifyIcon;


        private void ExitItem_Click(object sender, EventArgs e)
        {
            closeForReal = true;
            Close();
        }

        DateTime LastReport;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LastReport = DateTime.Now;

            m_notifyIcon = new System.Windows.Forms.NotifyIcon();
            m_notifyIcon.BalloonTipText = "Call manager has been minimised. Click the tray icon to show.";
            m_notifyIcon.BalloonTipTitle = "Call manager";
            m_notifyIcon.Text = "Call manager";
            m_notifyIcon.Icon = new System.Drawing.Icon("telephone_icon.ico");
            m_notifyIcon.MouseClick += m_notifyIcon_Click;


            System.Windows.Forms.ContextMenu menu = new System.Windows.Forms.ContextMenu();
            System.Windows.Forms.MenuItem exitItem = new System.Windows.Forms.MenuItem();
            exitItem.Text = "Exit";
            exitItem.Click += ExitItem_Click;
            menu.MenuItems.Add(exitItem);

            m_notifyIcon.ContextMenu = menu;


            byte[] bytesFrom = new byte[65536];
            string dataFromClient = null;

            Thread t = new Thread(() =>
            {
                while ((true))
                {
                    try
                    {
                        if (!clientSocket.Connected)
                        {
                            while (Connect() == false)
                            {
                                Thread.Sleep(1000);
                            }

                            Dispatcher.Invoke(() => { TBOutput.Text += TBOutput.Text.Length > 0 ? Environment.NewLine + "Connected" : "Connected"; TBOutput.ScrollToEnd(); });
                        }

                        bool failed = false;

                        NetworkStream networkStream = clientSocket.GetStream();
                        while (!networkStream.DataAvailable)
                        {
                            try
                            {
                                Send("beakon");
                            }
                            catch (Exception ex)
                            {
                                failed = true;
                                break;
                            }
                            Thread.Sleep(100);
                        }

                        if (failed)
                        {
                            Dispatcher.Invoke(() => { TBOutput.Text += Environment.NewLine + "Disconnected"; TBOutput.ScrollToEnd(); });
                            continue;
                        }

                        networkStream.Read(bytesFrom, 0, (int)clientSocket.Available);
                        dataFromClient = System.Text.Encoding.ASCII.GetString(bytesFrom);
                        dataFromClient = dataFromClient.Substring(0, dataFromClient.IndexOf("$"));

                        lastCall = dataFromClient;
                        LastReport = DateTime.Now;

                        Dispatcher.Invoke(() =>
                         {
                             TBOutput.Text = TBOutput.Text + Environment.NewLine + "New call from: " + dataFromClient;
                             TBOutput.ScrollToEnd();

                             Show();
                             WindowState = m_storedWindowState;
                         });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(" >> " + ex.ToString());
                    }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        private bool Connect()
        {
            string serverIP = Environment.GetCommandLineArgs()[1];

            try
            {
                clientSocket.Close();
                clientSocket = new System.Net.Sockets.TcpClient();
                clientSocket.Connect(serverIP, 8778);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        bool closeForReal = false;

        void OnClose(object sender, CancelEventArgs args)
        {
            if (closeForReal)
            {
                m_notifyIcon.Dispose();
                m_notifyIcon = null;
            }
            else
            {
                args.Cancel= true;
                WindowState = WindowState.Minimized;
            }     
        }

        private WindowState m_storedWindowState = WindowState.Normal;
        void OnStateChanged(object sender, EventArgs args)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                if (m_notifyIcon != null)
                    m_notifyIcon.ShowBalloonTip(2000);
            }
            else
                m_storedWindowState = WindowState;
        }
        void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            CheckTrayIcon();
        }



        private void m_notifyIcon_Click(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                Show();
                WindowState = m_storedWindowState;
            }
            else
            {

            }
        }


        void CheckTrayIcon()
        {
            ShowTrayIcon(!IsVisible);
        }

        void ShowTrayIcon(bool show)
        {
            if (m_notifyIcon != null)
                m_notifyIcon.Visible = show;
        }


    }
}
