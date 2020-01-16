using System.Net.Sockets;
using System.Threading.Tasks;
using System.IO;
using static System.Console;
using TCPServerWrapper;
using System.Text;
using System;
using System.Collections.Generic;

namespace Console_Server_Asynchronous
{
    struct Data
    {
        public static char[][] stringTable = new char[][]
        {
            "220 Nachi's FTP Server 0.0 ready...\r\n".ToCharArray()
        };

        public static char[][] userTable = new char[][]
        {
            "anonymous".ToCharArray()
        };

        public static char[][] passwordTable = new char[][]
        {
            "anonymous@domain.com".ToCharArray()
        };
    }

    class FTPServer
    {
        Socket[] clientCollection;
        int numberOfConnections = 0;
        int listeningPort;

        //Add New Connection
        int AddConnection(Socket socket)
        {
            for (int temp = 0; temp < clientCollection.Length; temp++)
            {
                if (clientCollection[temp] == null)
                {
                    clientCollection[temp] = socket;
                    numberOfConnections += 1;
                    return temp;
                }
            }
            return -1;
        }

        //Begins Server
        public void StartConnecting(int listeningPort, int maxConnections)
        {
            ServerSocket server = new ServerSocket();
            this.listeningPort = listeningPort;
            clientCollection = new Socket[maxConnections];
            server.Initialize(listeningPort);
            while (true)
            {
                if (numberOfConnections < 10)
                {
                    int connectionKey = AddConnection(server.Listen());
                    ClientThread client = new ClientThread();
                    Task.Run(() => client.ServerResponseThread(connectionKey, maxConnections, clientCollection[connectionKey]));
                }
            }
        }

        //Remove Connection
        public void RemoveConnection(int key)
        {
            ServerSocket.Disconnect(clientCollection[key]);
            clientCollection[key] = null;
            numberOfConnections -= 1;
        }
    }

    class ClientThread
    {
        Socket client;
        Socket clientTransfer;
        int maxConnections;
        int port = 50000;
        string workingDirectory;
        string home;
        List<FileSystemItem> fileSystem;
        bool utf8 = false;
        bool asciiType = false;

        public void ServerResponseThread(int key, int maxConnections, Socket client)
        {
            this.client = client;
            this.clientTransfer = null;
            this.maxConnections = maxConnections;
            this.port += key;
            ServerSocket.SendString(Data.stringTable[0], client);
            bool connected = false;
            if (Authenticate(client)) connected = true;
            workingDirectory = @"/";
            home = workingDirectory;
            fileSystem = new List<FileSystemItem>();
            MakeFileSystem();
            while (connected)
            {
                char[] array = ServerSocket.RecieveString(client);
                if ((array.Length == 4)&&(array[0] == 'Q') && (array[1] == 'U') && (array[2] == 'I') && (array[3] == 'T')) break;
                DoCommand(client, array);
            }
        }

        bool Authenticate(Socket socket)
        {
            char[] buffer = ServerSocket.RecieveString(socket);
            string[] command = (new string(buffer)).Split(' ');
            while (!command[0].Equals("USER"))
            {
                ServerSocket.SendString(("530 Not logged In. Log in first\r\n").ToCharArray(), socket);
                buffer = ServerSocket.RecieveString(socket);
                command = (new string(buffer)).Split(' ');
                //throw Exception;
                //return false;
            }
            ServerSocket.SendString("331 Anonymous login OK, send your e-mail as password\r\n".ToCharArray(), socket);
            buffer = ServerSocket.RecieveString(socket);
            command = (new string(buffer)).Split(' ');
            if (command[0].Equals("PASS"))
            {
                ServerSocket.SendString("230 Login OK\n".ToCharArray(), socket);
            }
            else
            {
                //throw Exception;
                //return false;
            }
            return true;
        }

