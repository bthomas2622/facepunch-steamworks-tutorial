# A Unity tutorial for Facepunch Steamworks C# Wrapper

This is a tutorial for how to use “Facepunch.Steamworks”, a Steamworks C# wrapper, to implement P2P multiplayer and other Steamworks features in a Unity game.

I found the Facepunch.Steamworks documentation to be very good, but found very few code examples on the internet so I wanted to write this in case it can be helpful for anyone as a jumping off point. 

You will need to know you’re way around some Unity C# and have some eagerness to jump into Facepunch.Steamworks and Steamworks documentation.

Feel free to reach out with questions I am [@freebrunch](https://twitter.com/freebrunch) on Twitter.

### Links:
1. [Tutorial code](https://github.com/bthomas2622/facepunch-steamworks-tutorial)
2. [Facepunch.Steamworks wiki](https://wiki.facepunch.com/steamworks/)
3. [Facepunch.Steamworks github](https://github.com/Facepunch/Facepunch.Steamworks)
4. [Steamworks Docs](https://partner.steamgames.com/doc/home)
5. [Nectar of the Gods Steam page](https://store.steampowered.com/app/1421410/Nectar_of_the_Gods/)

[Nectar of the Gods](https://store.steampowered.com/app/1421410/Nectar_of_the_Gods/) was always going to be an online multiplayer game. I love multiplayer games and wanted to make something I could play with friends. But I had a few obstacles to overcome as a first time independent developer:

1. I had never written any netcode.
2. Unity networking support seemed extremely limited.
3. I was hoping to do “Peer 2 Peer” networking rather than “Client Server” because I have no budget for dedicated servers and steady income to support them from an indie game can be elusive.
4. My hope was to use something open source

A lot of people in my initial research were recommending using [Steamworks Docs](https://partner.steamgames.com/doc/features/multiplayer) to do the netcode heavy lifting. Steam offers free backend APIs via Steamworks to help you set up things like game lobbies, P2P packet management, etc. The downside was that I could only distribute my game through Steam to take advantage of this online implementation. This was a worth it tradeoff for me though because I was making a PC game, Steam is a great marketplace, and without much resources Steamworks was a life saver. 

Buuut Steamworks is written in C++, so to use it with Unity I would need to use a C# wrapper. And it appeared the latest and greatest Steamworks C# implementation was a MIT open source project created by Garry Newman (of Garry’s Mod) called “Facepunch.Steamworks”. The most recent release as of this writing was February 28, 2020 but it does seem to still be active with contributions. 

## Prerequisites 
1. You will need valid Steam AppID by [paying Steam app fee](https://partner.steamgames.com/doc/store/application)
2. [Facepunch Install + Setup](https://wiki.facepunch.com/steamworks/Setting_Up)

## Getting Started

I essentially managed all my Steamworks functions and statuses in a singleton created at app startup called **SteamManager.cs**.

In the `Awake()` function I setup my Singleton, and initialize my Steam Client with `SteamClient.Init`.


```
public void Awake()
{
    if (Instance == null)
    {
        daRealOne = true;
        DontDestroyOnLoad(gameObject);
        Instance = this;
        PlayerName = "";
        try
        {
            // Create client
            SteamClient.Init(gameAppId, true);
            if (!SteamClient.IsValid)
            {
                Debug.Log("Steam client not valid");
                throw new Exception();
            }
            PlayerName = SteamClient.Name;
            PlayerSteamId = SteamClient.SteamId;
            playerSteamIdString = PlayerSteamId.ToString();
            activeUnrankedLobbies = new List<Lobby>();
            activeRankedLobbies = new List<Lobby>();
            connectedToSteam = true;
            Debug.Log("Steam initialized: " + PlayerName);
        }
        catch (Exception e)
        {
            connectedToSteam = false;
            playerSteamIdString = "NoSteamId";
            Debug.Log("Error connecting to Steam");
            Debug.Log(e);
        }
    }
    else if (Instance != this)
    {
        Destroy(gameObject);
    }
}
```

The Facepunch.Steamworks architecture mainly centers around creating “events/actions” when a Steam thing happens. You write functions to handle these events and define them in your `Awake()` or `Start()` method. You need to run `SteamClient.RunCallbacks()` in your `Update()` method to field these Steam events. 

Below you can see all the callbacks I’ve defined to handle different Steam events around multiplayer, etc. 

```
   void Start()
    {
        // Callbacks
        SteamMatchmaking.OnLobbyGameCreated += OnLobbyGameCreatedCallback;
        SteamMatchmaking.OnLobbyCreated += OnLobbyCreatedCallback;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEnteredCallback;
        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoinedCallback;
        SteamMatchmaking.OnChatMessage += OnChatMessageCallback;
        SteamMatchmaking.OnLobbyMemberDisconnected += OnLobbyMemberDisconnectedCallback;
        SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeaveCallback;
        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequestedCallback;
        SteamApps.OnDlcInstalled += OnDlcInstalledCallback;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Update()
    {
        SteamClient.RunCallbacks();
    } 
```

And then in either your `OnDisable`, `OnDestroy`, or `OnApplicationQuit` you will want to shut down the Steam client. I use `daRealOne` bool to make sure I only shut down the SteamClient when my singleton is being destroyed, not when I am destroying an instance because it is not my singleton. 

```
   void OnDisable()
    {
        if (daRealOne)
        {
            leaveLobby();
            SteamClient.Shutdown();
        }
    }
```

## Core Loop

I’m not gonna go through every single callback I’ve defined. I recommend looking at the full example code to explore each one. But I will examine an example core loop of:

1. Create multiplayer lobby
2. Another player joins lobby after searching for it
3. All lobby players accept P2P session with each other
4. Game starts and players send packets to each other
5. Lobby cleanup

### Create Multiplayer Lobby

Creating a lobby is done with `await SteamMatchmaking.CreateLobbyAsync(2)` where 2 is the max number of players the lobby can hold. If the output of this async method `.HasValue` then the lobby was correctly created. A Steamworks Lobby object has a bunch of parameters that can be edited, as well as custom parameters that can be set with `SetData`. 

```
public async Task<bool> CreateLobby(int lobbyParameters)
    {
        try
        {
            var createLobbyOutput = await SteamMatchmaking.CreateLobbyAsync(2);
            if (!createLobbyOutput.HasValue)
            {
                Debug.Log("Lobby created but not correctly instantiated");
                throw new Exception();
            }

            LobbyPartnerDisconnected = false;
            hostedMultiplayerLobby = createLobbyOutput.Value;
            hostedMultiplayerLobby.SetPublic();
            hostedMultiplayerLobby.SetJoinable(true);
            hostedMultiplayerLobby.SetData(staticDataString, lobbyParameters)

            currentLobby = hostedMultiplayerLobby;

            return true;
        }
        catch (Exception exception)
        {
            Debug.Log("Failed to create multiplayer lobby");
            Debug.Log(exception.ToString());
            return false;
        }
    }
```

Another player joins lobby after searching for it

The way I populate lobbies a player can join in my game is:

`Lobby[] lobbies = await SteamMatchmaking.LobbyList`

In my game I display these lobbies as options to the player looking for a game, but you could handle all this in the background if you wanted to go with a more matchmaking approach. 

I refined my lobby search with parameters like `WithMaxResults` and `WithKeyValue` to separate my ranked and unranked lobbies. 

```
public async Task<bool> RefreshMultiplayerLobbies(bool ranked)
    {
        try
        {
            if (ranked)
            {
                activeRankedLobbies.Clear();
                Lobby[] lobbies = await SteamMatchmaking.LobbyList.WithMaxResults(20).WithKeyValue(isRankedDataString, TRUE).OrderByNear(playerEloDataString, playerElo).RequestAsync();
                if (lobbies != null)
                {
                    foreach (Lobby lobby in lobbies.ToList())
                    {
                        activeRankedLobbies.Add(lobby);
                    }
                }
                return true;
            }
            else
            {
                activeUnrankedLobbies.Clear();
                Lobby[] lobbies = await SteamMatchmaking.LobbyList.WithMaxResults(20).WithKeyValue(isRankedDataString, FALSE).RequestAsync();
                if (lobbies != null)
                {
                    foreach (Lobby lobby in lobbies.ToList())
                    {
                        activeUnrankedLobbies.Add(lobby);
                    }
                }
                return true;
            }
        } catch (Exception e)
        {
            Debug.Log(e.ToString());
            Debug.Log("Error fetching multiplayer lobbies");
            return true;
        }
    }
```

Join lobby with:

```
    RoomEnter joinedLobbySuccess = await joinedLobby.Join();
    if (joinedLobbySuccess != RoomEnter.Success)
    {
        Debug.Log("failed to join lobby");
    }
```

All lobby players accept P2P session with each other

I kinda stumbled into this implementation and my code has different ways of doing this. There are a lot of different callbacks that can be used for when a player joins a lobby. The main thing is the SteamIds need to be exchanged so that each player can call: 

`SteamNetworking.AcceptP2PSessionWithUser(opponentSteamId);`

One of the ways I exchange SteamIds is with the `OnLobbyMemberJoinedCallack`. So the host gets the joining player SteamId and AcceptsP2P on it. And the person joining can AcceptP2P on `Lobby.Owner.id`.

```
   void OnLobbyMemberJoinedCallback(Lobby lobby, Friend friend)
    {
        Debug.Log("someone else joined lobby");
        if (friend.Id != PlayerSteamId)
        {
            LobbyPartner = friend;
            OpponentSteamId = friend.Id;
            AcceptP2P(OpponentSteamId);
        }
    }
```

Game starts and both players send packets to each other

There are few ways for the host of a lobby to let other members know that it’s time to start the game. They can call `lobby.SetGameServer(PlayerSteamId);` and trigger everyone’s `OnLobbyGameCreated` callback.

`SteamMatchmaking.OnLobbyGameCreated += OnLobbyGameCreatedCallback;`

Or you could communicate with chat messages api, etc. 

After the game has started I have a GameObject (not SteamManager.cs) in my game scene that receives and sends data packets. To receive data packets I use: 

```
   void Awake()
    {
        // Check every 0.05 seconds for new packets
        InvokeRepeating("ReceiveDataPacket", 0f, 0.05f);
    }


    void ReceiveDataPacket()
    {
        while (SteamNetworking.IsP2PPacketAvailable())
        {
            var packet = SteamNetworking.ReadP2PPacket();
            if (packet.HasValue)
            {
                HandleOpponentDataPacket(packet.Value.Data);
            }
        }
    }
```

And to send data packets I use: 

`SteamNetworking.SendP2PPacket(opponentSteamId, ConvertStringToByteArray(dataToSend));`

```
   public void SendAdHocData(string adHocData)
    {
        if (!SteamManager.Instance.LobbyPartnerDisconnected)
        {
            string dataToSend = adHocData;
            var sent = SteamNetworking.SendP2PPacket(opponentSteamId, ConvertStringToByteArray(dataToSend));
            if (!sent)
            {
                var sent2 = SteamNetworking.SendP2PPacket(opponentSteamId, ConvertStringToByteArray(dataToSend));
                if (!sent2)
                {
                    thingsThatFailedToSend.Add(ConvertStringToByteArray(dataToSend));
                }
            }
        }
    }
```

### Lobby Cleanup

Lobby / Game Server are garbage collected automatically when both players call “Leave()” on a Steamworks Lobby object (which is cached in SteamManager singleton). This is why it’s important to call “leaveLobby” on onDestroy(), and when a player quits, and any other case that represents a player leaving a session. 

And that’s the core loop! Checkout the [full boilerplate code](https://github.com/bthomas2622/facepunch-steamworks-tutorial) in the github repo. It has things like inviting a friend to a game lobby, unlocking achievements, setting steam rich presence, etc. Things like achievements and rich presence also need to be configured on the Steam end of things. 

I hope this is helpful! Suggest edits to me [@freebrunch](https://twitter.com/freebrunch) on Twitter.

Plug for my game [Nectar of the Gods](https://store.steampowered.com/app/1421410/Nectar_of_the_Gods/) on Steam. **“An unquenchable multiplayer real-time strategy game where bugs clash over the sweet nectar of sugary beverages. From Beetle Brigade to Spidey Party, you will artfully master strengths and shortcomings to topple boba shops and coffee bars. The countertop mayhem has begun.”**

