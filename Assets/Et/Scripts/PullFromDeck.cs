using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.UdonNetworkCalling;

public class PullFromDeck : UdonSharpBehaviour
{
    public GameObject DeckObject;
    private UdonSharpBehaviour Deck;
    private int Counter = 0;

    void Start()
    {
    }
    public override void Interact()
    {
        Debug.Log("Event success: Interact");
        if (Counter == 0)
        {
            Deck = DeckObject.GetComponent<UdonSharpBehaviour>();
        }
        Counter++;


        Debug.Log("Deck: " + Deck);
        string PullCardName = "PullCard";//(string)Deck.GetProgramVariable("PullCardName");
        Debug.Log("PullCardName: " + PullCardName);
        Deck.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Self, PullCardName, gameObject.transform.position, Networking.LocalPlayer.playerId);
        return;
    }
}
