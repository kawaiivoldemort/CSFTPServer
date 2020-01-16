using System;
using System.Net;
using TCPClientWrapper;
using static System.Console;

namespace FTP
{
    struct Data
    {
        public static string[] commandTable = new string[]
        {
            //"ABOR ", //abort a file transfer
            //"PORT ", //open a data port
            //"SITE ", //site-specific commands
            //"TYPE ", //set transfer type
            //"ACCT* ",//send account information
            //"APPE ", //append to a remote file
            //"HELP ", //return help on using the server
            //"MODE ", //set transfer mode
            //"NOOP ", //do nothing
            //"REIN* ",//reinitialize the connection
            //"STAT ", //return server status
            //"STOU ", //store a file uniquely
            //"STRU ", //set file transfer structure
            //"SYST "  //return system type
        };
    }

    class FTPClient
    {
        ClientSocket serverCommandSocket;
        ClientSocket serverTransferSocket;
        string remoteAddress;
        int port = 21;
        int statusCode = 0;

        string username;
        string password;
        string commandEnd = "\r\n";

        string workingDirectory = "";

        private bool isConnected = false;

        //Connect
        public bool Connect(string address)
        {
            try
            {
                this.remoteAddress = address;
                serverCommandSocket = new ClientSocket();
                SocketConnectArgs args = serverCommandSocket.ConnectThread(Dns.GetHostAddresses(address)[0], port);
                if (!args.isConnected) throw new FTPCannotConnectException(args.exception.Message);
                char[] response = WaitForStatus();
                Print(response);
                statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                if (statusCode != 220)
                {
                    serverCommandSocket.Disconnect();
                    throw new FTPCannotConnectException(args.exception.Message);
                }
                return true;
            }
            catch(FTPCannotConnectException exception)
            {
                WriteLine(exception);
                return false;
            }
        }

        //Login
        public void Login()
        {
            Login("anonymous", "anonymous@domain.com");
        }
        public bool Login(string username, string password)
        {
            try
            {
                SetUsernamePassword(username, password);
                char[] response = SendCommand("USER ", this.username);
                Print(response);
                statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                if ((statusCode != 331) && (statusCode != 230))
                {
                    throw new LoginException(statusCode + " status : login Failed for Unknown Reason");
                }
                response = SendCommand("PASS ", this.password);
                Print(response);
                statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                if (statusCode != 230)
                {
                    throw new LoginException(statusCode + " status : login Failed due to Incorrect Credentials");
                }
                isConnected = true;
            }
            catch(LoginException exception)
            {
                WriteLine(exception.Message);
                isConnected = false;
            }
            return isConnected;
        }

        //Get Socket for Transfer in Passive Mode
        private void GetTransferSocket()
        {
            try
            {
                char[] response = SendCommand("PASV ");
                statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                if (statusCode != 227)
                {
                    throw new FTPException(statusCode + " status : unable to transfer, task failed");
                }
                Print(response);
                bool start = false;
                int arrayCounter = 0;
                string[] stringServerDetails = new string[6];
                for (int counter = 0; counter < response.Length; counter++)
                {
                    if (response[counter] == '(') { start = true; }
                    else if (start)
                    {
                        if (response[counter] == ',') arrayCounter++;
                        else if (response[counter] == ')') start = false;
                        else stringServerDetails[arrayCounter] += response[counter];
                    }
                }
                serverTransferSocket = new ClientSocket();
                serverTransferSocket.ConnectThread(Dns.GetHostAddresses(stringServerDetails[0] + "." + stringServerDetails[1] + "." + stringServerDetails[2] + "." + stringServerDetails[3])[0], int.Parse(stringServerDetails[4]) * 256 + int.Parse(stringServerDetails[5]));
            }
            catch(FTPException exception)
            {
                WriteLine(exception.Message);
            }
        }

