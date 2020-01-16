using System;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using static System.Console;

namespace TCPClientWrapper
{
    class ClientSocket : IDisposable
    {
        Socket server;

        public FileTransferProgressArgs progress = null;

        //Connect
        public SocketConnectArgs ConnectThread(IPAddress ip, int port)
        {
            Task<SocketConnectArgs> connect = Task<SocketConnectArgs>.Run(() => Connect(ip, port));
            while (!connect.IsCompleted) ;
            if (connect.Result.isConnected)
            {
                server = connect.Result.socket;
                //server.ReceiveTimeout = 1000;
                //server.SendTimeout = 1000;
            }
            return connect.Result;
        }
        static SocketConnectArgs Connect(IPAddress ip, int port)
        {
            var socketToConnectTo = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            SocketConnectArgs args = new SocketConnectArgs();
            try
            {
                socketToConnectTo.Connect(new IPEndPoint(ip, port));
                args.socket = socketToConnectTo;
                args.isConnected = true;
            }
            catch (Exception e)
            {
                args.isConnected = false;
                args.exception = e;
            }
            return args;
        }

        //Send String
        public void SendStringThread(Char[] stringArray)
        {
            Task sendString = Task.Run(() => SendString(stringArray, server));
            while (!sendString.IsCompleted) ;
        }
        static void SendString(Char[] sendString, Socket socket)
        {
            try
            {
                Encoder encoder = Encoding.ASCII.GetEncoder();
                UInt16 packetSize = 1024;
                var buffer = new byte[packetSize];
                UInt16 stringSizeInBytes = (UInt16)(sendString.Length);
                UInt16 numberOfPackets = (UInt16)((stringSizeInBytes + packetSize - 1) / packetSize);
                int packetsSent = 0;
                if (sendString.Length > 1024)
                {
                    for (UInt16 temp = 0; temp < (numberOfPackets - 1); temp++)
                    {
                        encoder.GetBytes(sendString, packetsSent * 1024, packetSize, buffer, 0, true);
                        socket.Send(buffer, packetSize, SocketFlags.None);
                        packetsSent += 1;
                    }
                }
                packetSize = (UInt16)(stringSizeInBytes - ((numberOfPackets - 1) * 1024));
                encoder.GetBytes(sendString, packetsSent * 1024, packetSize, buffer, 0, true);
                socket.Send(buffer, packetSize, SocketFlags.None);
                packetsSent += 1;
            }
            catch (Exception) { }

        }

        //Recieve String
        public char[] RecieveStringThread()
        {
            Task<char[]> stringArray = Task<char[]>.Run(() => RecieveString(server));
            while (!stringArray.IsCompleted) ;
            return stringArray.Result;
        }
        static char[] RecieveString(Socket socket)
        {
            StringBuilder recieveString = new StringBuilder("");
            try
            {
                Encoding decoder = Encoding.ASCII;
                UInt16 packetSize = 1024;
                var buffer = new byte[packetSize];
                UInt16 bytesRead = 0;
                UInt64 totalBytesRead = 0;
                do
                {
                    bytesRead = (UInt16)socket.Receive(buffer, packetSize, SocketFlags.None);
                    totalBytesRead += bytesRead;
                    recieveString.Append(decoder.GetString(buffer, 0, bytesRead));
                }
                while (bytesRead == packetSize);
            }
            catch (Exception) { }
            char[] recievedString = new char[recieveString.Length];
            recieveString.CopyTo(0, recievedString, 0, recieveString.Length);
            return recievedString;
        }

        //Send File
        public void SendFileThread(string path)
        {
            var fileTransferProgress = new Progress<FileTransferProgressArgs>(updateProgress);
            Task sendFile = Task.Run(() => SendFile(path, server, fileTransferProgress));
            while (progress == null) ;
            while (!sendFile.IsCompleted)
            {
                ClearLine();
                Write("Sent {0} kb : {1}%", progress.packetsTransferred, (progress.packetsTransferred * 100 / progress.numberOfPackets));
            }
            WriteLine();
            resetProgress();
        }
        static void SendFile(string path, Socket socket, IProgress<FileTransferProgressArgs> fileTransferProgress)
        {
            int packetSize = 1024;
            FileStream uploadFileStream = null;
            try
            {
                uploadFileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, (int)packetSize);
                UInt64 fileSize = 0;
                var buffer = new byte[packetSize];
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
                    fileTransferProgress.Report(progressArgs);
                }
                packetSize = (int)(fileSize - ((numberOfPackets - 1) * 1024));
                buffer = new byte[(int)packetSize];
                uploadFileStream.Read(buffer, 0, packetSize);
                socket.Send(buffer);
                progressArgs.packetsTransferred += 1;
                fileTransferProgress.Report(progressArgs);
                uploadFileStream.Close();
            }
            catch (FileNotFoundException) { }
            catch(Exception)
            {
                if(uploadFileStream != null) uploadFileStream.Close();
            }
        }

        //Recieve File
        public void RecieveFileThread(String path/*, UInt64 fileSize*/)
        {
            var fileTransferProgress = new Progress<FileTransferProgressArgs>(updateProgress);
            Task recieveFile = Task.Run(() => RecieveFile(path, server/*, fileSize*/, fileTransferProgress));
            while (progress == null) ;
            /*WriteLine("Recieving {0} byte file ", progress.FileSize);*/
            while (!recieveFile.IsCompleted) { ClearLine(); Write("Recieved {0} kb"/* : {1}%*/, progress.packetsTransferred/*, (progress.packetsTransferred * 100 / progress.numberOfPackets) */); }
            resetProgress();
        }
        static void RecieveFile(String path, Socket socket/*, UInt64 fileSize*/, IProgress<FileTransferProgressArgs> fileTransferProgress)
        {
            UInt16 packetSize = 1024;
            FileStream downloadFileStream = null;
            try
            {
                downloadFileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                var buffer = new byte[packetSize];
                var progressArgs = new FileTransferProgressArgs();
                UInt16 bytesRead = 0;
                progressArgs.packetsTransferred = 0;
                /*progressArgs.FileSize = fileSize;
                progressArgs.numberOfPackets = (fileSize + packetSize - 1) / packetSize;*/
                while ((bytesRead = (UInt16)socket.Receive(buffer)) > 0)
                {
                    progressArgs.packetsTransferred += (UInt64)(bytesRead / packetSize);
                    downloadFileStream.Write(buffer, 0, bytesRead);
                    fileTransferProgress.Report(progressArgs);
                }
                downloadFileStream.Close();
            }
            catch (FileNotFoundException) { }
            catch (Exception)
            {
                if (downloadFileStream != null) downloadFileStream.Close();
            }
        }

        //Clear Line
        static void ClearLine()
        {
            int currentLineCursor = CursorTop;
            SetCursorPosition(0, CursorTop);
            Write(new string(' ', WindowWidth));
            SetCursorPosition(0, currentLineCursor);
        }

        //File Transfer Process Progress
        public void updateProgress(FileTransferProgressArgs args)
        {
            progress = args;
        }
        public void resetProgress()
        {
            progress = null;
        }

        //Disconnect
        public void Disconnect()
        {
            Task disconnect = Task.Factory.StartNew(() => DisconnectThread(server));
            while (!disconnect.IsCompleted) ;
            server.Close();
            Dispose();
        }
        static void DisconnectThread(Socket socket)
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Disconnect(false);
        }
        public void Dispose()
        {
            server.Dispose();
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