        void DoCommand(Socket clientCommand, char[] buffer)
        {
            string[] command = (new string(buffer)).Split(' ', '\r', '\n');
            string path;
            command[0] = command[0].ToUpper();
            switch (command[0].ToUpper())
            {
                case "SYST":
                    ServerSocket.SendString(("215 UNIX Type: L8\r\n").ToCharArray(), clientCommand);
                    break;
                case "FEAT":
                    ServerSocket.SendString(("211-Features:\n MDTM\n NLST\n MLST type *; size *; modify *;\n MLSD  UTF8\n REST STREAM\n SIZE\n211 End\r\n").ToCharArray(), clientCommand);
                    break;
                case "TYPE":
                    //if (command.Length == 1) ServerSocket.SendString(("TYPE I\r\n").ToCharArray(), clientCommand);
                    if (command[1].Equals("A"))
                    {
                        asciiType = true;
                        ServerSocket.SendString(("200 OK\r\n").ToCharArray(), clientCommand);
                    }
                    else if (command[1].Equals("I"))
                    {
                        asciiType = false;
                        ServerSocket.SendString(("200 OK\r\n").ToCharArray(), clientCommand);
                    }
                    else ServerSocket.SendString(("504 Command not implemented for that parameter.").ToCharArray(), clientCommand);
                    break;
                case "PASV":
                    if(clientTransfer != null)
                    {
                        clientTransfer.Close();
                        clientTransfer = null;
                    }
                    ServerSocket transferSocket = new ServerSocket();
                    string[] endPointParts = clientCommand.LocalEndPoint.ToString().Split('.', ':');
                    transferSocket.Initialize(endPointParts[0] + '.' + endPointParts[1] + '.' + endPointParts[2] + '.' + endPointParts[3], port);
                    ServerSocket.SendString(("227 Entering Passive Mode (" + endPointParts[0] + ',' + endPointParts[1] + ',' + endPointParts[2] + ',' + endPointParts[3] + ',' + (port / 256) + ',' + (port % 256) + ")\n").ToCharArray(), clientCommand);
                    clientTransfer = transferSocket.Listen();
                    break;
                case "PWD":
                    ServerSocket.SendString(("257 \"" + workingDirectory + "\"\r\n").ToCharArray(), clientCommand);
                    break;
                case "MLST":
                    ServerSocket.SendString(("250-Listing " + workingDirectory + ":\n" + fileSystem.Find(item => item.name.Equals(command[1])).MachineList() + " " + fileSystem.Find(item => item.name.Equals(command[1])).name + "\n250 End.\r\n").ToCharArray(), clientCommand);
                    break;
                case "SIZE":
                    ServerSocket.SendString(("213 " + fileSystem.Find(item => item.name.Equals(command[1])).size + "\r\n").ToCharArray(), clientCommand);
                    break;
                case "MDTM":
                    ServerSocket.SendString(("213 " + fileSystem.Find(item => item.name.Equals(command[1])).modificationTime + "\r\n").ToCharArray(), clientCommand);
                    break;
                case "CWD":
                    if (!command[1].StartsWith("/"))
                    {
                        path = workingDirectory + command[1];
                        if (!path.EndsWith("/")) path += "/";
                    }
                    else
                    {
                        if (command[1].LastIndexOf("/") != 0)
                        {
                            path = home + command[1];
                            if (command[1].EndsWith("/")) path += "/";
                        }
                        else path = home;
                    }
                    if (Directory.Exists(path))
                    {
                        workingDirectory = path;
                        MakeFileSystem();
                        ServerSocket.SendString("250 Directory Successfully Changed\r\n".ToCharArray(), clientCommand);
                    }
                    else ServerSocket.SendString(("550 Can't change directory to \"" + path + "\"\r\n").ToCharArray(), clientCommand);
                    break;
                case "CDUP":
                    if (!workingDirectory.Equals("/")) workingDirectory = workingDirectory.Substring(0, workingDirectory.Length - workingDirectory.IndexOf('/', 0, workingDirectory.Length - 1));
                    MakeFileSystem();
                    ServerSocket.SendString(("250 Directory successfully changed\r\n").ToCharArray(), clientCommand);
                    break;
                case "DELE":
                    if ((File.Exists(workingDirectory + command[1])) && !(new FileInfo(workingDirectory + command[1])).IsReadOnly)
                    {
                        File.Delete(workingDirectory + command[1]);
                        if (fileSystem.Remove(fileSystem.Find(item => item.name.Equals(command[1])))) ServerSocket.SendString(("250 Deleted file \"" + command[1] + "\"\r\n").ToCharArray(), clientCommand);
                        else ServerSocket.SendString(("550 Can't delete file \"" + command[1] + "\"\r\n").ToCharArray(), clientCommand);
                    }
                    else ServerSocket.SendString(("550 Can't delete file \"" + command[1] + "\"\r\n").ToCharArray(), clientCommand);
                    break;
                //case "RNFR":
                //    if ((File.Exists(workingDirectory + command[1])) && !(new FileInfo(workingDirectory + command[1])).IsReadOnly)
                //    {
                //        File.Delete(workingDirectory + command[1]);
                //        ServerSocket.SendString(("250 Deleted file \"" + command[1] + "\"\r\n").ToCharArray(), clientCommand);
                //    }
                //    break;
                //case "RNTO":
                //    break;
                case "MKD":
                    path = workingDirectory;
                    path += command[1];
                    if (!command[1].EndsWith("/")) path += "/";
                    try
                    {
                        if (!Directory.Exists(path)) throw new IOException();
                        fileSystem.Add(new FileSystemItem(Directory.CreateDirectory(path)));
                        ServerSocket.SendString(("257 \"" + path + "\" directory created\r\n").ToCharArray(), clientCommand);
                    }
                    catch
                    {
                        ServerSocket.SendString(("521 Can't create directory\r\n").ToCharArray(), clientCommand);
                    }
                    break;
                case "RMD":
                    path = workingDirectory;
                    path += command[1];
                    if (!command[1].EndsWith("/")) path += "/";
                    try
                    {
                        Directory.Delete(path, true);
                        if (fileSystem.Remove(fileSystem.Find(item => item.name.Equals(command[1])))) ServerSocket.SendString(("250 Directory removed\r\n").ToCharArray(), clientCommand);
                        else ServerSocket.SendString(("550 Can't remove directory\r\n").ToCharArray(), clientCommand);
                    }
                    catch
                    {
                        ServerSocket.SendString(("550 Can't remove directory\r\n").ToCharArray(), clientCommand);
                    }
                    break;
                case "LIST":
                    if (clientTransfer != null)
                    {
                        ServerSocket.SendString("150 Opening BINARY Mode for Data Connection for LIST\n".ToCharArray(), clientCommand);
                        ListFilesWithDetails(clientTransfer);
                        ServerSocket.SendString(("226 Transfer Complete\r\n").ToCharArray(), clientCommand);
                        ServerSocket.Disconnect(clientTransfer);
                        clientTransfer = null;
                        port += maxConnections;
                    }
                    else ServerSocket.SendString(("426 Use PORT or PASV first\r\n").ToCharArray(), clientCommand);
                    break;
                case "MLSD":
                    if (clientTransfer != null)
                    {
                        ServerSocket.SendString("150 Opening BINARY Mode for Data Connection for MLSD\n".ToCharArray(), clientCommand);
                        ListFilesForMachines(clientTransfer);
                        ServerSocket.SendString(("226 Transfer Complete\r\n").ToCharArray(), clientCommand);
                        ServerSocket.Disconnect(clientTransfer);
                        clientTransfer = null;
                        port += maxConnections;
                    }
                    else ServerSocket.SendString(("426 Use PORT or PASV first\r\n").ToCharArray(), clientCommand);
                    break;
                case "NLST":
                    if (clientTransfer != null)
                    {
                        ServerSocket.SendString("150 Opening BINARY Mode for Data Connection for NLST\n".ToCharArray(), clientCommand);
                        ListFiles(clientTransfer);
                        ServerSocket.SendString(("226 Transfer Complete\r\n").ToCharArray(), clientCommand);
                        ServerSocket.Disconnect(clientTransfer);
                        clientTransfer = null;
                        port += maxConnections;
                    }
                    else ServerSocket.SendString(("426 Use PORT or PASV first\r\n").ToCharArray(), clientCommand);
                    break;
                case "STOR":
                    if (clientTransfer != null)
                    {
                        ServerSocket.Disconnect(clientTransfer);
                        clientTransfer = null;
                        port += maxConnections;
                    }
                    else ServerSocket.SendString(("426 Use PORT or PASV first\r\n").ToCharArray(), clientCommand);
                    break;
                case "RETR":
                    if (clientTransfer != null)
                    {
                        ServerSocket.Disconnect(clientTransfer);
                        clientTransfer = null;
                        port += maxConnections;
                    }
                    else ServerSocket.SendString(("426 Use PORT or PASV first\r\n").ToCharArray(), clientCommand);
                    break;
                case "OPTS":
                    utf8 = true;
                    ServerSocket.SendString(("200 Command Ok\r\n").ToCharArray(), clientCommand);
                    break;
                case "NOOP":
                    ServerSocket.SendString(("200 Ok\r\n").ToCharArray(), clientCommand);
                    break;
                case "QUIT":
                    break;
                default:
                    ServerSocket.SendString(("502 Unknown ftp command\r\n").ToCharArray(), clientCommand);
                    break;
            }
        }

