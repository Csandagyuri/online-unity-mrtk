using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Nakama;
using Nakama.TinyJson;
using System.Text;

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

    private bool connected = false;

    private GameObject user;
    private GameObject remoteUser;

    private Vector3 remoteUserPosition;

    async void Start()
    {
        user = GameObject.FindGameObjectsWithTag("Player")[0];
        remoteUser = GameObject.FindGameObjectsWithTag("Remote")[0];
        remoteUserPosition = remoteUser.transform.position;
        Debug.Log(user);
        client = new Client(scheme, host, port, serverKey, UnityWebRequestAdapter.Instance);
        session = await client.AuthenticateDeviceAsync(SystemInfo.deviceUniqueIdentifier);
        socket = client.NewSocket();
        await socket.ConnectAsync(session, true);

        socket.ReceivedMatchmakerMatched += OnReceivedMatchmakerMatched;
        //socket.ReceivedMatchState += OnReceivedMatchState;
        socket.ReceivedMatchState += OnReceivedMatchStatePosition;

        Debug.Log(session);
        Debug.Log(socket);
    }

    void Update()
    {
        remoteUser.transform.position = remoteUserPosition;
    }

    public async void FindMatch()
    {
        Debug.Log("Finding match!");
        var matchmakerTicket = await socket.AddMatchmakerAsync("*", 2, 2);
        ticket = matchmakerTicket.Ticket;
    }

    public async void Ping()
    {
        var position = CurrentPositionAsJson();
            Debug.Log(position);
            Debug.Log("Ping");
            await socket.SendMatchStateAsync(matchId, 2, position, null);
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
        connected = true;
    }

    private void OnReceivedMatchStatePosition(IMatchState matchState)
    {
        var positionDictionary = DictFromJson(matchState.State);
        remoteUserPosition = new Vector3(
            float.Parse(positionDictionary["position.x"]),
            float.Parse(positionDictionary["position.y"]),
            float.Parse(positionDictionary["position.z"])
        );
        Debug.Log(remoteUserPosition);
    }

    private async void UpdatePosition(IMatchState matchState)
    {
            Debug.Log("Sending position");
            await socket.SendMatchStateAsync(matchId, 1, CurrentPositionAsJson(), new [] { matchState.UserPresence });
    }

    private string CurrentPositionAsJson()
    {
        var NewPosition = new Dictionary<string, string>
        {
            {"position.x", user.transform.position.x.ToString()},
            {"position.y", user.transform.position.y.ToString()},
            {"position.z", user.transform.position.z.ToString()}
        };
        return NewPosition.ToJson();
    }

    private IDictionary<string,string> DictFromJson(byte[] state)
    {
        return Encoding.UTF8.GetString(state).FromJson<Dictionary<string, string>>();
    }

    private async void OnReceivedMatchState(IMatchState matchState)
    {
        if (matchState.OpCode == 1)
        {
            var position = CurrentPositionAsJson();
            Debug.Log(position);
            Debug.Log("Pong");
            await socket.SendMatchStateAsync(matchId, 2, position, new [] { matchState.UserPresence });
        }

        if (matchState.OpCode == 2)
        {
            Debug.Log("Ping");
            await socket.SendMatchStateAsync(matchId, 1, "", new [] { matchState.UserPresence });
        }
    }
}
