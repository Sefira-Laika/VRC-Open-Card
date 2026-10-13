using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.UdonNetworkCalling;

public class ReturnToDeck : UdonSharpBehaviour
{
    public GameObject DeckObject;
    private UdonSharpBehaviour Deck;

    void Start()
    {
    }
    void OnTriggerEnter(Collider intersect)
    {
        Deck = DeckObject.GetComponent<UdonSharpBehaviour>();
        Debug.Log("Event success: OnTriggerEnter");
        string cardName = intersect.gameObject.name;
        int cardID = intersect.gameObject.GetInstanceID();
        VRC_Pickup Pickup = intersect.gameObject.GetComponent<VRC_Pickup>();
        Pickup.Drop();
        intersect.gameObject.transform.position = DeckObject.transform.position;

        Debug.Log("Deck: " + Deck);
        string ReturnCardFn = "ReturnCard";//
        Debug.Log("Sending card: " + cardName + " with instance ID: "+ cardID + " to deck, for verification. If a match it will be returned.");
        Deck.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Self, ReturnCardFn, cardName,cardID);
        return;
    }
}