        void MakeFileSystem()
        {
            fileSystem.Clear();
            DirectoryInfo root = new DirectoryInfo(workingDirectory);
            DirectoryInfo[] directories = root.GetDirectories();
            FileInfo[] files = root.GetFiles();
            foreach (DirectoryInfo directory in directories)
            {
                try { fileSystem.Add(new FileSystemItem(directory)); }
                catch { }
            }
            foreach (FileInfo file in files)
            {
                try { fileSystem.Add(new FileSystemItem(file)); }
                catch { }
            }
        }

        void ListFilesForMachines(Socket socket)
        {
            StringBuilder folderContents = new StringBuilder("");
            foreach (FileSystemItem item in fileSystem)
            {
                folderContents.Append(item.MachineList() + "\n");
            }
            folderContents.Remove(folderContents.Length - 1, 1);
            folderContents.Append("\r\n");
            Write(folderContents);
            char[] charFolderContents = new char[folderContents.Length];
            folderContents.CopyTo(0, charFolderContents, 0, folderContents.Length);
            if (utf8) ServerSocket.SendStringUTF8(charFolderContents, socket);
            else ServerSocket.SendString(charFolderContents, socket);
        }

        void ListFilesWithDetails(Socket socket)
        {
            StringBuilder folderContents = new StringBuilder("");
            foreach (FileSystemItem item in fileSystem)
            {
                folderContents.Append(item.DetailedList() + "\n");
            }
            folderContents.Remove(folderContents.Length - 1, 1);
            folderContents.Append("\r\n");
            Write(folderContents);
            char[] charFolderContents = new char[folderContents.Length];
            folderContents.CopyTo(0, charFolderContents, 0, folderContents.Length);
            if (utf8) ServerSocket.SendStringUTF8(charFolderContents, socket);
            else ServerSocket.SendString(charFolderContents, socket);
        }