        //Get Working Directory
        public void GetWorkingDirectory()
        {
            try
            {
                if (isConnected)
                {
                    char[] response = SendCommand("PWD ");
                    Print(response);
                    statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    if (statusCode != 257)
                    {
                        throw new FTPException(statusCode + " status : unable to get Working Directory");
                    }
                    bool start = false;
                    for (int counter = 0; counter < response.Length; counter++)
                    {
                        if (response[counter] == '"') start = (start == true) ? false : true;
                        else if (start == true)
                        {
                            workingDirectory += response[counter];
                        }
                    }
                }
                else throw new IsNotConnectedException("Not Connected");
            }
            catch (FTPException exception)
            {
                WriteLine(exception.Message);
            }
        }

        //List Files
        public void ListFilesWithDetails()
        {
            try
            {
                GetTransferSocket();
                if (isConnected)
                {
                    char[] response = SendCommand("LIST ");
                    Print(response);
                    statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    if (statusCode != 150)
                    {
                        throw new FTPException(statusCode + " status : unable to List Files");
                    }
                    response = serverTransferSocket.RecieveStringThread();
                    Print(response);
                    serverTransferSocket.Disconnect();
                    serverTransferSocket = null;
                    //response = WaitForStatus();
                    //Print(response);
                    //statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    //if (statusCode != 226)
                    //{
                    //    throw new FTPException(statusCode + " status : unable to complete Download");
                    //}
                }
                else throw new IsNotConnectedException("Not Connected");
            }
            catch(FTPException exception)
            {
                WriteLine(exception.Message);
            }
            catch (IsNotConnectedException exception)
            {
                WriteLine(exception.Message);
                Logout();
            }
        }
        public void ListFilesForParsing()
        {
            try
            {
                GetTransferSocket();
                if (isConnected)
                {
                    char[] response = SendCommand("MLSD ");
                    Print(response);
                    statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    if (statusCode != 150)
                    {
                        throw new FTPException(statusCode + " status : unable to List Files");
                    }
                    response = serverTransferSocket.RecieveStringThread();
                    Print(response);
                    serverTransferSocket.Disconnect();
                    serverTransferSocket = null;
                    //response = WaitForStatus();
                    //Print(response);
                    //statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    //if (statusCode != 226)
                    //{
                    //    throw new FTPException(statusCode + " status : unable to complete Download");
                    //}
                }
                else throw new IsNotConnectedException("Not Connected");
            }
            catch (FTPException exception)
            {
                WriteLine(exception.Message);
            }
            catch (IsNotConnectedException exception)
            {
                WriteLine(exception.Message);
                Logout();
            }
        }
        public void ListFiles()
        {
            try
            {
                GetTransferSocket();
                if (isConnected)
                {
                    char[] response = SendCommand("NLST ");
                    Print(response);
                    statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    if (statusCode != 150)
                    {
                        throw new FTPException(statusCode + " status : unable to List Files");
                    }
                    response = serverTransferSocket.RecieveStringThread();
                    Print(response);
                    serverTransferSocket.Disconnect();
                    serverTransferSocket = null;
                    //response = WaitForStatus();
                    //Print(response);
                    //statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    //if (statusCode != 226)
                    //{
                    //    throw new FTPException(statusCode + " status : unable to complete Download");
                    //}
                }
                else throw new IsNotConnectedException("Not Connected");
            }
            catch (FTPException exception)
            {
                WriteLine(exception.Message);
            }
            catch (IsNotConnectedException exception)
            {
                WriteLine(exception.Message);
                Logout();
            }
        }

        //Get File Size
        public UInt64 GetFileSize(string fileName)
        {
            try
            {
                if (isConnected)
                {
                    char[] response = SendCommand("SIZE ", fileName);
                    Print(response);
                    statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    if (statusCode != 213)
                    {
                        throw new FTPException(statusCode + " status : unable to get File Size");
                    }
                    UInt64 fileSize = 0;
                    for (int temp = 4; temp < response.Length; temp++)
                    {
                        if (char.IsDigit(response[temp]))
                        {
                            fileSize += (UInt64)(response[temp] - '0');
                            fileSize *= 10;
                        }
                        else break;
                    }
                    fileSize /= 10;
                    return fileSize;
                }
                else throw new IsNotConnectedException("Not Connected");
            }
            catch (FTPException exception)
            {
                WriteLine(exception.Message);
            }
            catch (IsNotConnectedException exception)
            {
                WriteLine(exception.Message);
                Logout();
            }
            return 0;
        }

