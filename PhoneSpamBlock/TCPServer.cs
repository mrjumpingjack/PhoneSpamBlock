using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PhoneSpamBlock
{
    public class TCPServer
    {
        public List<handleClinet> Clients = new List<handleClinet>();

        public event EventHandler<string> BlockCall;

        public void Start()
        {
            TcpListener serverSocket = new TcpListener(8778);
            TcpClient clientSocket = default(TcpClient);
            int counter = 0;

            serverSocket.Start();
            Console.WriteLine(" >> " + "Server Started");

            counter = 0;

            Thread thread = new Thread(() =>
             {
                 while (true)
                 {
                     counter += 1;
                     clientSocket = serverSocket.AcceptTcpClient();
                     Console.WriteLine(" >> " + "Client No:" + Convert.ToString(counter) + " started!");
                     handleClinet client = new handleClinet();
                     Clients.Add(client);
                     client.startClient(clientSocket, Convert.ToString(counter));
                     client.BlockCall += Client_BlockCall;
                 }
             });
            thread.IsBackground = true;
            thread.Start();
        }



        public void Stop()
        {
            foreach (var client in Clients)
            {
                client.Stop();
            }
        }


        public void Broadcast(string msg)
        {
            foreach (handleClinet client in Clients.Where(c=>c.clientSocket.Connected))
            {
                try
                {
                    if (client.isOnline())
                        client.Send(msg);
                }
                catch (Exception ex)
                {
                    try
                    {
                        client.Stop();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine(ex1.Message);
                    }

                    Console.WriteLine(ex.Message);
                }
            }

            for (int i = Clients.Count - 1; i >= 0; i--)
            {
                if (!Clients[i].clientSocket.Connected)
                    Clients.RemoveAt(i);
            }

        }


        private void Client_BlockCall(object sender, string e)
        {
            BlockCall.Invoke(sender, e);
        }
    }


    //Class to handle each client request separatly
    public class handleClinet
    {
        public event EventHandler<string> BlockCall;

        bool beakonRecived = true;

        public TcpClient clientSocket;
        Thread ctThread;
        Thread CheckBeakonThread;
        string clNo;
        public void startClient(TcpClient inClientSocket, string clineNo)
        {
            this.clientSocket = inClientSocket;
            this.clNo = clineNo;

            ctThread = new Thread(doChat);
            ctThread.IsBackground = true;
            ctThread.Start();

            CheckBeakonThread = new Thread(CheckBekon);
            CheckBeakonThread.IsBackground = true;
            CheckBeakonThread.Start();
        }

        public void Stop()
        {
            ctThread.Abort();
            clientSocket.Close();
        }

        private void CheckBekon()
        {
           
        }

        private void doChat()
        {
            int requestCount = 0;
            byte[] bytesFrom = new byte[65536];
            string dataFromClient = null;


            requestCount = 0;

            while ((true))
            {
                try
                {
                    requestCount = requestCount + 1;
                    NetworkStream networkStream = clientSocket.GetStream();

                    while (!networkStream.DataAvailable)
                    {
                        Thread.Sleep(100);
                    }
                    
                    networkStream.Read(bytesFrom, 0, (int)clientSocket.ReceiveBufferSize);
                    dataFromClient = System.Text.Encoding.ASCII.GetString(bytesFrom);
                    dataFromClient = dataFromClient.Substring(0, dataFromClient.IndexOf("$"));

                    if (dataFromClient == "beakon")
                        beakonRecived = true;
                    else
                        Console.WriteLine(" >> " + "From client " + clNo + ": " + dataFromClient);

                    if (!String.IsNullOrEmpty(dataFromClient))
                        BlockCall.Invoke(null, dataFromClient);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: " + ex.ToString());
                }
            }
        }

        public void Send(String msg,bool addDollar=true)
        {
            NetworkStream networkStream = clientSocket.GetStream();

            Byte[] sendBytes = null;

            sendBytes = Encoding.ASCII.GetBytes(msg + (addDollar == true ? "$" : ""));
            networkStream.Write(sendBytes, 0, sendBytes.Length);
            networkStream.Flush();
            Console.WriteLine(" >> " + msg);
        }

        public bool isOnline()
        {
            return clientSocket.Connected && clientSocket.Client.Connected;
        }

    }
}
