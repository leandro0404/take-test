using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Take.Processo.Seletivo
{
    public static class WebSocketServer
    {
        private static int SocketCounter = 0;
        private static bool ServerIsRunning = true;
        private static ConcurrentDictionary<int, ConnectedClient> Clients = new ConcurrentDictionary<int, ConnectedClient>();
        private static HttpListener Listener;
        private static CancellationTokenSource SocketLoopTokenSource;
        private static CancellationTokenSource ListenerLoopTokenSource;

        public static void Start(string uriPrefix)
        {
            SocketLoopTokenSource = new CancellationTokenSource();
            ListenerLoopTokenSource = new CancellationTokenSource();
            Listener = new HttpListener();
            Listener.Prefixes.Add(uriPrefix);
            Listener.Start();
            if (Listener.IsListening)
                Task.Run(() => ListenerProcessingLoopAsync().ConfigureAwait(false));
            else
                Console.WriteLine("Server failed to start.");
        }

        public static void SendMessage(string message)
        {
            foreach (KeyValuePair<int, ConnectedClient> client in Clients)
                client.Value.BroadcastQueue.Add(message);
        }

        public static void SendPrivateMessage(int socketId, string message)
        {
            Clients.Where((x => x.Key == socketId))
                .FirstOrDefault().Value.BroadcastQueue
                .Add(message);
        }

        private static async Task ListenerProcessingLoopAsync()
        {
            CancellationToken cancellationToken = ListenerLoopTokenSource.Token;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    HttpListenerContext context = await Listener.GetContextAsync();
                    if (ServerIsRunning)
                    {
                        if (context.Request.IsWebSocketRequest)
                        {
                            HttpListenerWebSocketContext wsContext = null;
                            try
                            {
                                wsContext = await context.AcceptWebSocketAsync(null);
                                int socketId = Interlocked.Increment(ref SocketCounter);
                                ConnectedClient client = new ConnectedClient(socketId, GetUserName(context), wsContext.WebSocket);
                                Clients.TryAdd(socketId, client);
                                await Task.Run(() => SocketProcessingLoopAsync(client).ConfigureAwait(false));
                            }
                            catch (Exception ex)
                            {
                                context.Response.StatusCode = 500;
                                context.Response.StatusDescription = "WebSocket upgrade failed";
                                context.Response.Close();
                                break;
                            }
                         
                        }
                        else
                        {
                            if (((IEnumerable<string>)context.Request.AcceptTypes).Contains<string>("text/html"))
                            {
                                ReadOnlyMemory<byte> HtmlPage = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(HtmlRead.Get()));
                                context.Response.ContentType = "text/html; charset=utf-8";
                                context.Response.StatusCode = 200;
                                context.Response.StatusDescription = "OK";
                                context.Response.ContentLength64 = (long)HtmlPage.Length;
                                await context.Response.OutputStream.WriteAsync(HtmlPage, CancellationToken.None);
                                await context.Response.OutputStream.FlushAsync(CancellationToken.None);
                                HtmlPage = new ReadOnlyMemory<byte>();
                            }
                            else
                                context.Response.StatusCode = 400;
                            context.Response.Close();
                        }
                        
                    }
                    else
                    {
                        context.Response.StatusCode = 409;
                        context.Response.StatusDescription = "Server is shutting down";
                        context.Response.Close();
                        break;
                    }
                }
            }
            catch (HttpListenerException ex1)
            {
            }
        }

        private static async Task SocketProcessingLoopAsync(ConnectedClient client)
        {
            await Task.Run(() => client.BroadcastLoopAsync().ConfigureAwait(false));
            WebSocket socket = client.Socket;
            CancellationToken loopToken = SocketLoopTokenSource.Token;
            CancellationTokenSource broadcastTokenSource = client.BroadcastLoopTokenSource;
            try
            {
                ArraySegment<byte> buffer = WebSocket.CreateServerBuffer(4096);
                while (socket.State != WebSocketState.Closed && socket.State != WebSocketState.Aborted && !loopToken.IsCancellationRequested)
                {
                    WebSocketReceiveResult receiveResult = await client.Socket.ReceiveAsync(buffer, loopToken);
                    if (!loopToken.IsCancellationRequested)
                    {
                        if (client.Socket.State == WebSocketState.CloseReceived && receiveResult.MessageType == WebSocketMessageType.Close)
                        {
                            broadcastTokenSource.Cancel();
                            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "fechar Socket", CancellationToken.None);
                        }
                        if (client.Socket.State == WebSocketState.Open)
                        {
                            string message = Encoding.UTF8.GetString(buffer.Array, 0, receiveResult.Count);
                            SendMessage(client.UserName + " : " + message);
                           
                        }
                    }
                   
                }
               
            }
            catch (OperationCanceledException ex)
            {
            }
            catch (Exception ex1)
            {
                Exception ex = ex1;
                Console.WriteLine(string.Format("Socket {0}:", client.SocketId));
            }
            finally
            {
                broadcastTokenSource.Cancel();
                if (client.Socket.State != WebSocketState.Closed)
                    client.Socket.Abort();
                ConnectedClient connectedClient;
                if (Clients.TryRemove(client.SocketId, out connectedClient))
                    socket.Dispose();
            }
        }

        private static string GetUserName(HttpListenerContext context)
        {
            string str = context.Request.QueryString["UserName"];
            foreach (KeyValuePair<int, ConnectedClient> client in Clients)
            {
                if (client.Value.UserName.ToLower() == str.ToLower())
                    context.Response.Close();
            }
            return str;
        }
    }
}
