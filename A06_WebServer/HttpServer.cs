﻿/* FILE: HttpServer.cs
 * DATE: Nov 2020
 * AUTHORS: Joel Smith & Ian Ewing
 * PROJECT: WDD A06 Web Server
 * DESCRIPTION: main server logic and functionality here
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace A06_WebServer
{
    /// <summary>
    /// to be used for functions related to webserver
    /// </summary>
    public class HttpServer
    {
        //assigned only in constructor
        readonly string webRoot;
        readonly IPAddress webIP;
        readonly int webPort;

        //Will allow us to log any pertinent events
        public Logger.HttpServerLogger serverLog;

        //Both of these exist to allow for connection of client-browser
        private static TcpListener serverListener;
        Socket clientSocket;

        Response errorResponse;

        /// <summary>
        /// constructor for HttpServer that takes command line arguments
        /// </summary>
        /// <param name="serverRoot">command line arg for root of server directory</param>
        /// <param name="serverIP">command line arg as IPAddress for serverIP </param>
        /// <param name="serverPort">port number for server</param>
        /// <param name="serverLog">directory for all of our server logs to be written</param>
        public HttpServer(string serverRoot, IPAddress serverIP, int serverPort)
        {
            webRoot = serverRoot;
            webIP = serverIP;
            webPort = serverPort;
            serverLog = new Logger.HttpServerLogger("./myOwnWebServer.log");
        }

        /// <summary>
        /// start up server, listen?
        /// </summary>
        public void Init()
        {
            try
            {
                //initialize TcpListener
                serverListener = new TcpListener(webIP, webPort);
                serverListener.Start();
                Thread thread = new Thread(new ThreadStart(GetRequest));
                thread.Start();
                //set Listener on Thread
            }
            catch (Exception e)
            {
                //Log any exception that was thrown
                serverLog.Log("Exception occured initializing TcpListener for Server : " + e.ToString());
            }
        }

        /*
         * Listen for the browser request, ensure it meets our needs, 
         * pass to ParseRequest for heavy lifting
         * 
         */
        private void GetRequest()
        {
            int index = 0; //Will allow us to grab substrings from input
            string target = null; //The targeted file the user wants
            string version = null; //HTTP version
            string verb = null; //Method for data transmission
            int statusCode = 0; //HTTP status codes

            while (Run.Go)
            {
               try { 
                
               //Establish a socket and listen for connections
               clientSocket = serverListener.AcceptSocket();

               if (clientSocket.Connected)
               {
                   //Array of bytes to hold data received
                   Byte[] bytes = new byte[1024];

                   //Store the data in the new bytes array
                   clientSocket.Receive(bytes, bytes.Length, 0);

                   //Translate the received bytes into a HTTP request string
                   string buffer = Encoding.ASCII.GetString(bytes);

                   //Check to see if GET appears anywhere in the Request header.
                   if (buffer.IndexOf("GET") == -1)
                   {           
                            statusCode = 405; //405: Method not allowed
                            string errorMsg = "<h2>405: Method Not Allowed</h2>";
                            int length = errorMsg.Length;
                            Byte[] byteMsg = Encoding.UTF8.GetBytes(errorMsg);
                            errorResponse = new Response(1.1, statusCode, "text/html", length, byteMsg);
                            SendResponse(errorResponse);
                            clientSocket.Close(); //This might need to come out?
                            break;
                   }
                   else
                   {
                       //Grab the HTTP method and store it. 
                       verb = buffer.Substring(0, 3); //3 = num of characters in GET
                   }
                   //Grab the location of HTTP within the request string
                   index = buffer.IndexOf("HTTP");

                   //Grab the 8 characters comprising the HTTP version
                   version = buffer.Substring(index, 8);

                   //Will grab a substring from beginning to just before position of the HTTP version
                   target = buffer.Substring(0, (index - 1));
                   //Grab the index of the last forward slash + 1
                   index = (target.LastIndexOf("/") + 1);
                   //This will grab the string beginning with the first character of the filename
                   target = target.Substring(index);

                   //Log the http verb and the requested resource
                   serverLog.Log($"[REQUEST] HTTP Verb {verb} Resource: {target}");

                   //webRoot works here
                   Request browserRequest = new Request(target, webRoot);

                   //Pass our request string into ParseRequest to find out what directory and filetype to retrieve.
                   ParseRequest(browserRequest);
               }
               }
               catch (Exception e)
               {
                   serverLog.Log($"[ERROR] {e.ToString()}");
               }
            }
        }

        /// <summary>
        /// takes a request object and turns it into a response by transferring relevant metadata,
        /// measures the length and takes the bytes
        /// </summary>
        /// <param name="inputReq">request object to turn into response</param>
        public void ParseRequest(Request inputReq)
        {
            //declare our return
            Response returnResponse;
            //Declare some integers that will be used
            int messageLength;
            int statusCode;

            //Grab the file we're searching for
            string targetFile = inputReq.startLine.Target;
            //Checks if the there was no requested target URL
            if (targetFile == "")
            {
                //If true, we direct the request to our index.
                targetFile = "index.html";
            }

            //Grab our mime type and file path.
            string mimeType = MimeMapping.GetMimeMapping(targetFile);
            string filePath = webRoot + @"/" + targetFile;

            if (File.Exists(filePath) == false) //The file doesn't exist, classic 404
            {
                //Build a response object for the error message
                statusCode = 404;
                string error = "<h2>404: Not Found</h2>";
                Byte[] bytes = Encoding.UTF8.GetBytes(error);
                messageLength = error.Length;
                errorResponse = new Response(1.1, statusCode, "text/html", messageLength, bytes);
                SendResponse(errorResponse);
            }
            else if (mimeType.Contains("text") || mimeType.Contains("image")) //Filter here if contains text
            {
                //get the length
                FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                messageLength = (int)fs.Length;
                statusCode = 200; //Sucessful request
                
                //make Response object for text
                

                BinaryReader reader = new BinaryReader(fs);
                //Create an array of bytes equal in size to the length of the file stream
                Byte[] bytes = new byte[fs.Length];

                reader.Read(bytes, 0, bytes.Length);

                //Create an object to hold all pertinent response information
                returnResponse = new Response(1.1, statusCode, mimeType, messageLength, bytes);

               // returnResponse.FillBody(bytes);
                SendResponse(returnResponse);
                //Close all our resources
                reader.Close();
                fs.Close();
            }
            else
            {
                //Build a response object for the error message
                statusCode = 415;
                string error = "<h2>415: Unsupported Media Type</h2>";
                Byte[] bytes = Encoding.UTF8.GetBytes(error);
                messageLength = error.Length;
                errorResponse = new Response(1.1, statusCode, "text/html", messageLength, bytes);
                SendResponse(errorResponse);
            }

        }


        /// <summary>
        /// working sendresponse
        /// </summary>
        /// <param name="serverSend">the response to send back</param>
        public void SendResponse(Response serverSend)
        {
            //Grab local copies of information needed to send our response
            double version = serverSend.startLine.Version;
            int contentLength = Int32.Parse(serverSend.headers["Content-Length"]);
            string contentType = serverSend.headers["Content-Type"];
            int statusCode = serverSend.startLine.Code;
            
            string dateString = DateTime.Now.ToString("ddd, dd MMM yyyy H:mm:ss K");

            //Send just the header to the client. This allows us to send back negative status codes too.
            string header = $"HTTP/{version} {statusCode}\r\n" + $"Date: {dateString}\r\n" + $"Content-Type: {contentType}\r\n" + $"Content-Length: {contentLength}\r\n\r\n";
            Byte[] msg = Encoding.UTF8.GetBytes(header);
            clientSocket.Send(msg);

            //Send the actual contents of the webpage requested
            clientSocket.Send(serverSend.bodyBytes);

            //We all good in the hood
            if (statusCode != 200)
            {
                //If we're in this block, there was an issue. We need only to log the status code.
                serverLog.Log($"[RESPONSE] { statusCode }"); //Log the failed status code
            }
            else
            {
                //Remove our carriage returns/new lines so we can log all in one nice tidy line
                header = header.Replace("\n", " ");
                header = header.Replace("\r", "");
                serverLog.Log($"[RESPONSE] {header}");
            }
            clientSocket.Close(); //needed to have repeated requests
        }



        /// <summary>
        /// clears up server to exit
        /// </summary>
        public void Close()
        {
            Run.Go = false;

            serverListener.Stop();

            serverLog.Log("[SERVER STOPPED]");
        }



        

    }
}
