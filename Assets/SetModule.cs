using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Set;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of S.E.T.
/// Created by Zawu and Timwi
/// </summary>
public class SetModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;

    public Texture[] Symbols;
    public Texture[] SymbolsSelected;
    public KMSelectable[] Cards;
    public KMSelectable MainSelectable;

    private MeshRenderer[] _cardImages;
    private MeshRenderer[] _cardSelections;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private List<int> _selected = new List<int>();
    private SetSet _solution;
    private SetCard[] _displayedCards;
    private bool _isSolved;

    struct SetCard : IEquatable<SetCard>
    {
        public int Index { get; private set; }
        public int X { get { return Index % 3; } }
        public int Y { get { return (Index / 3) % 3; } }
        public int NumDots { get { return (Index / 9) % 3; } }
        public int Filling { get { return Index / 27; } }

        public SetCard(int x, int y, int numDots, int filling) { Index = x + 3 * (y + 3 * (numDots + 3 * filling)); }
        public SetCard(int index) { Index = index; }

        private static int get3rdValue(int one, int two) { return (6 - one - two) % 3; }
        public SetCard Get3rdInSet(SetCard other) { return new SetCard(get3rdValue(X, other.X), get3rdValue(Y, other.Y), get3rdValue(NumDots, other.NumDots), get3rdValue(Filling, other.Filling)); }
        public static SetCard GetRandom() { return new SetCard(Rnd.Range(0, 81)); }
        public bool Equals(SetCard other) { return other.Index == Index; }
        public override int GetHashCode() { return Index; }
        public override bool Equals(object obj) { return obj is SetCard && Equals((SetCard) obj); }
    }

    sealed class SetSet : IEquatable<SetSet>
    {
        public SetCard One { get; private set; }
        public SetCard Two { get; private set; }
        public SetCard Three { get; private set; }
        public IEnumerable<SetCard> Cards { get { yield return One; yield return Two; yield return Three; } }
        public SetSet(SetCard one, SetCard two, SetCard three)
        {
            One = one;
            Two = two;
            Three = three;
        }
        public override int GetHashCode()
        {
            // The hash function needs to be invariant under reordering of the three cards
            return (One.GetHashCode() + Two.GetHashCode() + Three.GetHashCode()) ^ (One.GetHashCode() * Two.GetHashCode() * Three.GetHashCode());
        }
        public bool Equals(SetSet other) { return other != null && other.Cards.Contains(One) && other.Cards.Contains(Two) && other.Cards.Contains(Three); }
        public override bool Equals(object obj) { return obj is SetSet && Equals((SetSet) obj); }

        public static SetSet GetRandom(bool avoidSameSymbol = false)
        {
            var one = SetCard.GetRandom();
            tryAgain:
            var two = SetCard.GetRandom();
            if (two.Equals(one) || (avoidSameSymbol && one.X == two.X && one.Y == two.Y))
                goto tryAgain;
            return new SetSet(one, two, one.Get3rdInSet(two));
        }
    }

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        // The textures are in random order and I couldn’t be bothered to fix it in Unity.
        Symbols = Enumerable.Range(0, 81).Select(ix => Symbols.FirstOrDefault(sy => sy.name == string.Format("Icon{0}{1}{2}{3}", (char) ('A' + (ix % 3)), (char) ('1' + ((ix / 3) % 3)), (char) ('a' + ((ix / 9) % 3)), (char) ('1' + ((ix / 27) % 3))))).ToArray();
        SymbolsSelected = Enumerable.Range(0, 81).Select(ix => SymbolsSelected.FirstOrDefault(sy => sy.name == string.Format("IconSel{0}{1}{2}{3}", (char) ('A' + (ix % 3)), (char) ('1' + ((ix / 3) % 3)), (char) ('a' + ((ix / 9) % 3)), (char) ('1' + ((ix / 27) % 3))))).ToArray();

        _cardImages = new MeshRenderer[Cards.Length];
        _cardSelections = new MeshRenderer[Cards.Length];
        for (int i = 0; i < Cards.Length; i++)
        {
            _cardImages[i] = Cards[i].transform.Find("CardImage").GetComponent<MeshRenderer>();
            _cardSelections[i] = Cards[i].transform.Find("CardSelection").GetComponent<MeshRenderer>();
            Cards[i].OnInteract = getClickHandler(i);
        }

        tryAgain:
        _solution = SetSet.GetRandom(avoidSameSymbol: true);
        var cards = new HashSet<SetCard> { _solution.One, _solution.Two, _solution.Three };
        var iter = 0;
        while (cards.Count < 9)
        {
            var newCard = SetCard.GetRandom();
            if (cards.Contains(newCard) || cards.Any(c => cards.Contains(newCard.Get3rdInSet(c))))
            {
                iter++;
                if (iter > 1000)
                    goto tryAgain;
                continue;
            }
            cards.Add(newCard);
        }

        _displayedCards = cards.ToList().Shuffle().ToArray();
        var fillings = new[] { "filled", "wavy", "empty" };
        for (int i = 0; i < _displayedCards.Length; i++)
        {
            Debug.LogFormat("[S.E.T. #{0}] Icon at (module) {1}{2} is (manual) {3}{4}, {5}, {6} dots.", _moduleId,
                (char) ('A' + i % 3), (char) ('1' + i / 3),
                (char) ('A' + _displayedCards[i].X), (char) ('1' + _displayedCards[i].Y),
                fillings[_displayedCards[i].Filling], _displayedCards[i].NumDots);
            _cardImages[i].material.mainTexture = Symbols[_displayedCards[i].Index];
        }

        Debug.LogFormat("[S.E.T. #{0}] Solution: {1}, {2}, {3}.",
            _moduleId, coords(Array.IndexOf(_displayedCards, _solution.One)), coords(Array.IndexOf(_displayedCards, _solution.Two)), coords(Array.IndexOf(_displayedCards, _solution.Three)));
    }

    private string coords(int i)
    {
        return (char) ('A' + i % 3) + "" + (char) ('1' + i / 3);
    }

    private KMSelectable.OnInteractHandler getClickHandler(int i)
    {
        return delegate
        {
            Cards[i].AddInteractionPunch();

            if (_isSolved)
            {
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Cards[i].transform);
                return false;
            }

            if (_selected.Contains(i))
            {
                Audio.PlaySoundAtTransform("Deselect", Cards[i].transform);
                //_cardSelections[i].material = CardUnselected;
                _cardImages[i].material.mainTexture = Symbols[_displayedCards[i].Index];
                _selected.Remove(i);
            }
            else
            {
                _selected.Add(i);
                if (_selected.Count == 3 && _selected.All(s => _solution.Cards.Contains(_displayedCards[s])))
                {
                    Debug.LogFormat("[S.E.T. #{0}] Module solved.", _moduleId);
                    Module.HandlePass();
                    //_cardSelections[i].material = CardSelected;
                    _cardImages[i].material.mainTexture = SymbolsSelected[_displayedCards[i].Index];
                    _isSolved = true;
                    Audio.PlaySoundAtTransform("Chime", Cards[i].transform);
                }
                else if (_selected.Count == 3)
                {
                    Debug.LogFormat("[S.E.T. #{0}] Incorrect set selected: {1}, {2}, {3}.", _moduleId, coords(_selected[0]), coords(_selected[1]), coords(_selected[2]));
                    Module.HandleStrike();
                    _selected.Remove(i);
                }
                else
                {
                    //_cardSelections[i].material = CardSelected;
                    _cardImages[i].material.mainTexture = SymbolsSelected[_displayedCards[i].Index];
                    Audio.PlaySoundAtTransform("Stamp", Cards[i].transform);
                }
            }
            return false;
        };
    }

#pragma warning disable 414
    private string TwitchHelpMessage = @"Use “!{0} press a1 a2” to press any number of buttons in that order, using a–c for columns and 1–3 for rows, or “!{0} press tm br” (top middle, bottom right).";
#pragma warning restore 414


    private KMSelectable[] ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        var m = Regex.Match(command, @"^(?:press|submit|select|toggle|push)((?: +([abc][123]|[lcrtmb][lcrtmb]))+) *$");
        if (!m.Success)
            return null;
        return m.Groups[1].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(str =>
        {
            if (str[0] >= 'a' && str[0] <= 'c' && str[1] >= '1' && str[1] <= '3')
                return Cards[(str[0] - 'a') + 3 * (str[1] - '1')];
            var x = str.Contains('l') ? 0 : str.Contains('r') ? 2 : 1;
            var y = str.Contains('t') ? 0 : str.Contains('b') ? 2 : 1;
            return Cards[x + 3 * y];
        }).ToArray();
    }
}
