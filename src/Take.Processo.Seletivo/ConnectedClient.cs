
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Take.Processo.Seletivo
{
    public class ConnectedClient
    {
        public const int BROADCAST_TRANSMIT_INTERVAL_MS = 250;

        public ConnectedClient(int socketId, string userName, WebSocket socket)
        {
            this.SocketId = socketId;
            this.UserName = userName;
            this.Socket = socket;
        }

        public int SocketId { get; private set; }
        public string UserName { get; set; }
        public WebSocket Socket { get; private set; }
        public BlockingCollection<string> BroadcastQueue { get; } = new BlockingCollection<string>();
        public CancellationTokenSource BroadcastLoopTokenSource { get; set; } = new CancellationTokenSource();

        public async Task BroadcastLoopAsync()
        {
            CancellationToken cancellationToken = this.BroadcastLoopTokenSource.Token;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(250, cancellationToken);
                    string message;
                    if (!cancellationToken.IsCancellationRequested && this.Socket.State == WebSocketState.Open && this.BroadcastQueue.TryTake(out message))
                    {
                        ArraySegment<byte> msgbuf = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
                        await this.Socket.SendAsync(msgbuf, WebSocketMessageType.Text, true, CancellationToken.None);
                        msgbuf = new ArraySegment<byte>();
                    }
                    message = (string)null;
                }
                catch (OperationCanceledException ex)
                {
                }
                catch (Exception ex1)
                {
                    Exception ex = ex1;
                }
            }
        }
    }
}