        //Get File Modification Time
        public void GetFileModificationTime(string fileName)
        {
            try
            {
                if (isConnected)
                {
                    char[] response = SendCommand("MDTM ", fileName);
                    Print(response);
                    statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    if (statusCode != 213)
                    {
                        throw new FTPException(statusCode + " status : unable to get File Modification Time");
                    }
                    int fileSize = 0;
                    for (int temp = 4; temp < response.Length; temp++)
                    {
                        if (char.IsDigit(response[temp]))
                        {
                            fileSize += response[temp] - '0';
                            fileSize *= 10;
                        }
                        else break;
                    }
                    fileSize /= 10;
                }
                else throw new IsNotConnectedException("Not Connected");
            }
            catch (FTPException exception)
            {
                WriteLine(exception.Message);
            }
            catch (IsNotConnectedException exception)
            {
                WriteLine(exception.Message);
                Logout();
            }
        }

        //Upload
        public void Upload(string path)
        {
            string fileName;
            int positionAt = path.LastIndexOf('\\');
            if (positionAt == -1) fileName = path;
            else fileName = path.Substring(positionAt + 1);
            Upload(path, fileName);
        }
        public void Upload(string path, string fileName)
        {
            try
            {
                GetTransferSocket();
                if (isConnected)
                {
                    char[] response = SendCommand("STOR ", workingDirectory + fileName);
                    Print(response);
                    statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    if (statusCode != 150)
                    {
                        throw new FTPException(statusCode + " status : unable to Upload");
                    }
                    Print(response);
                    serverTransferSocket.SendFileThread(path);
                    serverTransferSocket.Disconnect();
                    serverTransferSocket = null;
                    response = WaitForStatus();
                    Print(response);
                    statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    if (statusCode != 226)
                    {
                        throw new FTPException(statusCode + " status : unable to complete Upload");
                    }
                }
                else throw new IsNotConnectedException("Not Connected");
            }
            catch (FTPException exception)
            {
                WriteLine(exception.Message);
            }
            catch (IsNotConnectedException exception)
            {
                WriteLine(exception.Message);
                Logout();
            }
        }

        //Download
        public void Download(string downloadFileName)
        {
            Download(downloadFileName, workingDirectory, downloadFileName);
        }
        public void Download(string downloadFileName, string saveFilePath)
        {
            Download(downloadFileName, saveFilePath, downloadFileName);
        }
        public void Download(string downloadFileName, string saveFilePath, string newName)
        {
            try
            {
                GetTransferSocket();
                if (isConnected)
                {
                    char[] response = SendCommand("RETR ", workingDirectory + downloadFileName);
                    Print(response);
                    statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    if (statusCode == 550)
                    {
                        throw new FTPException(statusCode + " status : unable to Download");
                    }
                    serverTransferSocket.RecieveFileThread(saveFilePath + newName);
                    serverTransferSocket.Disconnect();
                    serverTransferSocket = null;
                    response = WaitForStatus();
                    Print(response);
                    statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    if (statusCode != 226)
                    {
                        throw new FTPException(statusCode + " status : unable to complete Download");
                    }
                }
                else throw new IsNotConnectedException("Not Connected");
            }
            catch (FTPException exception)
            {
                WriteLine(exception.Message);
            }
            catch (IsNotConnectedException exception)
            {
                WriteLine(exception.Message);
                Logout();
            }
        }

        //Change Working Directory
        public void ChangeWorkingDirectory(string folderName)
        {
            try
            {
                if (isConnected)
                {
                    char[] response = SendCommand("CWD ", workingDirectory + folderName);
                    Print(response);
                    statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    if (statusCode == 550)
                    {
                        throw new FTPException(statusCode + " status : unable to Change Directory");
                    }
                }
                else throw new IsNotConnectedException("Not Connected");
            }
            catch (FTPException exception)
            {
                WriteLine(exception.Message);
            }
            catch (IsNotConnectedException exception)
            {
                WriteLine(exception.Message);
                Logout();
            }
        }

