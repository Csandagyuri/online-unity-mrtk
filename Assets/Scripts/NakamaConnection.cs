using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Nakama;

public class NakamaConnection : MonoBehaviour
{
    private string scheme = "http";
    private string host = "localhost";
    private int port = 7350;
    private string serverKey = "defaultkey";

    private IClient client;
    private ISession session;
    private ISocket socket;

    private string ticket;

    private string matchId;

    // Start is called before the first frame update
    async void Start()
    {
        client = new Client(scheme, host, port, serverKey, UnityWebRequestAdapter.Instance);
        session = await client.AuthenticateDeviceAsync(SystemInfo.deviceUniqueIdentifier);
        socket = client.NewSocket();
        await socket.ConnectAsync(session, true);

        socket.ReceivedMatchmakerMatched += OnReceivedMatchmakerMatched;
        socket.ReceivedMatchState += OnReceivedMatchState;

        Debug.Log(session);
        Debug.Log(socket);

    }

    public async void FindMatch()
    {
         Debug.Log("Finding match!");

        var matchmakerTicket = await socket.AddMatchmakerAsync("*", 2, 2);
        ticket = matchmakerTicket.Ticket;

    }

    public async void Ping()
    {
        Debug.Log("Pinging");

        await socket.SendMatchStateAsync(matchId, 1, "", null);
    }

    private async void OnReceivedMatchmakerMatched(IMatchmakerMatched matchmakerMatched)
    {
        var match = await socket.JoinMatchAsync(matchmakerMatched);
        matchId = match.Id;
        
        Debug.Log("Our Session ID:" + match.Self.SessionId);
        foreach (var user in match.Presences)
        {
            Debug.Log("Connected User Session ID:" + user.SessionId);
        }
    }

    private async void OnReceivedMatchState(IMatchState matchState)
    {
        if (matchState.OpCode == 1)
        {
            Debug.Log("Received PING");
            Debug.Log("Sending PONG");
            await socket.SendMatchStateAsync(matchId, 2, "", new [] { matchState.UserPresence });
        }

        if (matchState.OpCode == 2)
        {
            Debug.Log("Received PONG");
            Debug.Log("Sending PING");
            await socket.SendMatchStateAsync(matchId, 1, "", new [] { matchState.UserPresence });
        }
    }
}
