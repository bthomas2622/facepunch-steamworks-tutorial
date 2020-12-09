using Steamworks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class P2PGameObject : MonoBehaviour
{
    void Awake()
    {
        // Handle messages sent in different scenes
        if (CachedMessages.Count > 0)
        {
            foreach(byte[] cachedMessage in CachedMessages)
            {
                thingsThatFailedToHandle.Add(cachedMessage);
            }
            CachedMessages.Clear();
            Invoke("HandleCachedMessages", 0.25f);
        }

        // Send periodic data every 0.4 seconds
        InvokeRepeating("SendDataOnInterval", 0f, 0.4f);

        // Send messages that failed to send every 1.25 seconds
        InvokeRepeating("SendDroppedMessages", 1f, 1.25f);

        // Check every 0.05 seconds for new packets
        InvokeRepeating("ReceiveDataPacket", 0f, 0.05f);
    }

    void SendDroppedMessages()
    {
        if (!SteamManager.Instance.LobbyPartnerDisconnected)
        {
            if (thingsThatFailedToSend.Count > 0)
            {
                retryQueueForThingsThatFailedToSend.Clear();
                foreach (byte[] message in thingsThatFailedToSend)
                {
                    retryQueueForThingsThatFailedToSend.Add(message);
                }
                thingsThatFailedToSend.Clear();
                foreach (byte[] message in retryQueueForThingsThatFailedToSend)
                {
                    Debug.Log("Attempting to send dropped message");
                    var sent = SteamNetworking.SendP2PPacket(opponentSteamId, message);
                    if (!sent)
                    {
                        thingsThatFailedToSend.Add(message);
                    } 
                }
            }
        }
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

    void SendDataOnInterval()
    {
        if (!SteamManager.Instance.LobbyPartnerDisconnected)
        {
            string dataToSend = someDataYouWantToUpdatePeriodically;
            var sent = SteamNetworking.SendP2PPacket(opponentSteamId, ConvertStringToByteArray(dataToSend));
            if (!sent)
            {
                // try one more time
                var sent2 = SteamNetworking.SendP2PPacket(opponentSteamId, ConvertStringToByteArray(dataToSend));
                if (!sent2)
                {
                    thingsThatFailedToSend.Add(ConvertStringToByteArray(dataToSend));
                }
            }
        }
    }

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

    void HandleCachedMessages()
    {
        try
        {
            if (thingsThatFailedToHandle.Count > 0)
            {
                retryQueueForThingsThatFailedToHandle.Clear();
                foreach (byte[] message in thingsThatFailedToHandle)
                {
                    retryQueueForThingsThatFailedToHandle.Add(message);
                }
                thingsThatFailedToHandle.Clear();
                foreach (byte[] missedMessage in retryQueueForThingsThatFailedToHandle)
                {
                    HandleOpponentDataPacket(missedMessage);
                }
            }
        }
        catch
        {
            Debug.Log("Error while handling cached messages");
        }

    }

    private void HandleOpponentDataPacket(byte[] dataPacket)
    {
        try
        {
            string opponentDataSent = ConvertByteArrayToString(dataPacket);
            // Handle opponenet P2P packet
        }
        catch
        {
            Debug.Log("Failed to process incoming opponent data packet");
        }
    }

    // Util
    private byte[] ConvertStringToByteArray(string stringToConvert)
    {
        if (stringToConvert.Length != 0)
        {
            return System.Text.Encoding.UTF8.GetBytes(stringToConvert);
        }
        else
        {
            return System.Text.Encoding.UTF8.GetBytes("");
        }
    }

    // Util
    private string ConvertByteArrayToString(byte[] byteArrayToConvert)
    {
        return System.Text.Encoding.UTF8.GetString(byteArrayToConvert);
    }
}