        void ListFiles(Socket socket)
        {
            StringBuilder folderContents = new StringBuilder("");
            foreach (FileSystemItem item in fileSystem)
            {
                if(!item.isDirectory) folderContents.Append(item.name + "\n");
            }
            folderContents.Remove(folderContents.Length - 1, 1);
            folderContents.Append("\r\n");
            Write(folderContents);
            char[] charFolderContents = new char[folderContents.Length];
            folderContents.CopyTo(0, charFolderContents, 0, folderContents.Length);
            if (utf8) ServerSocket.SendStringUTF8(charFolderContents, socket);
            else ServerSocket.SendString(charFolderContents, socket);
        }

        /*void MachineListing(Socket socket)
        {
            DirectoryInfo info = new DirectoryInfo(workingDirectory);
            DirectoryInfo[] directories = info.GetDirectories();
            FileInfo[] files = info.GetFiles();
            StringBuilder folderContents = new StringBuilder();
            int currentYear = DateTime.Now.Year;
            foreach (DirectoryInfo directory in directories)
            {
                string modifiedDate = directory.LastWriteTimeUtc.Year < currentYear ? directory.LastWriteTime.ToString("MMM dd  yyyy") : directory.LastWriteTime.ToString("MMM dd HH:mm");
                folderContents.Append("drw-rw-rw- " + "1 " + "ftp " + "ftp " + "0 ".PadLeft(17) + modifiedDate + " " + directory.ToString() + '\n');
            }
            foreach (FileInfo file in files)
            {
                string modifiedDate = file.LastWriteTimeUtc.Year < currentYear ? file.LastWriteTime.ToString("MMM dd  yyyy") : file.LastWriteTime.ToString("MMM dd HH:mm");
                folderContents.Append("-rw-rw-rw- " + "1 " + "ftp " + "ftp " + file.Length.ToString().PadLeft(16) + " " + modifiedDate + " " + file.ToString() + '\n');
            }
            folderContents.Remove(folderContents.Length - 1, 1);
            folderContents.Append("\r\n");
            Write(folderContents);
            char[] charFolderContents = new char[folderContents.Length];
            folderContents.CopyTo(0, charFolderContents, 0, folderContents.Length);
            if (utf8) ServerSocket.SendStringUTF8(charFolderContents, socket);
            else ServerSocket.SendString(charFolderContents, socket);
        }*/

        //Print a Char Array
        public void Print(char[] characterArray)
        {
            foreach (char c in characterArray) if (c != '\0') Write(c);
        }
    }

    class FileSystemItem
    {
        public string name;
        public string permissions;
        public long size;
        public string modificationTime;
        public string modifiedDate;
        public static int numberOfLinks = 1;
        public static string owner = "ftp";
        public static string user = "user";
        public static int currentYear = DateTime.Now.Year;
        public bool isDirectory;

