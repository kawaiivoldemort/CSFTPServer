using System;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using static System.Console;

namespace TCPServerWrapper
{
    class ServerSocket
    {
        Socket boundSocket;
        int listeningPort = 21;

        public void Initialize(int listeningPort)
        {
            boundSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.listeningPort = listeningPort;
            boundSocket.Bind(new IPEndPoint(IPAddress.Any, listeningPort));
        }
        public void Initialize(string ip, int listeningPort)
        {
            boundSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.listeningPort = listeningPort;
            boundSocket.Bind(new IPEndPoint(IPAddress.Parse(ip), listeningPort));
        }

        //Connect
        public Socket Listen()
        {
            Task<Socket> task = Task<Socket>.Run(() => ListenThread(listeningPort, boundSocket));
            while (!task.IsCompleted) ;
            WriteLine("Recieved connection");
            if (task.Result != null)
            {
                return task.Result;
            }
            else
            {
                WriteLine("Connection failed");
                return null;
            }
        }
        static Socket ListenThread(int port, Socket socketToHostOn)
        {
            Socket socket = null;
            try
            {
                socketToHostOn.Listen(10);
                socket = socketToHostOn.Accept();
            }
            catch (Exception e)
            {
                WriteLine(e.Message);
                if (socket != null)
                {
                    if (socket.Connected) socket.Disconnect(false);
                    socket.Dispose();
                    socket = null;
                }
            }
            return socket;
        }

        //Send String
        public static void SendString(Char[] sendString, Socket socket)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(sendString);
            socket.Send(buffer, buffer.Length, SocketFlags.None);
        }
        public static void SendStringUTF8(Char[] sendString, Socket socket)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(sendString);
            socket.Send(buffer, buffer.Length, SocketFlags.None);
        }

        //Recieve String
        public static char[] RecieveString(Socket socket)
        {
            Encoding decoder = Encoding.ASCII;
            UInt16 packetSize = 1024;
            var buffer = new byte[packetSize];
            UInt16 bytesRead = 0;
            UInt64 totalBytesRead = 0;
            StringBuilder recieveString = new StringBuilder();
            do
            {
                bytesRead = (UInt16)socket.Receive(buffer, packetSize, SocketFlags.None);
                totalBytesRead += bytesRead;
                recieveString.Append(decoder.GetString(buffer, 0, bytesRead));
            }
            while (bytesRead == packetSize);
            Console.WriteLine(recieveString);
            char[] recievedString = new char[recieveString.Length];
            recieveString.CopyTo(0, recievedString, 0, recieveString.Length);
            return recievedString;
        }
        public static char[] RecieveStringUTF8(Socket socket)
        {
            Encoding decoder = Encoding.UTF8;
            UInt16 packetSize = 1024;
            var buffer = new byte[packetSize];
            UInt16 bytesRead = 0;
            UInt64 totalBytesRead = 0;
            StringBuilder recieveString = new StringBuilder();
            do
            {
                bytesRead = (UInt16)socket.Receive(buffer, packetSize, SocketFlags.None);
                totalBytesRead += bytesRead;
                recieveString.Append(decoder.GetString(buffer, 0, bytesRead));
            }
            while (bytesRead == packetSize);
            Console.WriteLine(recieveString);
            char[] recievedString = new char[recieveString.Length];
            recieveString.CopyTo(0, recievedString, 0, recieveString.Length);
            return recievedString;
        }

        //Send File
        public static void SendFile(string path, Socket socket, IProgress<FileTransferProgressArgs> progress)
        {
            int packetSize = 1024;
            UInt64 fileSize = 0;
            var buffer = new byte[packetSize];
            FileStream uploadFileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, (int)packetSize);
            fileSize = (UInt64)uploadFileStream.Length;
            UInt64 numberOfPackets = (UInt64)((fileSize + (ulong)packetSize - 1) / ((ulong)packetSize));
            var progressArgs = new FileTransferProgressArgs();
            progressArgs.FileSize = fileSize;
            progressArgs.numberOfPackets = numberOfPackets;
            progressArgs.packetsTransferred = 0;
            for (UInt64 tempor = 0; tempor < (numberOfPackets - 1); tempor++)
            {
                uploadFileStream.Read(buffer, 0, packetSize);
                socket.Send(buffer);
                progressArgs.packetsTransferred += 1;
                progress.Report(progressArgs);
            }
            packetSize = (int)(fileSize - ((numberOfPackets - 1) * 1024));
            buffer = new byte[(int)packetSize];
            uploadFileStream.Read(buffer, 0, packetSize);
            socket.Send(buffer);
            progressArgs.packetsTransferred += 1;
            progress.Report(progressArgs);
            uploadFileStream.Close();
        }

        //Clear Line
        static void ClearLine()
        {
            int currentLineCursor = CursorTop;
            SetCursorPosition(0, CursorTop);
            Write(new string(' ', WindowWidth));
            SetCursorPosition(0, currentLineCursor);
        }

        //Disconnect
        public static void Disconnect(Socket socket)
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Disconnect(true);
            socket.Close();
            socket.Dispose();
        }
    }

    class FileTransferProgressArgs
    {
        public UInt64 FileSize { get; set; }
        public UInt64 numberOfPackets { get; set; }
        public UInt64 packetsTransferred { get; set; }
    }

    class SocketConnectArgs
    {
        public Socket socket { get; set; }
        public bool isConnected { get; set; }
        public Exception exception { get; set; }
    }

    class ConnectedSocket
    {

    }
}