        //Go Up
        public void GoUp()
        {
            try
            {
                if (isConnected)
                {
                    char[] response = SendCommand("CDUP ");
                    Print(response);
                    statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                }
                else throw new IsNotConnectedException("Not Connected");
            }
            catch (IsNotConnectedException exception)
            {
                WriteLine(exception.Message);
                Logout();
            }
        }

        //Delete File
        public void DeleteFile(string fileName)
        {
            try
            {
                if (isConnected)
                {
                    char[] response = SendCommand("DELE ", workingDirectory + fileName);
                    Print(response);
                    statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    if (statusCode == 550)
                    {
                        throw new FTPException(statusCode + " status : unable to Delete File");
                    }
                }
                else throw new IsNotConnectedException("Not Connected");
            }
            catch (FTPException exception)
            {
                WriteLine(exception.Message);
            }
            catch (IsNotConnectedException exception)
            {
                WriteLine(exception.Message);
                Logout();
            }
        }

        //Delete File
        public void RenameFile(string fileName, string newName)
        {
            try
            {
                if (isConnected)
                {
                    char[] response = SendCommand("RNFR ", fileName);
                    Print(response);
                    statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    if (statusCode != 350)
                    {
                        throw new FTPException(statusCode + " status : unable to Rename File");
                    }
                    response = SendCommand("RNTO ", newName);
                    Print(response);
                    statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    if (statusCode != 250)
                    {
                        throw new FTPException(statusCode + " status : File not Renamed");
                    }
                }
                else throw new IsNotConnectedException("Not Connected");
            }
            catch (FTPException exception)
            {
                WriteLine(exception.Message);
            }
            catch (IsNotConnectedException exception)
            {
                WriteLine(exception.Message);
                Logout();
            }
        }

        //Create Directory
        public void MakeDirectory(string folderName)
        {
            try
            {
                if (isConnected)
                {
                    char[] response = SendCommand("MKD ", folderName);
                    Print(response);
                    statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    if ((statusCode != 250) && (statusCode != 257))
                    {
                        throw new FTPException(statusCode + " status : Unable to Make Directory");
                    }
                }
                else throw new IsNotConnectedException("Not Connected");
            }
            catch (FTPException exception)
            {
                WriteLine(exception.Message);
            }
            catch (IsNotConnectedException exception)
            {
                WriteLine(exception.Message);
                Logout();
            }
        }

        //Delete Directory
        public void DeleteDirectory(string folderName)
        {
            try
            {
                if (isConnected)
                {
                    char[] response = SendCommand("RMD ", folderName);
                    Print(response);
                    statusCode = 100 * (response[0] - '0') + 10 * (response[1] - '0') + (response[2] - '0');
                    if ((statusCode != 250) && (statusCode != 257))
                    {
                        throw new FTPException(statusCode + " status : Unable to Delete Directory");
                    }
                }
                else throw new IsNotConnectedException("Not Connected");
            }
            catch (FTPException exception)
            {
                WriteLine(exception.Message);
            }
            catch (IsNotConnectedException exception)
            {
                WriteLine(exception.Message);
                Logout();
            }
        }

        //Logout
        public void Logout()
        {
            try
            {
                if (isConnected)
                {
                    char[] response = SendCommand("QUIT ");
                    Print(response);
                    if (statusCode != 221)
                    {
                        throw new FTPException(statusCode + " status : Not Logged Out");
                    }
                    serverCommandSocket.Disconnect();
                }
                else throw new IsNotConnectedException("Not Connected");
            }
            catch (IsNotConnectedException exception)
            {
                WriteLine(exception.Message);
                Logout();
            }
        }