        public FileSystemItem(FileInfo file)
        {
            name = file.Name;
            permissions = new string(file.GetFilePermissions());
            size = file.Length;
            modificationTime = file.LastWriteTimeUtc.Year.ToString("0000") + file.LastWriteTimeUtc.Month.ToString("00") + file.LastWriteTimeUtc.Day.ToString("00") + file.LastWriteTimeUtc.Hour.ToString("00") + file.LastWriteTimeUtc.Minute.ToString("00") + file.LastWriteTimeUtc.Second.ToString("00");
            modifiedDate = file.LastWriteTimeUtc.Year < currentYear ? file.LastWriteTime.ToString("MMM dd  yyyy") : file.LastWriteTime.ToString("MMM dd HH:mm");
            isDirectory = false;
        }
        public FileSystemItem(DirectoryInfo directory)
        {
            name = directory.Name;
            permissions = new string(directory.GetDirectoryPermissions());
            size = 0;
            modificationTime = directory.LastWriteTimeUtc.Year.ToString("0000") + directory.LastWriteTimeUtc.Month.ToString("00") + directory.LastWriteTimeUtc.Day.ToString("00") + directory.LastWriteTimeUtc.Hour.ToString("00") + directory.LastWriteTimeUtc.Minute.ToString("00") + directory.LastWriteTimeUtc.Second.ToString("00");
            modifiedDate = directory.LastWriteTimeUtc.Year < currentYear ? directory.LastWriteTime.ToString("MMM dd  yyyy") : directory.LastWriteTime.ToString("MMM dd HH:mm");
            isDirectory = true;
        }

        public string DetailedList()
        {
            return permissions + " " + numberOfLinks + " " + owner + " " + user + " " + size.ToString().PadLeft(16) + " " + modifiedDate + " " + name.ToString();
        }

        public string MachineList()
        {
            return "Type=" + ((isDirectory) ? "dir" : "file") + ";Size=" + size.ToString() + ";Modify=" + modificationTime.ToString() + "; " + name;
        }
    }

    static class SubFileSystemExtension
    {
        //Directory System Extension
        public static char[] GetDirectoryPermissions(this DirectoryInfo directory)
        {
            System.Security.AccessControl.AuthorizationRuleCollection rules = directory.GetAccessControl().GetAccessRules(true, true, typeof(System.Security.Principal.NTAccount));
            char[] permissions = { 'd', '-', '-', '-', '-', '-', '-', '-', '-', '-' };
            int index = -1;
            foreach (System.Security.AccessControl.FileSystemAccessRule rule in rules)
            {
                permissions[1] = 'r';
                permissions[2] = 'w';
                permissions[3] = 'x';
                if (rule.IdentityReference.ToString().Contains("Administrators")) index = 4;
                else if (rule.IdentityReference.ToString().Contains("Users")) index = 7;
                if (index != -1)
                {
                    if (rule.FileSystemRights.ToString().Contains("FullControl"))
                    {
                        permissions[index] = 'r';
                        permissions[index + 1] = 'w';
                        permissions[index + 2] = 'x';
                    }
                    else if (rule.FileSystemRights.ToString().Contains("ReadAndExecute"))
                    {
                        permissions[index] = 'r';
                        permissions[index + 2] = 'x';
                    }
                    else if (rule.FileSystemRights.ToString().Contains("Modify"))
                    {
                        permissions[index + 1] = 'w';
                    }
                }
                index = -1;
            }
            return permissions;
        }
        //File System Extension
        public static char[] GetFilePermissions(this FileInfo file)
        {
            System.Security.AccessControl.AuthorizationRuleCollection rules = file.GetAccessControl().GetAccessRules(true, true, typeof(System.Security.Principal.NTAccount));
            char[] permissions = { '-', '-', '-', '-', '-', '-', '-', '-', '-', '-' };
            int index = -1;
            foreach (System.Security.AccessControl.FileSystemAccessRule rule in rules)
            {
                permissions[1] = 'r';
                permissions[2] = 'w';
                permissions[3] = 'x';
                if (rule.IdentityReference.ToString().Contains("Administrators")) index = 4;
                else if (rule.IdentityReference.ToString().Contains("Users")) index = 7;
                if (index != -1)
                {
                    if (rule.FileSystemRights.ToString().Contains("FullControl"))
                    {
                        permissions[index] = 'r';
                        permissions[index + 1] = 'w';
                        permissions[index + 2] = 'x';
                    }
                    else if (rule.FileSystemRights.ToString().Contains("ReadAndExecute"))
                    {
                        permissions[index] = 'r';
                        permissions[index + 2] = 'x';
                    }
                    else if (rule.FileSystemRights.ToString().Contains("Modify"))
                    {
                        permissions[index + 1] = 'w';
                    }
                }
                index = -1;
            }
            return permissions;
        }
    }

    class ServerConsole
    {
        public static void ServerInterface()
        {
            FTPServer server = new FTPServer();
            server.StartConnecting(21, 10);
        }
    }
}
