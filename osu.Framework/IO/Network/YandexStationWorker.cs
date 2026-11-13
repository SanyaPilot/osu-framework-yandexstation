// Copyright (c) SanyaPilot 2026

using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;

namespace osu.Framework.IO.Network
{
    public class YandexStationWorker
    {
        private UdpClient? client;
        private CancellationTokenSource cancellation = new();
        private Task? task;

        private string hostname;
        private int port;
        public string Hostname
        {
            get => hostname;
            set
            {
                hostname = value;
                Connect();
            }
        }
        public int Port
        {
            get => port;
            set
            {
                port = value;
                Connect();
            }
        }

        public event EventHandler<string>? PacketReceived;

        public YandexStationWorker(string hostname, int port)
        {
            this.hostname = hostname;
            this.port = port;
        }

        public void Connect()
        {
            cancellation.Cancel();
            cancellation = new();
            Logger.Log("[YandexStation] Connecting to station!", LoggingTarget.Network);
            Task.Run(async () =>
            {
                CancellationToken recvTaskToken = cancellation.Token;
                using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(recvTaskToken);

                // Wait for previous task to stop
                if (task != null)
                    await task.ConfigureAwait(false);

                // Recreate client to drop all previous data
                client?.Dispose();
                client = new();

                try
                {
                    await client.SendAsync(Encoding.ASCII.GetBytes("ping"), hostname, port).ConfigureAwait(false);
                    timeoutCancellation.CancelAfter(5000);
                    UdpReceiveResult packet = await client.ReceiveAsync(timeoutCancellation.Token).ConfigureAwait(false);
                    string reply = Encoding.ASCII.GetString(packet.Buffer);
                    if (reply != "pong")
                        throw new InvalidOperationException("Wrong device reply: " + reply);

                    task = receiveTask(recvTaskToken);
                    Logger.Log("Connected to the Yandex Station successfully!", LoggingTarget.Network, LogLevel.Important);
                }
                catch (OperationCanceledException)
                {
                    if (!recvTaskToken.IsCancellationRequested)  // Then it's a timeout
                        Logger.Log("Connection to the Yandex Station failed! Timeout", LoggingTarget.Network, LogLevel.Error);
                    // Do nothing
                }
                catch (Exception e)
                {
                    Logger.Log("Connection to the Yandex Station failed! " + e.Message, LoggingTarget.Network, LogLevel.Error);
                }
            });
        }

        private async Task receiveTask(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    UdpReceiveResult packet = await client!.ReceiveAsync(token).ConfigureAwait(false);
                    PacketReceived?.Invoke(this, Encoding.ASCII.GetString(packet.Buffer));
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Do nothing
            }
            catch (Exception e)
            {
                Logger.Log("Error while receiving Yandex Station input! " + e.Message, LoggingTarget.Network, LogLevel.Error);
            }
        }

        private void send(string data)
        {
            try
            {
                client?.Send(Encoding.ASCII.GetBytes(data), hostname, port);
            }
            catch (SocketException e)
            {
                Logger.Log("Failed sending data to Yandex Station: " + e.Message, LoggingTarget.Network, LogLevel.Error);
            }
        }

        public void NotifyGameplayStarted(double timerOffset)
        {
            send("action:start:" + Math.Round(timerOffset, 2));
        }

        public void NotifyGameplayEnded()
        {
            send("action:stop");
        }

        public void NotifyGameplayPaused()
        {
            send("action:pause");
        }

        public void NotifyGameplayResumed()
        {
            send("action:resume");
        }

        public void DisplaySPM(int targetSpins)
        {
            send("display:spm:" + targetSpins);
        }

        public void DisplayTime()
        {
            send("display:time");
        }
    }
}
