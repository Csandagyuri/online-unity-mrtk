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

    // Start is called before the first frame update
    async void Start()
    {
        client = new Client(scheme, host, port, serverKey, UnityWebRequestAdapter.Instance);
        session = await client.AuthenticateDeviceAsync(SystemInfo.deviceUniqueIdentifier);
        socket = client.NewSocket();
        await socket.ConnectAsync(session, true);

        socket.ReceivedMatchmakerMatched += OnReceivedMatchmakerMatched;

        Debug.Log(session);
        Debug.Log(socket);

    }

    public async void FindMatch()
    {
         Debug.Log("Finding match!");

        var matchmakerTicket = await socket.AddMatchmakerAsync("*", 2, 2);
        ticket = matchmakerTicket.Ticket;

    }

    private async void OnReceivedMatchmakerMatched(IMatchmakerMatched matchmakerMatched)
    {
        var match = await socket.JoinMatchAsync(matchmakerMatched);
        
        Debug.Log("Our Session ID:" + match.Self.SessionId);
        foreach (var user in match.Presences)
        {
            Debug.Log("Connected User Session ID:" + user.SessionId);
        }
    } 
}
