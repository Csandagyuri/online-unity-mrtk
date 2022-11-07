using Nakama;
using Nakama.TinyJson;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using WebSocketSharp;
using WebSocketSharp.Server;

namespace csharp_client
{
    public class Echo : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e)
        {
            Console.WriteLine(e.Data);
            Send(e.Data);
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            SocketCommunicationReciever socketReciever = new SocketCommunicationReciever();
            CommunicationTargetGroup targets = new CommunicationTargetGroup();
            CommunicationController communicationController = new CommunicationController(targets, "asd");
            var headlessClient = new HeadlessClient(targets);
            var roomCreatingNakamaClient = new NakamaTarget();
            var partyID = roomCreatingNakamaClient.createParty();
            targets.AddTarget(roomCreatingNakamaClient);
            while (Console.ReadLine() != "")
            {
                var joiningNakama = new NakamaTarget();
                joiningNakama.joinParty(partyID);
                targets.AddTarget(joiningNakama);
                targets.AddTarget(new SocketTarget("ws://127.0.0.1:7890/Echo"));
                Console.WriteLine($"Current active targets {targets.Count()}");
            }
        }

        private static void PrintMessage(object sender, MessageEventArgs e)
        {
            Console.WriteLine(sender);
            Console.WriteLine(e.Data);
        }
    }

    class HeadlessClient
    {
        CommunicationTargetGroup targets;
        Vector3 position = new Vector3(0, 0, 0);

        public HeadlessClient(CommunicationTargetGroup targets)
        {
            this.targets = targets;
            var sendingTimer = new System.Timers.Timer();
            sendingTimer.Elapsed += new ElapsedEventHandler(SendPositionMessages);
            sendingTimer.Interval = 1000;
            sendingTimer.Start();
        }

        private void SendPositionMessages(object sender, ElapsedEventArgs e)
        {
            targets.SendMessages(position.ToString());
        }
    }

    class SocketCommunicationReciever
    {
        private WebSocketServer wssv = new WebSocketServer("ws://127.0.0.1:7890");

        public SocketCommunicationReciever()
        {
            wssv.AddWebSocketService<Echo>("/Echo");
            wssv.Start();
            Console.WriteLine("Server Started on ws://127.0.0.1:7890/Echo");
        }

        ~SocketCommunicationReciever()
        {
            wssv.Stop();
        }
    }

    abstract class CommunicationTarget
    {
        public abstract void SendMessage(String message);
    }

    class SocketTarget : CommunicationTarget
    {
        WebSocket ws;
        public SocketTarget(String url)
        {
            ws = new WebSocket(url);
            ws.Connect();
        }

        public override void SendMessage(string message)
        {
            ws.Send(message);
        }
    }

    class CommunicationTargetGroup
    {
        List<CommunicationTarget> targets = new List<CommunicationTarget>();

        public void AddTarget(CommunicationTarget target)
        {
            targets.Add(target);
        }

        public int Count()
        {
            return targets.Count;
        }

        public void RemoveTarget(CommunicationTarget target)
        {
            targets.Remove(target);
        }

        public void SendMessages(String message)
        {
            foreach (CommunicationTarget target in targets){
                target.SendMessage(message);
            }
        }
    }

    class CommunicationController
    {
        CommunicationTargetGroup clientCommunicationTargetGroup;
        string controllerURL;

        public CommunicationController(CommunicationTargetGroup clientCommunicationTargetGroup, string controllerURL)
        {
            this.clientCommunicationTargetGroup = clientCommunicationTargetGroup;
            this.controllerURL = controllerURL;
            new Thread(() =>
            {
                var sendingTimer = new System.Timers.Timer();
                sendingTimer.Elapsed += new ElapsedEventHandler(CommunicateWithAPIServerAsync);
                sendingTimer.Interval = 5000;
                sendingTimer.Start();
            }).Start();
        }

        private void CommunicateWithAPIServerAsync(object source, ElapsedEventArgs e)
        {
            var response = SendAPIRequest();
            ProcessResponse(response);
        }

        private string SendAPIRequest()
        {
            Console.WriteLine($"Sending REST API request to {controllerURL}");
            Thread.Sleep(500);
            return "test";
        }

        private void ProcessResponse(string response)
        {
            Console.WriteLine("ProcessingResponse");
            Thread.Sleep(500);
        }
    }

    class NakamaTarget : CommunicationTarget
    {
        private string scheme = "http";
        private string host = "localhost";
        private int port = 7350;
        private string serverKey = "defaultkey";

        private IClient client;
        private ISocket socket;

        private string partyID;

        private string ticket;

        private string matchId;

        private Vector3 selfPosition = new Vector3(0, 0, 0);
        private Vector3 remoteUserPosition = new Vector3(0, 0, 0);

        public NakamaTarget()
        {
            client = new Client(scheme, host, port, serverKey);
            var sessionTask = AuthenticateUser();
            var session = sessionTask.Result;
            Console.WriteLine(session);
            socket = Socket.From(client);
            socket.Connected += () =>
            {
                System.Console.WriteLine("Socket connected.");
            };
            socket.Closed += () =>
            {
                System.Console.WriteLine("Socket closed.");
            };
            socket.ConnectAsync(session, true);

            socket.ReceivedPartyData += OnReceivedMatchStatePosition;

            Console.WriteLine(session);
            Console.WriteLine(socket);

            
        }

        public string createParty()
        {
            var party = socket.CreatePartyAsync(true, 256);
            partyID = party.Result.Id;
            Console.WriteLine(partyID);
            return party.Result.Id;
        }

        public async void joinParty(string partyID)
        {
            this.partyID = partyID;
            Thread.Sleep(1000);
            await socket.JoinPartyAsync(partyID);
        }

        private async Task<ISession> AuthenticateUser()
        {
            var email = "super@heroes.com";
            var password = "batsignal";
            return await client.AuthenticateEmailAsync(email, password);
        }

        public override void SendMessage(string message)
        {
            var position = CurrentPositionAsJson();
            Console.WriteLine(position);
            socket.SendPartyDataAsync(partyID, 1, position);
        }

        public void RandomizeMovement()
        {
            Random r = new Random();
            selfPosition = new Vector3(r.Next(-5, 5), r.Next(-5, 5), r.Next(-5, 5));
        }

        private void OnReceivedMatchStatePosition(IPartyData partyData)
        {
            var positionDictionary = DictFromJson(partyData.Data);
            remoteUserPosition = new Vector3(
                float.Parse(positionDictionary["position.x"]),
                float.Parse(positionDictionary["position.y"]),
                float.Parse(positionDictionary["position.z"])
            );
            Console.WriteLine(remoteUserPosition);
        }

        private async void UpdatePosition(IMatchState matchState)
        {
            Console.WriteLine("Sending position");
            await socket.SendMatchStateAsync(matchId, 1, CurrentPositionAsJson(), new[] { matchState.UserPresence });
        }

        private string CurrentPositionAsJson()
        {
            var NewPosition = new Dictionary<string, string>
            {
                {"position.x", selfPosition.X.ToString() },
                {"position.y", selfPosition.X.ToString() },
                {"position.z", selfPosition.X.ToString() }
            };
            return NewPosition.ToJson();
        }

        private IDictionary<string, string> DictFromJson(byte[] state)
        {
            return Encoding.UTF8.GetString(state).FromJson<Dictionary<string, string>>();
        }

    }


}