        //Wait for a Status Message
        private char[] WaitForStatus()
        {
            string buffer = new string(serverCommandSocket.RecieveStringThread());
            string[] commandLines = buffer.Split('\n');
            for (int temp = 0; temp < commandLines.Length; temp++)
            {
                if (commandLines[temp].IndexOf(' ') != 3) Write(commandLines[temp]);
                else return (commandLines[temp] + '\n').ToCharArray();
            }
            //throw exception
            return null;
        }

        //Username and Password
        public void SetUsernamePassword(string username, string password)
        {
            this.username = username;
            this.password = password;
        }

        //Send a Command
        public char[] SendCommand(string command)
        {
            serverCommandSocket.SendStringThread((command + commandEnd).ToCharArray());
            return WaitForStatus();
        }
        public char[] SendCommand(string command, string parameters)
        {
            serverCommandSocket.SendStringThread((command + parameters + commandEnd).ToCharArray());
            return WaitForStatus();
        }

        //Print a Char Array
        public void Print(char[] characterArray)
        {
            foreach (char c in characterArray) if (c != '\0') Write(c);
        }
    }

    class ClientConsole
    {
        public static void ClientInterface()
        {
            FTPClient client = new FTPClient();
            string address;
            do
            {
                Write("Please enter the IP Address : ");
                address = ReadLine();
            }
            while (!client.Connect(address));
            Write("Login\nLogin as Anonymous? (Enter Y for Yes) : ");
            char c = (ReadKey()).KeyChar;
            WriteLine();
            string username;
            string password;
            if ((c != 'Y') && (c != 'y'))
            {
                do
                {
                    Write("Please enter your Username : ");
                    username = ReadLine();
                    Write("Please enter your Password : ");
                    password = ReadLine();
                }
                while (!client.Login(username, password));
            }
            else
            {
                client.Login();
            }
            string command;
            do
            {
                Write(">");
                command = ReadLine();
                Command(command, client);
            }
            while (!command.Equals("quit") || command.Equals("exit"));
        }

        static void Command(string command, FTPClient client)
        {
            //string stringCommand = command.Substring(0, 3);
            string[] parts = command.Split(' ');
            switch (parts[0])
            {
                case "pwd":
                    client.GetWorkingDirectory();
                    break;
                case "ls":
                    if ((parts.Length > 1) && (parts[1].Equals("-l"))) client.ListFilesWithDetails();
                    else client.ListFiles();
                    break;
                case "sizeof":
                    client.GetFileSize(parts[1]);
                    break;
                case "mtime":
                    client.GetFileModificationTime(parts[1]);
                    break;
                case "upload":
                    if (parts.Length > 2) client.Upload(parts[1], parts[2]);
                    else client.Upload(parts[1]);
                    break;
                case "download":
                    if (parts.Length > 3) client.Download(parts[1], parts[2], parts[3]);
                    else if (parts.Length > 2) client.Download(parts[1], parts[2]);
                    else client.Download(parts[1]);
                    break;
                case "cd":
                    if (parts[1].Equals("..")) client.GoUp();
                    else client.ChangeWorkingDirectory(parts[1]);
                    break;
                case "rm":
                    client.DeleteFile(parts[1]);
                    break;
                case "rename":
                    client.RenameFile(parts[1], parts[2]);
                    break;
                case "mkdir":
                    client.MakeDirectory(parts[1]);
                    break;
                case "rmdir":
                    client.DeleteDirectory(parts[1]);
                    break;
                case "quit":
                    client.Logout();
                    break;
                case "exit":
                    client.Logout();
                    break;
                default:
                    WriteLine("Command not found");
                    break;
            }
        }
    }

    public class FTPCannotConnectException : Exception
    {
        public FTPCannotConnectException(string message) : base(message)
        {
        }
    }

    public class LoginException : Exception
    {
        public LoginException(string message) : base(message)
        {
        }
    }

    public class FTPException : Exception
    {
        public FTPException(string message) : base(message)
        {
        }
    }

    public class IsNotConnectedException : Exception
    {
        public IsNotConnectedException(string message) : base(message)
        {
        }
    }
}