
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.UdonNetworkCalling;

public class Card : UdonSharpBehaviour
{
    private UdonSharpBehaviour Deck;
    public int CardIndex;
    public override void OnPickup()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
    }
}
