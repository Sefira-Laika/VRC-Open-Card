using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.UdonNetworkCalling;


//String card name format
//card + Cardcolour (empty if universal) + CardType + '-' + TableName
//Examples: cardWhite0-Table1, cardBluePlus2-Table2, cardOrangeReverse-Table3, cardPurplePlus2-Table4, cardColourChange-Table6, cardPlus4-TableVIP
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class deck : UdonSharpBehaviour
{
    private const int White = 0;
    private const int Blue = 1;
    private const int Orange = 2;
    private const int Purple = 3;
    private const int Universal = 0; //Universal (Colourless cards) has the same offset as White and uses the same rendering technique
    private const int DeckLength = 108;
    //public variable as an attempt to make the pullcard function visible to other scripts
    public const string PullCardName = nameof(PullCard);
    //U# does not support structs inside classes
    //I have switched this data to strings
    //private struct CardData
    //{
    //    public int ColourOffsetMultiplier;
    //    public string CardColour;
    //    public string CardType;
    //}
    //private struct CardIndex
    //{
    //    public int CardIndexOffset;
    //    public int CardCountPerColour;
    //}

    [UdonSynced]
    private bool[] Deck = new bool[DeckLength];
    [UdonSynced]
    private int RemainingCardsInDeck = -1;
    //public VRC.SDK3.Components.VRCObjectPool CardsInPlay; //Should be synced by default
    //[SerializeField]
    public GameObject[] CardsInPlay = new GameObject[DeckLength]; //Not supported by UdonSynced
    private string TableName = "placeholder";
    //public GameObject cardPrefab; //Does not work globally
    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        Debug.Log("Deck script active: Player joined: " + player.displayName);
        if (Networking.LocalPlayer.isMaster) // stuff should run only once from the world Master and Owner
        {
            Debug.Log("World Master is: " + Networking.LocalPlayer.displayName);
            if (player.isMaster)
            {
                Debug.Log("Player joining is master. This means player is the first to join.");
                Networking.SetOwner(Networking.LocalPlayer, gameObject); //Master must be the Owner
                _Start();
            }
            else
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject); //Master must be the Owner
                Debug.Log("Synchronising cards with " + player.displayName);
                RequestSerialization();
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Others, nameof(SyncCardStatus), player.playerId, Networking.LocalPlayer.playerId);
            }
        }
    }
    //Non synced variables used to check if cards have been generated and the check array initialised as false.
    private bool FirstSync = true;
    private bool[] CardGenerated = new bool[DeckLength];

    //Synchronises and generates the cards for a new player or updates status upon drawing or returning a card.
    [NetworkCallable(maxEventsPerSecond: 100)]
    public void CardStatusUpdate(int cardIndex, bool InDeck, int playerID, int FromPlayerID)
    {
        //if (Networking.LocalPlayer.IsOwner(gameObject)){ Debug.Log("ERROR Owner sent function only called as Others: SyncCardStatus"); return;}
        if (playerID == Networking.LocalPlayer.playerId || playerID == -1) // -1 means all Players
        {
            Debug.Log($"CardStatusUpdate: cardIndex={cardIndex}, InDeck={InDeck}, playerID={playerID}, FromPlayerID={FromPlayerID} = {VRCPlayerApi.GetPlayerById(FromPlayerID).displayName}");
            if (Networking.LocalPlayer.isMaster) { Debug.Log("Master ignores this: CardStatusUpdate"); return; }
            if (FirstSync)
            {
                Debug.Log("First synchronisation: initialising card status");
                FirstSync = false;
                TableName = gameObject.transform.parent.gameObject.name;
                for (int i = 0; i < DeckLength; i++)
                {
                    CardGenerated[i] = false;
                }
            }
            if (!CardGenerated[cardIndex])
            {
                GenerateCard(cardIndex);
                CardGenerated[cardIndex] = true;
            }
            //If the card is not in the deck, it is active (interactable and visible)
            if (InDeck) CardsInPlay[cardIndex].SetActive(false);
            else CardsInPlay[cardIndex].SetActive(true);
        }
    }
    [NetworkCallable]
    public void SyncCardStatus(int playerID, int FromPlayerID)
    {
        if (Networking.LocalPlayer.isMaster) { Debug.Log("Master ignores this: SyncCardStatus"); return; }
        if (playerID == -1) Debug.Log("Syncing cards for everyone");
        else Debug.Log(VRCPlayerApi.GetPlayerById(FromPlayerID).displayName + ": Syncing cards for " + VRCPlayerApi.GetPlayerById(playerID).displayName);
        for (int i = 0; i < DeckLength; i++)
        {
            CardStatusUpdate(i, Deck[i], playerID, FromPlayerID);
        }
    }

    [NetworkCallable(maxEventsPerSecond: 100)]
    public void SyncIfCardInDeck(int cardIndex, bool InDeck, int playerID)
    {   //Message is bugged: it runs in all senarios except when it runs on a master PC. All clients will see it even if master PC fails.
        Debug.Log("Synchronising Card In Deck Status: cardIndex=" + cardIndex + " In deck=" + InDeck + " From Player: " + VRCPlayerApi.GetPlayerById(playerID).displayName + " Remaining Cards In Deck before operation: " + RemainingCardsInDeck);
        if (!Networking.LocalPlayer.isMaster) { Debug.Log("Only Master can sync card in deck status: SyncCardInDeckStatus"); return; }

        if (InDeck != Deck[cardIndex])
        {
            if (InDeck && RemainingCardsInDeck < DeckLength)
            {
                RemainingCardsInDeck++;
            }
            else if (RemainingCardsInDeck > 0 && RemainingCardsInDeck <= DeckLength)
            {
                RemainingCardsInDeck--;
            }
            else
            {
                Debug.LogError("Deck was empty or full on last draw, this is a network call error");
            }
            Deck[cardIndex] = InDeck;
        }
        else
        {
            Debug.LogError("Duplicate draw, this is likely a latency issue");
        }

        if (InDeck) CardsInPlay[cardIndex].SetActive(false);
        else CardsInPlay[cardIndex].SetActive(true);
        Networking.SetOwner(Networking.LocalPlayer, gameObject); //Master must be the Owner
        RequestSerialization();
        string msg;
        if (InDeck) msg = "Card added to deck";
        else msg = "Card pulled from deck";
        Debug.Log("Master: SyncIfCardInDeck completed. " + msg + ". Cards left in deck: " + RemainingCardsInDeck + " Card index: " + cardIndex + " status: " + Deck[cardIndex]);
    }
    void Start()
    {
        for (int i = 0; i < DeckLength; i++)
        {
            Deck[i] = true;
        }
    }
    private void FillDeck()
    {
        for (int i = 0; i < DeckLength; i++)
        {
            CardsInPlay[i].SetActive(false);
            Deck[i] = true;
        }
        RemainingCardsInDeck = DeckLength;
        Debug.Log("Deck filled to length " + Deck.Length + " " + Deck);
        RequestSerialization();
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Others, nameof(SyncCardStatus), -1, Networking.LocalPlayer.playerId);
    }

    private int RandomIndex()
    {
        int availableCardIndex = Random.Range(0, RemainingCardsInDeck - 1);
        string debugString = "RandomIndex: Deck[] contents: ";
        int availableCardIterator = -1; // first card has index 0 so this must be offset.
        for (int i = 0; i < DeckLength; i++)
        {
            if (Deck[i])//true
            {
                availableCardIterator++;
                debugString += i + "[" + Deck[i] + "]";
            }
            else
            {
                debugString += i + "[" + Deck[i] + "]";
            }

            if (availableCardIterator == availableCardIndex)
            {
                Debug.Log("RandomIndex: Card found with index " + i + " Available cards number: " + availableCardIndex);
                return i;
            }
        }
        Debug.LogError("RandomIndex: No available card found with index of available cards = " + availableCardIndex);
        Debug.Log("availableCardIterator: " + availableCardIterator);
        Debug.LogError(debugString);
        return -1;
    }

    private string ColourOffsetToColour(int offset)
    {
        switch (offset)
        {
            case 0:
                return "White";
            case 1:
                return "Blue";
            case 2:
                return "Orange";
            case 3:
                return "Purple";
            default:
                return "OUT OF SCOPE";
        }
    }

    private string GetCardFromIndex(int index, string mode)
    {
        int ColourOffsetMultiplier = -1;
        string CardColour = "";
        string CardType = "";

        //invalid card type
        if (index < 0 || index >= DeckLength)
        {
            ColourOffsetMultiplier = -1;
            CardColour = "OUT OF SCOPE";
            CardType = "OUT OF SCOPE";
        }
        //four 0s 1 per colour indexes 0-3
        else if (index <= 3)
        {
            ColourOffsetMultiplier = index;
            CardColour = ColourOffsetToColour(ColourOffsetMultiplier);
            CardType = "0";
            if (mode == "Draw")
            {
                RemainingCardsInDeck--;
                Deck[index] = false;
            }
        }
        //eight 1s 2 per colour indexes 4-11
        else if (index <= 11)
        {
            ColourOffsetMultiplier = (index - 4) / 2;
            CardColour = ColourOffsetToColour(ColourOffsetMultiplier);
            CardType = "1";
            if (mode == "Draw")
            {
                RemainingCardsInDeck--;
                Deck[index] = false;
            }
        }
        //eight 2s 2 per colour indexes 12-19
        else if (index <= 19)
        {
            ColourOffsetMultiplier = (index - 12) / 2;
            CardColour = ColourOffsetToColour(ColourOffsetMultiplier);
            CardType = "2";
            if (mode == "Draw")
            {
                RemainingCardsInDeck--;
                Deck[index] = false;
            }
        }
        //eight 3s 2 per colour indexes 20-27
        else if (index <= 27)
        {
            ColourOffsetMultiplier = (index - 20) / 2;
            CardColour = ColourOffsetToColour(ColourOffsetMultiplier);
            CardType = "3";
            if (mode == "Draw")
            {
                RemainingCardsInDeck--;
                Deck[index] = false;
            }
        }
        //eight 4s 2 per colour indexes 28-35
        else if (index <= 35)
        {
            ColourOffsetMultiplier = (index - 28) / 2;
            CardColour = ColourOffsetToColour(ColourOffsetMultiplier);
            CardType = "4";
            if (mode == "Draw")
            {
                RemainingCardsInDeck--;
                Deck[index] = false;
            }
        }
        //eight 5s 2 per colour indexes 36-43
        else if (index <= 43)
        {
            ColourOffsetMultiplier = (index - 36) / 2;
            CardColour = ColourOffsetToColour(ColourOffsetMultiplier);
            CardType = "5";
            if (mode == "Draw")
            {
                RemainingCardsInDeck--;
                Deck[index] = false;
            }
        }
        //eight 6s 2 per colour indexes 44-51
        else if (index <= 51)
        {
            ColourOffsetMultiplier = (index - 44) / 2;
            CardColour = ColourOffsetToColour(ColourOffsetMultiplier);
            CardType = "6";
            if (mode == "Draw")
            {
                RemainingCardsInDeck--;
                Deck[index] = false;
            }
        }
        //eight 7s 2 per colour indexes 52-59
        else if (index <= 59)
        {
            ColourOffsetMultiplier = (index - 52) / 2;
            CardColour = ColourOffsetToColour(ColourOffsetMultiplier);
            CardType = "7";
            if (mode == "Draw")
            {
                RemainingCardsInDeck--;
                Deck[index] = false;
            }
        }
        //eight 8s 2 per colour indexes 60-67
        else if (index <= 67)
        {
            ColourOffsetMultiplier = (index - 60) / 2;
            CardColour = ColourOffsetToColour(ColourOffsetMultiplier);
            CardType = "8";
            if (mode == "Draw")
            {
                RemainingCardsInDeck--;
                Deck[index] = false;
            }
        }
        //eight 9s 2 per colour indexes 68-75
        else if (index <= 75)
        {
            ColourOffsetMultiplier = (index - 68) / 2;
            CardColour = ColourOffsetToColour(ColourOffsetMultiplier);
            CardType = "9";
            if (mode == "Draw")
            {
                RemainingCardsInDeck--;
                Deck[index] = false;
            }
        }
        //eight Skips 2 per colour indexes 76-83
        else if (index <= 83)
        {
            ColourOffsetMultiplier = (index - 76) / 2;
            CardColour = ColourOffsetToColour(ColourOffsetMultiplier);
            CardType = "Skip";
            if (mode == "Draw")
            {
                RemainingCardsInDeck--;
                Deck[index] = false;
            }
        }
        //eight Reverses 2 per colour indexes 84-91
        else if (index <= 91)
        {
            ColourOffsetMultiplier = (index - 84) / 2;
            CardColour = ColourOffsetToColour(ColourOffsetMultiplier);
            CardType = "Reverse";
            if (mode == "Draw")
            {
                RemainingCardsInDeck--;
                Deck[index] = false;
            }
        }
        //eight Plus2s 2 per colour indexes 92-99
        else if (index <= 99)
        {
            ColourOffsetMultiplier = (index - 92) / 2;
            CardColour = ColourOffsetToColour(ColourOffsetMultiplier);
            CardType = "Plus2";
            if (mode == "Draw")
            {
                RemainingCardsInDeck--;
                Deck[index] = false;
            }
        }
        //four ColourChange 4 in universal colour indexes 100-103
        else if (index <= 103)
        {
            ColourOffsetMultiplier = (index - 100) / 4;
            CardColour = ""; //universal colour
            CardType = "ColourChange";
            if (mode == "Draw")
            {
                RemainingCardsInDeck--;
                Deck[index] = false;
            }
        }
        //four Plus4s 4 in universal colour indexes 104-107
        else if (index <= 107)
        {
            ColourOffsetMultiplier = (index - 100) / 4;
            CardColour = ""; //universal colour
            CardType = "Plus4";
            if (mode == "Draw")
            {
                RemainingCardsInDeck--;
                Deck[index] = false;
            }
        }

        if (ColourOffsetMultiplier != -1)
        {
            if (Networking.LocalPlayer.isMaster && mode == "Draw" && RemainingCardsInDeck >= 0) //Only Master handles RequestSerialization sync calls
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject); //Master must be the Owner
                RequestSerialization();
            }
            else if (mode == "Draw" && RemainingCardsInDeck >= 0) //Send request to owner to set deck status and use RequestSerialization.
            {
                Debug.Log("Network target:" + nameof(SyncIfCardInDeck) + " Intent is SyncIfCardInDeck. Local RemainingCardsInDeck: " + RemainingCardsInDeck);
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(SyncIfCardInDeck), index, Deck[index], Networking.LocalPlayer.playerId);
            }
        }
        if (RemainingCardsInDeck < 0) RemainingCardsInDeck = 0;
        return "ColourOffsetMultiplier:" + ColourOffsetMultiplier + ",CardColour:" + CardColour + ",CardType:" + CardType + ',';
    }

    //Standardised parsing methods for the transmitted card data
    private string ParseCardDataStringIntoType(string cardData)
    {
        return cardData.Split("CardType:")[1].Split(",")[0];
    }
    private string ParseCardDataStringIntoColour(string cardData)
    {
        return cardData.Split("CardColour:")[1].Split(",")[0];
    }
    private int ParseCardDataStringIntoOffset(string cardData)
    {
        return int.Parse(cardData.Split("ColourOffsetMultiplier:")[1].Split(",")[0]);
    }

    //Gets the possible indexes of the card in the deck
    private string GetCardIndexOffset(string cardData)
    {
        int typeIndexOffset;
        int cardCountPerColour;
        int colourOffsetMultiplier = ParseCardDataStringIntoOffset(cardData);
        switch (ParseCardDataStringIntoType(cardData))
        {
            //four 0s 1 per colour indexes 0-3
            case "0":
                typeIndexOffset = 0;
                cardCountPerColour = 1;
                break;
            //four 1s 2 per colour indexes 4-11
            case "1":
                typeIndexOffset = 4;
                cardCountPerColour = 2;
                break;
            case "2":
                //four 2s 2 per colour indexes 12-19
                typeIndexOffset = 12;
                cardCountPerColour = 2;
                break;
            case "3":
                //four 3s 2 per colour indexes 20-27
                typeIndexOffset = 20;
                cardCountPerColour = 2;
                break;
            //four 4s 2 per colour indexes 28-35
            case "4":
                typeIndexOffset = 28;
                cardCountPerColour = 2;
                break;
            //four 5s 2 per colour indexes 36-43
            case "5":
                typeIndexOffset = 36;
                cardCountPerColour = 2;
                break;
            //four 6s 2 per colour indexes 44-51
            case "6":
                typeIndexOffset = 44;
                cardCountPerColour = 2;
                break;
            //four 7s 2 per colour indexes 52-59
            case "7":
                typeIndexOffset = 52;
                cardCountPerColour = 2;
                break;
            //four 8s 2 per colour indexes 60-67
            case "8":
                typeIndexOffset = 60;
                cardCountPerColour = 2;
                break;
            //four 9s 2 per colour indexes 68-75
            case "9":
                typeIndexOffset = 68;
                cardCountPerColour = 2;
                break;
            //four Skips 2 per colour indexes 76-83
            case "Skip":
                typeIndexOffset = 76;
                cardCountPerColour = 2;
                break;
            //four Reverses 2 per colour indexes 84-91
            case "Reverse":
                typeIndexOffset = 84;
                cardCountPerColour = 2;
                break;
            //four Plus2s 2 per colour indexes 92-99
            case "Plus2":
                typeIndexOffset = 92;
                cardCountPerColour = 2;
                break;
            //four ColourChange 4 in universal colour indexes 100-103
            case "ColourChange":
                typeIndexOffset = 100;
                cardCountPerColour = 4;
                break;
            //four Plus4s 4 in universal colour indexes 104-107
            case "Plus4":
                typeIndexOffset = 104;
                cardCountPerColour = 4;
                break;
            //invalid card type
            default:
                typeIndexOffset = -1;
                cardCountPerColour = -1;
                break;
        }
        int CardIndexOffset = typeIndexOffset + (colourOffsetMultiplier * cardCountPerColour);
        return "CardIndexOffset:" + CardIndexOffset + ",CardCountPerColour:" + cardCountPerColour + ',';
    }
    //Standardised parsing methods for the transmitted card index data
    private int ParsCardIndexStringIntoOffset(string cardIndex)
    {
        return int.Parse(cardIndex.Split("CardIndexOffset:")[1].Split(",")[0]);
    }
    private int ParseCardIndexStringIntoCountPerColour(string cardIndex)
    {
        return int.Parse(cardIndex.Split("CardCountPerColour:")[1].Split(",")[0]);
    }

    private string GetCardData(string card)
    {
        int ColourOffsetMultiplier = -1;
        string CardColour = "";
        string CardType = "";
        if (card.StartsWith("cardWhite"))
        {
            ColourOffsetMultiplier = White;
            CardColour = "White";
            CardType = card.Replace("cardWhite", "");
        }
        else if (card.StartsWith("cardBlue"))
        {
            ColourOffsetMultiplier = Blue;
            CardColour = "Blue";
            CardType = card.Replace("cardBlue", "");
        }
        else if (card.StartsWith("cardOrange"))
        {
            ColourOffsetMultiplier = Orange;
            CardColour = "Orange";
            CardType = card.Replace("cardOrange", "");
        }
        else if (card.StartsWith("cardPurple"))
        {
            ColourOffsetMultiplier = Purple;
            CardColour = "Purple";
            CardType = card.Replace("cardPurple", "");
        }
        else
        {
            ColourOffsetMultiplier = Universal;
            CardColour = "";
            CardType = card.Replace("card", "");
        }
        return "ColourOffsetMultiplier:" + ColourOffsetMultiplier + ",CardColour:" + CardColour + ",CardType:" + CardType + ',';
    }
    void _Start()
    {
        Debug.Log("Generating and initialising deck");
        TableName = gameObject.transform.parent.gameObject.name; //Getting the Table name in unity from the parent
        for (int i = 0; i < DeckLength; i++)
        {
            GenerateCard(i);
        }
        RequestSerialization();
        FillDeck();
        Debug.Log("Deck initialised: Cards in deck: " + RemainingCardsInDeck);
    }
    private void GenerateCard(int index)
    {
        string cardData = GetCardFromIndex(index, "NonDraw");
        string cardColour = ParseCardDataStringIntoColour(cardData);
        string cardType = ParseCardDataStringIntoType(cardData);
        string cardName = "card" + cardColour + cardType + "-" + TableName + "-" + index;
        CardsInPlay[index].name = cardName;
        //CardsInPlay.Pool[index] = VRCInstantiate(cardPrefab); // Add the new card to the pool
        //CardsInPlay[index] = Instantiate(cardPrefab); // Add the new card to the pool
        //Setting variables for the card script.
        UdonSharpBehaviour script = CardsInPlay[index].GetComponent<UdonSharpBehaviour>();
        script.SetProgramVariable("CardIndex", index);
        //Make the card a child of the deck object so it can refer back this script.
        //CardsInPlay[index].transform.parent = gameObject.transform;
        //Setting card look
        SkinnedMeshRenderer look = CardsInPlay[index].GetComponent<SkinnedMeshRenderer>();
        switch (cardColour)//Colours converted from hex bytes to float 0.0-1.0
        {
            default: //FFFFFF
                Color cardWhite = new Color(1, 1, 1);
                look.materials[1].SetColor("_Color", cardWhite);
                look.materials[1].SetColor("_EmissionColor", cardWhite);
                break;
            case "Purple": //601A4A
                Color cardPurple = new Color(0x60 / 255f, 0x1A / 255f, 0x4A / 255f);
                look.materials[1].SetColor("_Color", cardPurple);
                look.materials[1].SetColor("_EmissionColor", cardPurple);
                break;
            case "Orange": //EE442F
                Color cardOrange = new Color(0xEE / 255f, 0x44 / 255f, 0x2F / 255f);
                look.materials[1].SetColor("_Color", cardOrange);
                look.materials[1].SetColor("_EmissionColor", cardOrange);
                break;
            case "Blue": //63ACBE
                Color cardBlue = new Color(0x63 / 255f, 0xAC / 255f, 0xBE / 255f);
                look.materials[1].SetColor("_Color", cardBlue);
                look.materials[1].SetColor("_EmissionColor", cardBlue);
                break;

        }
        //Puts the corresponding card face to the top with a blend shape
        switch (cardType)
        {
            default:// 0 also goes here
                look.SetBlendShapeWeight(0, 1f);
                break;
            case "1":
                look.SetBlendShapeWeight(1, 1f);
                break;
            case "2":
                look.SetBlendShapeWeight(2, 1f);
                break;
            case "3":
                look.SetBlendShapeWeight(3, 1f);
                break;
            case "4":
                look.SetBlendShapeWeight(4, 1f);
                break;
            case "5":
                look.SetBlendShapeWeight(5, 1f);
                break;
            case "6":
                look.SetBlendShapeWeight(6, 1f);
                break;
            case "7":
                look.SetBlendShapeWeight(7, 1f);
                break;
            case "8":
                look.SetBlendShapeWeight(8, 1f);
                break;
            case "9":
                look.SetBlendShapeWeight(9, 1f);
                break;
            case "Skip":
                look.SetBlendShapeWeight(10, 1f);
                break;
            case "Reverse":
                look.SetBlendShapeWeight(11, 1f);
                break;
            case "Plus2":
                look.SetBlendShapeWeight(12, 1f);
                break;
            case "Plus4":
                look.SetBlendShapeWeight(13, 1f);
                break;
            case "ColourChange":
                look.SetBlendShapeWeight(14, 1f);
                break;
        }
        return;
    }
    [NetworkCallable(maxEventsPerSecond: 100)]
    public void PullCard(Vector3 Location, int PlayerID)
    {
        VRCPlayerApi Player = VRCPlayerApi.GetPlayerById(PlayerID);
        string PlayerName = "N/A";
        if (Player == null) { Debug.LogError("Player not found: " + PlayerID);  }
        else { PlayerName = Player.displayName; }
        int index = RandomIndex();
        if (index < 0) { Debug.LogError("PullCard: No available card found");Debug.LogError("PullCard: index = " + index); return; }
        Debug.Log("Event success: Pulling card at location " + Location + " From index: " + index + " pointed at player: " + PlayerName + " (" + PlayerID + ")");
        Debug.Log("Card Data:" + GetCardFromIndex(index, "Draw"));
        Networking.SetOwner(Networking.LocalPlayer, CardsInPlay[index]);
        CardsInPlay[index].transform.position = Location;
        if (PlayerName != "N/A") { CardsInPlay[index].transform.LookAt(Player.GetBonePosition(HumanBodyBones.Head)); }
        CardsInPlay[index].SetActive(true);

        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Others, nameof(CardStatusUpdate), index, Deck[index], -1, Networking.LocalPlayer.playerId);
        return;
    }
    [NetworkCallable]
    public void ReturnCard(string cardName, int cardID)
    {
        for (int i = 0; i < DeckLength; i++)
        {
            //if (CardsInPlay[i].name == cardName)
            if (CardsInPlay[i].GetInstanceID() == cardID) // Changed to entity ID if name change is local
            {
                //Networking.SetOwner(Networking.LocalPlayer, CardsInPlay[i]); //Making sure card transform permissions are available
                CardsInPlay[i].transform.position = gameObject.transform.position; //Move away from trigger.
                CardsInPlay[i].SetActive(false);

                Debug.Log("ReturnCard: " + CardsInPlay[i].name + " returned to index: " + i);
                if (Networking.LocalPlayer.isMaster)
                {
                    if (Deck[i] == false)
                    {
                        RemainingCardsInDeck++;
                    }
                    Deck[i] = true;
                    SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Others, nameof(CardStatusUpdate), i, Deck[i], -1, Networking.LocalPlayer.playerId);
                    RequestSerialization();
                }
                else
                {
                    Debug.Log("Network target:" + nameof(SyncIfCardInDeck) + " Intent is SyncIfCardInDeck. Local RemainingCardsInDeck: " + RemainingCardsInDeck);
                    SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(SyncIfCardInDeck), i, true, Networking.LocalPlayer.playerId);
                }
                return;
            }
        }

        Debug.LogError("ERROR: " + cardName + " could not be returned. Card is not on this table.");
        return;
    }
    [NetworkCallable]
    public void ResetDeck()
    {
        FillDeck();
        Debug.Log("Deck has been reset.");
        return;
    }
}
